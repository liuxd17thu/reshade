/*
 * Copyright (C) 2024 Patrick Mours
 * SPDX-License-Identifier: BSD-3-Clause
 *
 * IME (Input Method Editor) support for CJK text input.
 * Handles IMM32 messages and exposes composition/candidate state
 * for custom candidate window rendering via ImGui.
 */

#pragma once

#include <string>
#include <vector>

namespace reshade
{
	/// <summary>
	/// Manages IME composition state and candidate list extraction
	/// without relying on the system IME candidate window.
	/// </summary>
	class input_ime
	{
	public:
		input_ime();
		~input_ime();

		/// <summary>
		/// Process an IME-related window message (WM_IME_*) and update internal state.
		/// </summary>
		void handle_ime_message(void *hwnd, unsigned int msg, unsigned long long wParam, long long lParam);

		/// <summary>
		/// Poll IME state from the IMM32 API directly (called every frame).
		/// Reads composition string, cursor position, and candidate list via Imm* functions.
		/// </summary>
		void poll(void *hwnd);

		/// <summary>
		/// Sets committed text from outside (e.g. from WM_IME_COMPOSITION GCS_RESULTSTR).
		/// Also marks result as consumed so poll() does not re-extract.
		/// </summary>
		void set_committed_text(const std::wstring &text);

		/// <summary>
		/// Marks that a key press has occurred while IME is enabled.
		/// poll() checks this flag (see it consumes a new IME event) and
		/// attempts GCS_RESULTSTR extraction. This is needed for IMEs like
		/// RIME Weasel that don't post WM_IME_* messages.
		/// </summary>
		void mark_pending_event() { _pending_ime_event = true; }

		/// <summary>
		/// Clears all IME state without touching COM/TSF.
		/// Safe to call from message handler context.
		/// </summary>
		void clear_composing_state();

		/// <summary>
		/// Whether the IME is currently in composing state.
		/// </summary>
		bool is_composing() const { return _composing; }

		/// <summary>
		/// Whether there is committed text pending consumption.
		/// </summary>
		bool has_committed_text() const { return !_committed_text.empty(); }

		/// <summary>
		/// Committed text captured via GCS_RESULTSTR poll fallback.
		/// Consumer should call this once per frame and feed the result to ImGui.
		/// </summary>
		std::wstring take_committed_text()
		{
			std::wstring result;
			result.swap(_committed_text);
			return result;
		}

		/// <summary>
		/// The current composition string (e.g. pinyin syllables being composed).
		/// </summary>
		const std::wstring &composition() const { return _composition_str; }

		/// <summary>
		/// Cursor position within the composition string.
		/// </summary>
		int composition_cursor() const { return _composition_cursor; }

		/// <summary>
		/// Whether a candidate list is available.
		/// </summary>
		bool has_candidates() const { return !_candidates.empty(); }

		/// <summary>
		/// Number of candidates currently available.
		/// </summary>
		size_t candidate_count() const { return _candidates.size(); }

		/// <summary>
		/// Get candidate string at the given index.
		/// </summary>
		const std::wstring &candidate(size_t index) const { return _candidates[index]; }

		/// <summary>
		/// Currently selected candidate index (0-based within the full list).
		/// </summary>
		int candidate_selection() const { return _candidate_selection; }

		/// <summary>
		/// Page size for the candidate display.
		/// </summary>
		int candidate_page_size() const { return _candidate_page_size; }

		/// <summary>
		/// Starting index of the current candidate page.
		/// </summary>
		int candidate_page_start() const { return _candidate_page_start; }

	private:
		bool _composing = false;
		std::wstring _composition_str;
		std::wstring _committed_text;
		int _composition_cursor = 0;

		std::vector<std::wstring> _candidates;
		int _candidate_selection = 0;
		int _candidate_page_start = 0;
		int _candidate_page_size = 0;
		bool _pending_ime_event = false;

		/// <summary>
		/// Clear all IME state.
		/// </summary>
		void clear();
	};
}
