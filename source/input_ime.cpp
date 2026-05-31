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
#include "utf8.h"

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
		clear();
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
		_extracted_by_poll = false;
		_composition_ended = false;
	}

	void input_ime::set_committed_text(const std::wstring &text)
	{
		_committed_text = text;
	}

	void input_ime::append_committed_text(const std::wstring &text)
	{
		_committed_text += text;
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
			// Reset per-frame flags at the start of every poll() call.
			_extracted_by_poll = false;

			// Extract GCS_RESULTSTR only when a composition just ended, not while
			// composition is still active. Polling during an active composition
			// risks reading an intermediate GCS_RESULTSTR value that doesn't
			// reflect the final committed text (e.g. "，" before shift is processed
			// into "《"). The ENDCOMPOSITION handler has the definitive extraction.
			// This poll() fallback is needed for RIME which doesn't send
			// WM_IME_ENDCOMPOSITION at all.
			if (_composition_ended)
			{
				const LONG result_size = ImmGetCompositionStringW(himc, GCS_RESULTSTR, nullptr, 0);
				if (result_size > 0)
				{
					std::string mbs{};
					_committed_text.resize(result_size / sizeof(wchar_t));
					ImmGetCompositionStringW(himc, GCS_RESULTSTR, &_committed_text[0], result_size);
					while (!_committed_text.empty() && _committed_text.back() == L'\0')
						_committed_text.pop_back();
					_extracted_by_poll = true;
					utf8::unchecked::utf16to8(_committed_text.begin(), _committed_text.end(), std::back_inserter(mbs));
					log::message(log::level::debug, "POLL: [%s]", mbs.c_str());
				}
				_composition_ended = false;

				_composing = false;
				_composition_str.clear();
				_composition_cursor = 0;
				_candidates.clear();
				_candidate_selection = 0;
				_candidate_page_start = 0;
				_candidate_page_size = 0;
			}
			// Always consume the flag to prevent false triggers on subsequent frames.

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
				const HWND hw = static_cast<HWND>(hwnd);
				const HIMC himc = ImmGetContext(hw);
				if (himc != nullptr)
				{
					// Skip extraction if poll() already did it this frame via
					// composition-end detection. We check _extracted_by_poll
					// instead of _committed_text.empty() because draw_gui()
					// may have already consumed _committed_text by now.
					if (!_extracted_by_poll)
					{
						//const LONG result_size = ImmGetCompositionStringW(himc, GCS_RESULTSTR, nullptr, 0);
						//if (result_size > 0)
						//{
						//	_committed_text.resize(result_size / sizeof(wchar_t));
						//	ImmGetCompositionStringW(himc, GCS_RESULTSTR, &_committed_text[0], result_size);
						//	while (!_committed_text.empty() && _committed_text.back() == L'\0')
						//		_committed_text.pop_back();
						//	_extracted_by_poll = true;
						//	log::message(log::level::debug, "IME extract via MSG");
						//}
					}
					log::message(log::level::debug, "WM_IME_COMPOSITION: NOTHING");
					ImmReleaseContext(hw, himc);
				}
			}
			break;
		}
		case WM_IME_ENDCOMPOSITION:
		{
			// Extract committed text here instead of relying on poll() because
			// for fast composition cycles (e.g. Shift+, → "《" in Microsoft
			// Pinyin), poll() runs between START and END when GCS_RESULTSTR is
			// still empty. The data only becomes available by END time.
			std::string mbs("");
			if (_composing && _committed_text.empty())
			{
				const HWND hw = static_cast<HWND>(hwnd);
				const HIMC himc = ImmGetContext(hw);
				if (himc != nullptr)
				{
					const LONG result_size = ImmGetCompositionStringW(himc, GCS_RESULTSTR, nullptr, 0);
					if (result_size > 0)
					{
						_committed_text.resize(result_size / sizeof(wchar_t));
						ImmGetCompositionStringW(himc, GCS_RESULTSTR, &_committed_text[0], result_size);
						while (!_committed_text.empty() && _committed_text.back() == L'\0')
							_committed_text.pop_back();
						utf8::unchecked::utf16to8(_committed_text.begin(), _committed_text.end(), std::back_inserter(mbs));
					}
					ImmReleaseContext(hw, himc);
				}
			}
			_composition_ended = _composing;
			_composing = false;
			_composition_str.clear();
			_composition_cursor = 0;
			_candidates.clear();
			_candidate_selection = 0;
			_candidate_page_start = 0;
			_candidate_page_size = 0;

			log::message(log::level::debug, "WM_IME_ENDCOMPOSITION, RESULTSTR [%s]", mbs.c_str());
			break;
		}
		case WM_IME_CHAR:
		{
			reshade::log::message(reshade::log::level::debug, "WM_IME_CHAR: [0x%x] [0x%x]", wParam, lParam);
			break;
		}
		case WM_IME_SELECT:
		{
			if (lParam == 0)
				clear();
			break;
		}
		case WM_INPUTLANGCHANGE:
		{
			reshade::log::message(reshade::log::level::debug, "WM_INPUTLANGCHANGE");
			this->clear();
			break;
		}
		case WM_IME_NOTIFY:
		{
			reshade::log::message(reshade::log::level::debug, "WM_IME_NOTIFY: [0x%x] [0x%x]", wParam, lParam);
			if (wParam == 0x10)
				_composition_ended = true;
			break;
		}
		case WM_IME_KEYDOWN:
		{
			reshade::log::message(reshade::log::level::debug, "WM_IME_KEYDOWN: [0x%x] [0x%x]", wParam, lParam);
			break;
		}
		default:
			reshade::log::message(reshade::log::level::debug, "WM_UNKNOWN[0x%x]: [0x%x] [0x%x]", msg, wParam, lParam);
		}
	}
}
