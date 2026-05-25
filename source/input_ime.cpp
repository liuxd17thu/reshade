/*
 * Copyright (C) 2024 Patrick Mours
 * SPDX-License-Identifier: BSD-3-Clause
 */

#include "input_ime.hpp"
#include <Windows.h>
#include <imm.h>
#include <cstdint>
#include <cstring>
#include <algorithm>
#include "dll_log.hpp"

using namespace reshade;

#pragma comment(lib, "imm32")

namespace reshade
{
	input_ime::input_ime()
	{
		clear();
	}

	input_ime::~input_ime()
	{
		clear_composing_state();
	}

	void input_ime::clear()
	{
		_composing = false;
		_composition_str.clear();
		_composition_cursor = 0;
		_candidates.clear();
		_candidate_selection = 0;
		_candidate_page_start = 0;
		_candidate_page_size = 0;
		_committed_text.clear();
		_pending_ime_event = false;
	}

	void input_ime::clear_composing_state()
	{
		_composing = false;
		_composition_str.clear();
		_composition_cursor = 0;
		_candidates.clear();
		_candidate_selection = 0;
		_candidate_page_start = 0;
		_candidate_page_size = 0;
		_committed_text.clear();
		_pending_ime_event = false;
	}

	void input_ime::set_committed_text(const std::wstring &text)
	{
		_committed_text = text;
	}

	void input_ime::poll(void *hwnd)
	{
		if (hwnd == nullptr)
		{
			clear();
			return;
		}

		const HWND hw = static_cast<HWND>(hwnd);
		const HIMC himc = ImmGetContext(hw);

		if (himc == nullptr)
		{
			clear();
			return;
		}

		const DWORD comp_size = ImmGetCompositionStringW(himc, GCS_COMPSTR, nullptr, 0);
		const bool is_comp_active = (comp_size > 0);
		
		if (!is_comp_active)
		{
			// On each key press while IME is enabled, input.cpp's WM_KEYDOWN handler
			// sets _pending_ime_event = true. We consume it here unconditionally on
			// the first poll() after the event, regardless of whether we extract.
			// If the message handler (WM_IME_COMPOSITION) already filled _committed_text,
			// we skip extraction — the text is already captured.
			if (_pending_ime_event)
			{
				_pending_ime_event = false;
				if (_committed_text.empty())
				{
					const LONG result_size = ImmGetCompositionStringW(himc, GCS_RESULTSTR, nullptr, 0);
					if (result_size > 0)
					{
						_committed_text.resize(result_size / sizeof(wchar_t));
						log::message(log::level::debug, "IME extract via POLL: %ls", _committed_text.c_str());
						ImmGetCompositionStringW(himc, GCS_RESULTSTR, &_committed_text[0], result_size);
						while (!_committed_text.empty() && _committed_text.back() == L'\0')
							_committed_text.pop_back();
					}
				}
			}

			_composing = false;
			_composition_str.clear();
			_composition_cursor = 0;
			_candidates.clear();
			_candidate_selection = 0;
			_candidate_page_start = 0;
			_candidate_page_size = 0;
			ImmReleaseContext(hw, himc);
			return;
		}

		_composing = true;

		_composition_str.resize(comp_size / sizeof(wchar_t));
		ImmGetCompositionStringW(himc, GCS_COMPSTR, &_composition_str[0], comp_size);

		{
			const DWORD cursor = ImmGetCompositionStringW(himc, GCS_CURSORPOS, nullptr, 0);
			_composition_cursor = static_cast<int>(cursor);
		}

		_candidates.clear();
		_candidate_selection = 0;
		_candidate_page_start = 0;
		_candidate_page_size = 0;

		// Get candidate list via IMM32 (works for both IMM32 and TSF-based IMEs)
		const DWORD cand_buf_size = ImmGetCandidateListW(himc, 0, nullptr, 0);
		if (cand_buf_size > 0)
		{
			std::vector<uint8_t> buf(cand_buf_size);
			if (const DWORD ret = ImmGetCandidateListW(himc, 0,
				reinterpret_cast<LPCANDIDATELIST>(buf.data()), cand_buf_size);
				ret > 0 && ret <= cand_buf_size)
			{
				const CANDIDATELIST *cl = reinterpret_cast<const CANDIDATELIST *>(buf.data());
				const DWORD count = cl->dwCount;
				_candidate_selection = static_cast<int>(cl->dwSelection);
				_candidate_page_start = static_cast<int>(cl->dwPageStart);
				_candidate_page_size = static_cast<int>(cl->dwPageSize);

				_candidates.reserve(count);
				for (DWORD i = 0; i < count; ++i)
				{
					const DWORD offset = cl->dwOffset[i];
					if (offset < cand_buf_size)
					{
						const wchar_t *str = reinterpret_cast<const wchar_t *>(buf.data() + offset);
						_candidates.push_back(std::wstring(str));
					}
				}

				// If IME didn't report page size, use a sensible default
				if (_candidate_page_size <= 0)
					_candidate_page_size = static_cast<int>(std::min<size_t>(count, 9));
			}
		}
	
		ImmReleaseContext(hw, himc);
	}

	void input_ime::handle_ime_message(void *hwnd, unsigned int msg, unsigned long long wParam, long long lParam)
	{
		switch (msg)
		{
		case WM_IME_STARTCOMPOSITION:
		{
			_composing = true;
			_composition_str.clear();
			_composition_cursor = 0;
			_candidates.clear();
			_candidate_selection = 0;
			_candidate_page_start = 0;
			_candidate_page_size = 0;

			reshade::log::message(reshade::log::level::debug, "WM_IME_STARTCOMPOSITION");
			break;
		}
		case WM_IME_COMPOSITION:
		{
			if (lParam & GCS_RESULTSTR)
			{
				// Consume the pending event. If poll() already extracted this commit,
				// _pending_ime_event is already false here. If poll() ran before the
				// IME messages were processed, this is where the event is consumed.
				// Either way, only one extraction path fires per key press.
				_pending_ime_event = false;
				// Immediate extraction of committed text, following the same approach
				// as Dalamud: consume GCS_RESULTSTR right in the message handler.
				const HWND hw = static_cast<HWND>(hwnd);
				const HIMC himc = ImmGetContext(hw);
				if (himc != nullptr)
				{
					const LONG result_size = ImmGetCompositionStringW(himc, GCS_RESULTSTR, nullptr, 0);
					if (result_size > 0)
					{
						_committed_text.resize(result_size / sizeof(wchar_t));
						log::message(log::level::debug, "IME extract via MSG: %ls", _committed_text.c_str());
						ImmGetCompositionStringW(himc, GCS_RESULTSTR, &_committed_text[0], result_size);
						while (!_committed_text.empty() && _committed_text.back() == L'\0')
							_committed_text.pop_back();
					}
					ImmReleaseContext(hw, himc);
				}
			}
			break;
		}
		case WM_IME_ENDCOMPOSITION:
		{
			_composing = false;
			_composition_str.clear();
			_composition_cursor = 0;
			_candidates.clear();
			_candidate_selection = 0;
			_candidate_page_start = 0;
			_candidate_page_size = 0;

			reshade::log::message(reshade::log::level::debug, "WM_IME_ENDCOMPOSITION");
			break;
		}
		case WM_IME_CHAR:
		{
			reshade::log::message(reshade::log::level::debug, "WM_IME_CHAR: %s", reinterpret_cast<char *>(wParam));
			break;
		}
		case WM_IME_SELECT:
		{
			if (lParam == 0)
			clear();
			break;
		}
		}
	}
}
