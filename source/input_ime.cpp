#include "input_ime.hpp"
#include "dll_log.hpp"
#include <Windows.h>
#include <imm.h>
#include <immdev.h>
#include <utf8.h>

#pragma comment(lib, "imm32")

namespace reshade
{
	input_ime::input_ime() { clear(); }

	input_ime::~input_ime() { clear(); }

	void input_ime::clear()
	{
		_composition_text.clear();
		_committed_text.clear();
		_candidates.clear();
		_candidate_selection = 0;
		_candidate_page_start = 0;
		_candidate_page_end = 0;
		set_stage(ime_stage::idle);
	}

	input_ime::ime_stage input_ime::set_stage(const ime_stage next)
	{
		const auto prev = _stage;
		_stage = next;
#if RESHADE_VERBOSE_LOG
		if(prev != next)
			reshade::log::message(reshade::log::level::debug, "%s -> %s", input_ime::to_string(prev).c_str(), input_ime::to_string(next).c_str());
#endif
		return prev;
	}

	input_ime::ime_stage input_ime::stage() const
	{
		return _stage;
	}

	void input_ime::poll_compose(void *hwnd)
	{
		std::string mbs;
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
		if (stage() == ime_stage::result_avail)
			return;

		const auto comp_size = ImmGetCompositionStringW(himc, GCS_COMPSTR, nullptr, 0);
		if(comp_size > 0) // PATH2: Get composition & candidates
		{
			std::wstring old_compose = std::move(_composition_text);
			set_stage(ime_stage::composing);
			// composition
			_composition_text.resize(comp_size / sizeof(wchar_t));
			ImmGetCompositionStringW(himc, GCS_COMPSTR, &_composition_text[0], comp_size);
#if RESHADE_VERBOSE_LOG
			mbs.clear();
			utf8::unchecked::utf16to8(_composition_text.begin(), _composition_text.end(), std::back_inserter(mbs));
			log::message(log::level::debug, "IME POLL/COMP: %s", mbs.c_str());
#endif
			if (old_compose != _composition_text)
				set_stage(ime_stage::composing_update);
		}
		else
		{
			if (stage() != ime_stage::idle)
				set_stage(ime_stage::composition_ended);
		}
		ImmReleaseContext(hw, himc);

		if(stage() == ime_stage::composing_update)
			poll_candidate(hwnd);
		return;
	}

	void input_ime::poll_result(void *hwnd)
	{
		std::string mbs {};
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

		const auto result_size = ImmGetCompositionStringW(himc, GCS_RESULTSTR, nullptr, 0);
		if (stage() == ime_stage::composition_ended) // PATH1: Get result
		{
			if (result_size > 0)
			{
				_committed_text.clear();
				_committed_text.resize(result_size / sizeof(wchar_t));
				ImmGetCompositionStringW(himc, GCS_RESULTSTR, &_committed_text[0], result_size);
#if RESHADE_VERBOSE_LOG
				mbs.clear();
				utf8::unchecked::utf16to8(_committed_text.begin(), _committed_text.end(), std::back_inserter(mbs));
				log::message(log::level::debug, "IME POLL/RES: %s", mbs.c_str());
#endif
				if (ImmGetCompositionStringW(himc, GCS_COMPSTR, nullptr, 0) == 0)
					set_stage(ime_stage::result_avail);
				else
					set_stage(ime_stage::composing);
			}
			_candidates.clear();
			_composition_text.clear();
			_candidate_selection = 0;
			_candidate_page_start = 0;
			_candidate_page_size = 0;
		}
		return;
	}

	void input_ime::poll_candidate(void *hwnd)
	{
		if (stage() != ime_stage::composing_update && stage() != ime_stage::composing)
			return;
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

		// candidates
		const DWORD cand_buf_size = ImmGetCandidateListW(himc, 0, nullptr, 0);
		if (cand_buf_size > 0)
		{
			std::vector<uint8_t> buf(cand_buf_size);
			if (const DWORD ret = ImmGetCandidateListW(himc, 0, reinterpret_cast<LPCANDIDATELIST>(buf.data()), cand_buf_size);
				ret > 0 && ret <= cand_buf_size)
			{
				const CANDIDATELIST *cl = reinterpret_cast<const CANDIDATELIST *>(buf.data());
				const DWORD count = cl->dwCount;
				_candidate_selection = static_cast<int>(cl->dwSelection);
				_candidate_page_start = static_cast<int>(cl->dwPageStart);
				_candidate_page_size = static_cast<int>(cl->dwPageSize);

				_candidates.clear();
				_candidates.reserve(count);
				for (DWORD i = 0; i < count; ++i)
				{
					const DWORD offset = cl->dwOffset[i];
					if (offset < cand_buf_size)
					{
						const wchar_t *wstr = reinterpret_cast<const wchar_t *>(buf.data() + offset);
						_candidates.push_back(std::wstring(wstr));
					}
				}
				if (_candidate_page_size <= 0)
					_candidate_page_size = std::min(static_cast<int>(count), 9);
				set_stage(ime_stage::composing);
			}
		}

		ImmReleaseContext(hw, himc);
	}

	void input_ime::handle_ime_message(void *hwnd, unsigned int msg, unsigned long long wParam, long long lParam)
	{
		bool need_poll = false;
		switch (msg)
		{
		case WM_IME_STARTCOMPOSITION:
			break;
		case WM_IME_COMPOSITION:
			break;
		case WM_IME_ENDCOMPOSITION:
			break;
		case WM_IME_NOTIFY:
			if (wParam == 0x10)
				need_poll = true;
			if (wParam == IMN_CHANGECANDIDATE || wParam == IMN_OPENCANDIDATE)
				poll_candidate(hwnd);
			if (stage() == ime_stage::composing)
				set_stage(ime_stage::composition_ended);
			break;
		}
		if (stage() != ime_stage::result_avail || need_poll)
		{
			poll_compose(hwnd);
			poll_candidate(hwnd);
		}
		return;
	}

	void input_ime::set_enabled(bool enabled)
	{
		_enabled = enabled;
		return;
	}

	bool input_ime::is_enabled() const { return _enabled; }
}
