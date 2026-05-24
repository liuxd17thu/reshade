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
		_result_consumed_in_poll = false;
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
		_result_consumed_in_poll = false;
	}

	void input_ime::set_committed_text(const std::wstring &text)
	{
		_committed_text = text;
		_result_consumed_in_poll = true;
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
			// Composition has ended. Extract any pending committed result text ONCE per commit cycle.
			// GCS_RESULTSTR may linger in the IME context across frames, so _result_consumed_in_poll
			// prevents re-extraction on subsequent polls.
			if (!_result_consumed_in_poll)
			{
				const LONG result_size = ImmGetCompositionStringW(himc, GCS_RESULTSTR, nullptr, 0);
				if (result_size > 0)
				{
					_committed_text.resize(result_size / sizeof(wchar_t));
					ImmGetCompositionStringW(himc, GCS_RESULTSTR, &_committed_text[0], result_size);
					while (!_committed_text.empty() && _committed_text.back() == L'\0')
						_committed_text.pop_back();
					_result_consumed_in_poll = true;
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

		// New composition is active; reset extraction flag so that GCS_RESULTSTR
		// can be captured when this composition ends.
		if (!_composing)
			_result_consumed_in_poll = false;
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
		(void)hwnd;
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
			// Reset extraction flag for the new composition cycle, so poll() can
			// extract GCS_RESULTSTR when this new composition ends (or the message
			// handler extracts it from WM_IME_COMPOSITION and sets the flag again).
			_result_consumed_in_poll = false;
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
