#pragma once

#include <string>
#include <vector>

namespace reshade
{
	class input_ime
	{
	public:
		enum class ime_stage : unsigned int
		{
			idle,
			composing,
			composing_update,
			composition_ended,
			result_avail
		};
		ime_stage stage = ime_stage::idle;

		static inline std::string to_string(const ime_stage &stage)
		{
			switch (stage)
			{
			case ime_stage::idle: return "IME_IDLE"; break;
			case ime_stage::composing: return "IME_COMP"; break;
			case ime_stage::composing_update: return "IME_COMP_UPDATE"; break;
			case ime_stage::composition_ended: return "IME_COMP_END"; break;
			case ime_stage::result_avail: return "IME_RES_OK"; break;
			default: return "IME_X";
			}
		}

		input_ime();
		~input_ime();

		void clear();

		bool has_committed_text() const { return !_committed_text.empty(); }

		std::wstring committed_text(bool take = false)
		{
			std::wstring out;
			if (take)
				std::swap(out, _committed_text);
			else
				out = _committed_text;
			return out;
		}

		std::wstring composition_text() const { return _composition_text; }

		std::vector<std::wstring> candidates() const { return _candidates; }

		bool has_candidates() const { return !_candidates.empty(); }

		int candidate_page_size() const { return _candidate_page_size; }

		int candidate_page_start() const { return _candidate_page_start; }

		int candidate_page_end() const { return _candidate_page_end; }

		size_t candidate_count() const { return _candidates.size(); }

		const std::wstring &candidate(size_t index) const { return _candidates[index]; }

		int candidate_selection() const { return _candidate_selection; }

		void handle_ime_message(void *hwnd, unsigned int msg, unsigned long long wParam, long long lParam);

		void poll(void *hwnd);

		void poll_candidate(void *hwnd);

		void set_enabled(bool enabled);

		bool is_enabled() const;

	private:
		bool _enabled = false;
		std::wstring _committed_text;
		std::wstring _composition_text;
		std::vector<std::wstring> _candidates;
		int _candidate_selection = 0;
		int _candidate_page_size = 0;
		int _candidate_page_start = 0;
		int _candidate_page_end = 0;

		bool _composing = false;
		bool _composition_ended = false;
		bool _result_polled = false;
	};
}
