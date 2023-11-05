/*
 * Copyright (C) 2021 Patrick Mours
 * Copyright (C) 2014-2022 Omar Cornut
 * SPDX-License-Identifier: BSD-3-Clause OR MIT
 */

struct imgui_font_18600
{
	static void convert(const ImFont &from, imgui_font_18600 &to);
	static void convert(const imgui_font_18600 &from, ImFont &to);

	ImVector<float> IndexAdvanceX;
	float FallbackAdvanceX;
	float FontSize;
	ImVector<ImWchar> IndexLookup;
	ImVector<ImFontGlyph> Glyphs;
	const ImFontGlyph *FallbackGlyph;
	ImFontAtlas *ContainerAtlas;
	const ImFontConfig *ConfigData;
	short ConfigDataCount;
	ImWchar FallbackChar;
	ImWchar EllipsisChar;
	ImWchar DotChar;
	bool DirtyLookupTables;
	float Scale;
	float Ascent, Descent;
	int MetricsTotalSurface;
	ImU8 Used4kPagesMap[(IM_UNICODE_CODEPOINT_MAX + 1) / 4096 / 8];
};

struct imgui_io_18600
{
	static void convert(const ImGuiIO &from, imgui_io_18600 &to);

	ImGuiConfigFlags ConfigFlags;
	ImGuiBackendFlags BackendFlags;
	ImVec2 DisplaySize;
	float DeltaTime;
	float IniSavingRate;
	const char *IniFilename;
	const char *LogFilename;
	float MouseDoubleClickTime;
	float MouseDoubleClickMaxDist;
	float MouseDragThreshold;
	int KeyMap[22];
	float KeyRepeatDelay;
	float KeyRepeatRate;
	void *UserData;

	ImFontAtlas *Fonts;
	float FontGlobalScale;
	bool FontAllowUserScaling;
	imgui_font_18600 *FontDefault;
	ImVec2 DisplayFramebufferScale;

	bool ConfigDockingNoSplit;
	bool ConfigDockingWithShift;
	bool ConfigDockingAlwaysTabBar;
	bool ConfigDockingTransparentPayload;

	bool ConfigViewportsNoAutoMerge;
	bool ConfigViewportsNoTaskBarIcon;
	bool ConfigViewportsNoDecoration;
	bool ConfigViewportsNoDefaultParent;

	bool MouseDrawCursor;
	bool ConfigMacOSXBehaviors;
	bool ConfigInputTextCursorBlink;
	bool ConfigDragClickToInputText;
	bool ConfigWindowsResizeFromEdges;
	bool ConfigWindowsMoveFromTitleBarOnly;
	float ConfigMemoryCompactTimer;

	const char *BackendPlatformName;
	const char *BackendRendererName;
	void *BackendPlatformUserData;
	void *BackendRendererUserData;
	void *BackendLanguageUserData;

	const char *(*GetClipboardTextFn)(void *user_data);
	void (*SetClipboardTextFn)(void *user_data, const char *text);
	void *ClipboardUserData;

	ImVec2 MousePos;
	bool MouseDown[5];
	float MouseWheel;
	float MouseWheelH;
	ImGuiID MouseHoveredViewport;
	bool KeyCtrl;
	bool KeyShift;
	bool KeyAlt;
	bool KeySuper;
	bool KeysDown[512];
	float NavInputs[20];

	bool WantCaptureMouse;
	bool WantCaptureKeyboard;
	bool WantTextInput;
	bool WantSetMousePos;
	bool WantSaveIniSettings;
	bool NavActive;
	bool NavVisible;
	float Framerate;
	int MetricsRenderVertices;
	int MetricsRenderIndices;
	int MetricsRenderWindows;
	int MetricsActiveWindows;
	int MetricsActiveAllocations;
	ImVec2 MouseDelta;

	bool WantCaptureMouseUnlessPopupClose;
	ImGuiKey KeyMods;
	ImGuiKey KeyModsPrev;
	ImVec2 MousePosPrev;
	ImVec2 MouseClickedPos[5];
	double MouseClickedTime[5];
	bool MouseClicked[5];
	bool MouseDoubleClicked[5];
	ImU16 MouseClickedCount[5];
	ImU16 MouseClickedLastCount[5];
	bool MouseReleased[5];
	bool MouseDownOwned[5];
	bool MouseDownOwnedUnlessPopupClose[5];
	float MouseDownDuration[5];
	float MouseDownDurationPrev[5];
	ImVec2 MouseDragMaxDistanceAbs[5];
	float MouseDragMaxDistanceSqr[5];
	float KeysDownDuration[512];
	float KeysDownDurationPrev[512];
	float NavInputsDownDuration[20];
	float NavInputsDownDurationPrev[20];
	float PenPressure;
	bool AppFocusLost;
	ImWchar16 InputQueueSurrogate;
	ImVector<ImWchar> InputQueueCharacters;
};

struct imgui_style_18600
{
	static void convert(const ImGuiStyle &from, imgui_style_18600 &to);

	float Alpha;
	float DisabledAlpha;
	ImVec2 WindowPadding;
	float WindowRounding;
	float WindowBorderSize;
	ImVec2 WindowMinSize;
	ImVec2 WindowTitleAlign;
	ImGuiDir WindowMenuButtonPosition;
	float ChildRounding;
	float ChildBorderSize;
	float PopupRounding;
	float PopupBorderSize;
	ImVec2 FramePadding;
	float FrameRounding;
	float FrameBorderSize;
	ImVec2 ItemSpacing;
	ImVec2 ItemInnerSpacing;
	ImVec2 CellPadding;
	ImVec2 TouchExtraPadding;
	float IndentSpacing;
	float ColumnsMinSpacing;
	float ScrollbarSize;
	float ScrollbarRounding;
	float GrabMinSize;
	float GrabRounding;
	float LogSliderDeadzone;
	float TabRounding;
	float TabBorderSize;
	float TabMinWidthForCloseButton;
	ImGuiDir ColorButtonPosition;
	ImVec2 ButtonTextAlign;
	ImVec2 SelectableTextAlign;
	ImVec2 DisplayWindowPadding;
	ImVec2 DisplaySafeAreaPadding;
	float MouseCursorScale;
	bool AntiAliasedLines;
	bool AntiAliasedLinesUseTex;
	bool AntiAliasedFill;
	float CurveTessellationTol;
	float CircleTessellationMaxError;
	ImVec4 Colors[55];
};

struct imgui_list_clipper_18600
{
	static void convert(const ImGuiListClipper &from, imgui_list_clipper_18600 &to);
	static void convert(const imgui_list_clipper_18600 &from, ImGuiListClipper &to);

	int DisplayStart;
	int DisplayEnd;
	int ItemsCount;
	float ItemsHeight;
	float StartPosY;
	void *TempData;
};

struct imgui_function_table_18600
{
	imgui_io_18600 &(*GetIO)();
	imgui_style_18600 &(*GetStyle)();
	const char *(*GetVersion)();
	bool(*Begin)(const char *name, bool *p_open, ImGuiWindowFlags flags);
	void(*End)();
	bool(*BeginChild)(const char *str_id, const ImVec2 &size, bool border, ImGuiWindowFlags flags);
	bool(*BeginChild2)(ImGuiID id, const ImVec2 &size, bool border, ImGuiWindowFlags flags);
	void(*EndChild)();
	bool(*IsWindowAppearing)();
	bool(*IsWindowCollapsed)();
	bool(*IsWindowFocused)(ImGuiFocusedFlags flags);
	bool(*IsWindowHovered)(ImGuiHoveredFlags flags);
	ImDrawList *(*GetWindowDrawList)();
	float(*GetWindowDpiScale)();
	ImVec2(*GetWindowPos)();
	ImVec2(*GetWindowSize)();
	float(*GetWindowWidth)();
	float(*GetWindowHeight)();
	void(*SetNextWindowPos)(const ImVec2 &pos, ImGuiCond cond, const ImVec2 &pivot);
	void(*SetNextWindowSize)(const ImVec2 &size, ImGuiCond cond);
	void(*SetNextWindowSizeConstraints)(const ImVec2 &size_min, const ImVec2 &size_max, ImGuiSizeCallback custom_callback, void *custom_callback_data);
	void(*SetNextWindowContentSize)(const ImVec2 &size);
	void(*SetNextWindowCollapsed)(bool collapsed, ImGuiCond cond);
	void(*SetNextWindowFocus)();
	void(*SetNextWindowBgAlpha)(float alpha);
	void(*SetWindowPos)(const ImVec2 &pos, ImGuiCond cond);
	void(*SetWindowSize)(const ImVec2 &size, ImGuiCond cond);
	void(*SetWindowCollapsed)(bool collapsed, ImGuiCond cond);
	void(*SetWindowFocus)();
	void(*SetWindowFontScale)(float scale);
	void(*SetWindowPos2)(const char *name, const ImVec2 &pos, ImGuiCond cond);
	void(*SetWindowSize2)(const char *name, const ImVec2 &size, ImGuiCond cond);
	void(*SetWindowCollapsed2)(const char *name, bool collapsed, ImGuiCond cond);
	void(*SetWindowFocus2)(const char *name);
	ImVec2(*GetContentRegionAvail)();
	ImVec2(*GetContentRegionMax)();
	ImVec2(*GetWindowContentRegionMin)();
	ImVec2(*GetWindowContentRegionMax)();
	float(*GetScrollX)();
	float(*GetScrollY)();
	void(*SetScrollX)(float scroll_x);
	void(*SetScrollY)(float scroll_y);
	float(*GetScrollMaxX)();
	float(*GetScrollMaxY)();
	void(*SetScrollHereX)(float center_x_ratio);
	void(*SetScrollHereY)(float center_y_ratio);
	void(*SetScrollFromPosX)(float local_x, float center_x_ratio);
	void(*SetScrollFromPosY)(float local_y, float center_y_ratio);
	void(*PushFont)(imgui_font_18600 *font);
	void(*PopFont)();
	void(*PushStyleColor)(ImGuiCol idx, ImU32 col);
	void(*PushStyleColor2)(ImGuiCol idx, const ImVec4 &col);
	void(*PopStyleColor)(int count);
	void(*PushStyleVar)(ImGuiStyleVar idx, float val);
	void(*PushStyleVar2)(ImGuiStyleVar idx, const ImVec2 &val);
	void(*PopStyleVar)(int count);
	void(*PushAllowKeyboardFocus)(bool allow_keyboard_focus);
	void(*PopAllowKeyboardFocus)();
	void(*PushButtonRepeat)(bool repeat);
	void(*PopButtonRepeat)();
	void(*PushItemWidth)(float item_width);
	void(*PopItemWidth)();
	void(*SetNextItemWidth)(float item_width);
	float(*CalcItemWidth)();
	void(*PushTextWrapPos)(float wrap_local_pos_x);
	void(*PopTextWrapPos)();
	imgui_font_18600 *(*GetFont)();
	float(*GetFontSize)();
	ImVec2(*GetFontTexUvWhitePixel)();
	ImU32(*GetColorU32)(ImGuiCol idx, float alpha_mul);
	ImU32(*GetColorU322)(const ImVec4 &col);
	ImU32(*GetColorU323)(ImU32 col);
	const ImVec4 &(*GetStyleColorVec4)(ImGuiCol idx);
	void(*Separator)();
	void(*SameLine)(float offset_from_start_x, float spacing);
	void(*NewLine)();
	void(*Spacing)();
	void(*Dummy)(const ImVec2 &size);
	void(*Indent)(float indent_w);
	void(*Unindent)(float indent_w);
	void(*BeginGroup)();
	void(*EndGroup)();
	ImVec2(*GetCursorPos)();
	float(*GetCursorPosX)();
	float(*GetCursorPosY)();
	void(*SetCursorPos)(const ImVec2 &local_pos);
	void(*SetCursorPosX)(float local_x);
	void(*SetCursorPosY)(float local_y);
	ImVec2(*GetCursorStartPos)();
	ImVec2(*GetCursorScreenPos)();
	void(*SetCursorScreenPos)(const ImVec2 &pos);
	void(*AlignTextToFramePadding)();
	float(*GetTextLineHeight)();
	float(*GetTextLineHeightWithSpacing)();
	float(*GetFrameHeight)();
	float(*GetFrameHeightWithSpacing)();
	void(*PushID)(const char *str_id);
	void(*PushID2)(const char *str_id_begin, const char *str_id_end);
	void(*PushID3)(const void *ptr_id);
	void(*PushID4)(int int_id);
	void(*PopID)();
	ImGuiID(*GetID)(const char *str_id);
	ImGuiID(*GetID2)(const char *str_id_begin, const char *str_id_end);
	ImGuiID(*GetID3)(const void *ptr_id);
	void(*TextUnformatted)(const char *text, const char *text_end);
	void(*TextV)(const char *fmt, va_list args);
	void(*TextColoredV)(const ImVec4 &col, const char *fmt, va_list args);
	void(*TextDisabledV)(const char *fmt, va_list args);
	void(*TextWrappedV)(const char *fmt, va_list args);
	void(*LabelTextV)(const char *label, const char *fmt, va_list args);
	void(*BulletTextV)(const char *fmt, va_list args);
	bool(*Button)(const char *label, const ImVec2 &size);
	bool(*SmallButton)(const char *label);
	bool(*InvisibleButton)(const char *str_id, const ImVec2 &size, ImGuiButtonFlags flags);
	bool(*ArrowButton)(const char *str_id, ImGuiDir dir);
	void(*Image)(ImTextureID user_texture_id, const ImVec2 &size, const ImVec2 &uv0, const ImVec2 &uv1, const ImVec4 &tint_col, const ImVec4 &border_col);
	bool(*ImageButton)(ImTextureID user_texture_id, const ImVec2 &size, const ImVec2 &uv0, const ImVec2 &uv1, int frame_padding, const ImVec4 &bg_col, const ImVec4 &tint_col);
	bool(*Checkbox)(const char *label, bool *v);
	bool(*CheckboxFlags)(const char *label, int *flags, int flags_value);
	bool(*CheckboxFlags2)(const char *label, unsigned int *flags, unsigned int flags_value);
	bool(*RadioButton)(const char *label, bool active);
	bool(*RadioButton2)(const char *label, int *v, int v_button);
	void(*ProgressBar)(float fraction, const ImVec2 &size_arg, const char *overlay);
	void(*Bullet)();
	bool(*BeginCombo)(const char *label, const char *preview_value, ImGuiComboFlags flags);
	void(*EndCombo)();
	bool(*Combo)(const char *label, int *current_item, const char *const items[], int items_count, int popup_max_height_in_items);
	bool(*Combo2)(const char *label, int *current_item, const char *items_separated_by_zeros, int popup_max_height_in_items);
	bool(*Combo3)(const char *label, int *current_item, bool(*items_getter)(void *data, int idx, const char **out_text), void *data, int items_count, int popup_max_height_in_items);
	bool(*DragFloat)(const char *label, float *v, float v_speed, float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragFloat2)(const char *label, float v[2], float v_speed, float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragFloat3)(const char *label, float v[3], float v_speed, float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragFloat4)(const char *label, float v[4], float v_speed, float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragFloatRange2)(const char *label, float *v_current_min, float *v_current_max, float v_speed, float v_min, float v_max, const char *format, const char *format_max, ImGuiSliderFlags flags);
	bool(*DragInt)(const char *label, int *v, float v_speed, int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragInt2)(const char *label, int v[2], float v_speed, int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragInt3)(const char *label, int v[3], float v_speed, int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragInt4)(const char *label, int v[4], float v_speed, int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragIntRange2)(const char *label, int *v_current_min, int *v_current_max, float v_speed, int v_min, int v_max, const char *format, const char *format_max, ImGuiSliderFlags flags);
	bool(*DragScalar)(const char *label, ImGuiDataType data_type, void *p_data, float v_speed, const void *p_min, const void *p_max, const char *format, ImGuiSliderFlags flags);
	bool(*DragScalarN)(const char *label, ImGuiDataType data_type, void *p_data, int components, float v_speed, const void *p_min, const void *p_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderFloat)(const char *label, float *v, float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderFloat2)(const char *label, float v[2], float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderFloat3)(const char *label, float v[3], float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderFloat4)(const char *label, float v[4], float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderAngle)(const char *label, float *v_rad, float v_degrees_min, float v_degrees_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderInt)(const char *label, int *v, int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderInt2)(const char *label, int v[2], int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderInt3)(const char *label, int v[3], int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderInt4)(const char *label, int v[4], int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderScalar)(const char *label, ImGuiDataType data_type, void *p_data, const void *p_min, const void *p_max, const char *format, ImGuiSliderFlags flags);
	bool(*SliderScalarN)(const char *label, ImGuiDataType data_type, void *p_data, int components, const void *p_min, const void *p_max, const char *format, ImGuiSliderFlags flags);
	bool(*VSliderFloat)(const char *label, const ImVec2 &size, float *v, float v_min, float v_max, const char *format, ImGuiSliderFlags flags);
	bool(*VSliderInt)(const char *label, const ImVec2 &size, int *v, int v_min, int v_max, const char *format, ImGuiSliderFlags flags);
	bool(*VSliderScalar)(const char *label, const ImVec2 &size, ImGuiDataType data_type, void *p_data, const void *p_min, const void *p_max, const char *format, ImGuiSliderFlags flags);
	bool(*InputText)(const char *label, char *buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void *user_data);
	bool(*InputTextMultiline)(const char *label, char *buf, size_t buf_size, const ImVec2 &size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void *user_data);
	bool(*InputTextWithHint)(const char *label, const char *hint, char *buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void *user_data);
	bool(*InputFloat)(const char *label, float *v, float step, float step_fast, const char *format, ImGuiInputTextFlags flags);
	bool(*InputFloat2)(const char *label, float v[2], const char *format, ImGuiInputTextFlags flags);
	bool(*InputFloat3)(const char *label, float v[3], const char *format, ImGuiInputTextFlags flags);
	bool(*InputFloat4)(const char *label, float v[4], const char *format, ImGuiInputTextFlags flags);
	bool(*InputInt)(const char *label, int *v, int step, int step_fast, ImGuiInputTextFlags flags);
	bool(*InputInt2)(const char *label, int v[2], ImGuiInputTextFlags flags);
	bool(*InputInt3)(const char *label, int v[3], ImGuiInputTextFlags flags);
	bool(*InputInt4)(const char *label, int v[4], ImGuiInputTextFlags flags);
	bool(*InputDouble)(const char *label, double *v, double step, double step_fast, const char *format, ImGuiInputTextFlags flags);
	bool(*InputScalar)(const char *label, ImGuiDataType data_type, void *p_data, const void *p_step, const void *p_step_fast, const char *format, ImGuiInputTextFlags flags);
	bool(*InputScalarN)(const char *label, ImGuiDataType data_type, void *p_data, int components, const void *p_step, const void *p_step_fast, const char *format, ImGuiInputTextFlags flags);
	bool(*ColorEdit3)(const char *label, float col[3], ImGuiColorEditFlags flags);
	bool(*ColorEdit4)(const char *label, float col[4], ImGuiColorEditFlags flags);
	bool(*ColorPicker3)(const char *label, float col[3], ImGuiColorEditFlags flags);
	bool(*ColorPicker4)(const char *label, float col[4], ImGuiColorEditFlags flags, const float *ref_col);
	bool(*ColorButton)(const char *desc_id, const ImVec4 &col, ImGuiColorEditFlags flags, ImVec2 size);
	void(*SetColorEditOptions)(ImGuiColorEditFlags flags);
	bool(*TreeNode)(const char *label);
	bool(*TreeNodeV)(const char *str_id, const char *fmt, va_list args);
	bool(*TreeNodeV2)(const void *ptr_id, const char *fmt, va_list args);
	bool(*TreeNodeEx)(const char *label, ImGuiTreeNodeFlags flags);
	bool(*TreeNodeExV)(const char *str_id, ImGuiTreeNodeFlags flags, const char *fmt, va_list args);
	bool(*TreeNodeExV2)(const void *ptr_id, ImGuiTreeNodeFlags flags, const char *fmt, va_list args);
	void(*TreePush)(const char *str_id);
	void(*TreePush2)(const void *ptr_id);
	void(*TreePop)();
	float(*GetTreeNodeToLabelSpacing)();
	bool(*CollapsingHeader)(const char *label, ImGuiTreeNodeFlags flags);
	bool(*CollapsingHeader2)(const char *label, bool *p_visible, ImGuiTreeNodeFlags flags);
	void(*SetNextItemOpen)(bool is_open, ImGuiCond cond);
	bool(*Selectable)(const char *label, bool selected, ImGuiSelectableFlags flags, const ImVec2 &size);
	bool(*Selectable2)(const char *label, bool *p_selected, ImGuiSelectableFlags flags, const ImVec2 &size);
	bool(*BeginListBox)(const char *label, const ImVec2 &size);
	void(*EndListBox)();
	bool(*ListBox)(const char *label, int *current_item, const char *const items[], int items_count, int height_in_items);
	bool(*ListBox2)(const char *label, int *current_item, bool(*items_getter)(void *data, int idx, const char **out_text), void *data, int items_count, int height_in_items);
	void(*PlotLines)(const char *label, const float *values, int values_count, int values_offset, const char *overlay_text, float scale_min, float scale_max, ImVec2 graph_size, int stride);
	void(*PlotLines2)(const char *label, float(*values_getter)(void *data, int idx), void *data, int values_count, int values_offset, const char *overlay_text, float scale_min, float scale_max, ImVec2 graph_size);
	void(*PlotHistogram)(const char *label, const float *values, int values_count, int values_offset, const char *overlay_text, float scale_min, float scale_max, ImVec2 graph_size, int stride);
	void(*PlotHistogram2)(const char *label, float(*values_getter)(void *data, int idx), void *data, int values_count, int values_offset, const char *overlay_text, float scale_min, float scale_max, ImVec2 graph_size);
	void(*Value)(const char *prefix, bool b);
	void(*Value2)(const char *prefix, int v);
	void(*Value3)(const char *prefix, unsigned int v);
	void(*Value4)(const char *prefix, float v, const char *float_format);
	bool(*BeginMenuBar)();
	void(*EndMenuBar)();
	bool(*BeginMainMenuBar)();
	void(*EndMainMenuBar)();
	bool(*BeginMenu)(const char *label, bool enabled);
	void(*EndMenu)();
	bool(*MenuItem)(const char *label, const char *shortcut, bool selected, bool enabled);
	bool(*MenuItem2)(const char *label, const char *shortcut, bool *p_selected, bool enabled);
	void(*BeginTooltip)();
	void(*EndTooltip)();
	void(*SetTooltipV)(const char *fmt, va_list args);
	bool(*BeginPopup)(const char *str_id, ImGuiWindowFlags flags);
	bool(*BeginPopupModal)(const char *name, bool *p_open, ImGuiWindowFlags flags);
	void(*EndPopup)();
	void(*OpenPopup)(const char *str_id, ImGuiPopupFlags popup_flags);
	void(*OpenPopup2)(ImGuiID id, ImGuiPopupFlags popup_flags);
	void(*OpenPopupOnItemClick)(const char *str_id, ImGuiPopupFlags popup_flags);
	void(*CloseCurrentPopup)();
	bool(*BeginPopupContextItem)(const char *str_id, ImGuiPopupFlags popup_flags);
	bool(*BeginPopupContextWindow)(const char *str_id, ImGuiPopupFlags popup_flags);
	bool(*BeginPopupContextVoid)(const char *str_id, ImGuiPopupFlags popup_flags);
	bool(*IsPopupOpen)(const char *str_id, ImGuiPopupFlags flags);
	bool(*BeginTable)(const char *str_id, int column, ImGuiTableFlags flags, const ImVec2 &outer_size, float inner_width);
	void(*EndTable)();
	void(*TableNextRow)(ImGuiTableRowFlags row_flags, float min_row_height);
	bool(*TableNextColumn)();
	bool(*TableSetColumnIndex)(int column_n);
	void(*TableSetupColumn)(const char *label, ImGuiTableColumnFlags flags, float init_width_or_weight, ImGuiID user_id);
	void(*TableSetupScrollFreeze)(int cols, int rows);
	void(*TableHeadersRow)();
	void(*TableHeader)(const char *label);
	ImGuiTableSortSpecs *(*TableGetSortSpecs)();
	int(*TableGetColumnCount)();
	int(*TableGetColumnIndex)();
	int(*TableGetRowIndex)();
	const char *(*TableGetColumnName)(int column_n);
	ImGuiTableColumnFlags(*TableGetColumnFlags)(int column_n);
	void(*TableSetColumnEnabled)(int column_n, bool v);
	void(*TableSetBgColor)(ImGuiTableBgTarget target, ImU32 color, int column_n);
	void(*Columns)(int count, const char *id, bool border);
	void(*NextColumn)();
	int(*GetColumnIndex)();
	float(*GetColumnWidth)(int column_index);
	void(*SetColumnWidth)(int column_index, float width);
	float(*GetColumnOffset)(int column_index);
	void(*SetColumnOffset)(int column_index, float offset_x);
	int(*GetColumnsCount)();
	bool(*BeginTabBar)(const char *str_id, ImGuiTabBarFlags flags);
	void(*EndTabBar)();
	bool(*BeginTabItem)(const char *label, bool *p_open, ImGuiTabItemFlags flags);
	void(*EndTabItem)();
	bool(*TabItemButton)(const char *label, ImGuiTabItemFlags flags);
	void(*SetTabItemClosed)(const char *tab_or_docked_window_label);
	ImGuiID(*DockSpace)(ImGuiID id, const ImVec2 &size, ImGuiDockNodeFlags flags, const ImGuiWindowClass *window_class);
	void(*SetNextWindowDockID)(ImGuiID dock_id, ImGuiCond cond);
	void(*SetNextWindowClass)(const ImGuiWindowClass *window_class);
	ImGuiID(*GetWindowDockID)();
	bool(*IsWindowDocked)();
	bool(*BeginDragDropSource)(ImGuiDragDropFlags flags);
	bool(*SetDragDropPayload)(const char *type, const void *data, size_t sz, ImGuiCond cond);
	void(*EndDragDropSource)();
	bool(*BeginDragDropTarget)();
	const ImGuiPayload *(*AcceptDragDropPayload)(const char *type, ImGuiDragDropFlags flags);
	void(*EndDragDropTarget)();
	const ImGuiPayload *(*GetDragDropPayload)();
	void(*BeginDisabled)(bool disabled);
	void(*EndDisabled)();
	void(*PushClipRect)(const ImVec2 &clip_rect_min, const ImVec2 &clip_rect_max, bool intersect_with_current_clip_rect);
	void(*PopClipRect)();
	void(*SetItemDefaultFocus)();
	void(*SetKeyboardFocusHere)(int offset);
	bool(*IsItemHovered)(ImGuiHoveredFlags flags);
	bool(*IsItemActive)();
	bool(*IsItemFocused)();
	bool(*IsItemClicked)(ImGuiMouseButton mouse_button);
	bool(*IsItemVisible)();
	bool(*IsItemEdited)();
	bool(*IsItemActivated)();
	bool(*IsItemDeactivated)();
	bool(*IsItemDeactivatedAfterEdit)();
	bool(*IsItemToggledOpen)();
	bool(*IsAnyItemHovered)();
	bool(*IsAnyItemActive)();
	bool(*IsAnyItemFocused)();
	ImVec2(*GetItemRectMin)();
	ImVec2(*GetItemRectMax)();
	ImVec2(*GetItemRectSize)();
	void(*SetItemAllowOverlap)();
	bool(*IsRectVisible)(const ImVec2 &size);
	bool(*IsRectVisible2)(const ImVec2 &rect_min, const ImVec2 &rect_max);
	double(*GetTime)();
	int(*GetFrameCount)();
	ImDrawList *(*GetBackgroundDrawList)();
	ImDrawList *(*GetForegroundDrawList)();
	ImDrawList *(*GetBackgroundDrawList2)(ImGuiViewport *viewport);
	ImDrawList *(*GetForegroundDrawList2)(ImGuiViewport *viewport);
	ImDrawListSharedData *(*GetDrawListSharedData)();
	const char *(*GetStyleColorName)(ImGuiCol idx);
	void(*SetStateStorage)(ImGuiStorage *storage);
	ImGuiStorage *(*GetStateStorage)();
	bool(*BeginChildFrame)(ImGuiID id, const ImVec2 &size, ImGuiWindowFlags flags);
	void(*EndChildFrame)();
	ImVec2(*CalcTextSize)(const char *text, const char *text_end, bool hide_text_after_double_hash, float wrap_width);
	ImVec4(*ColorConvertU32ToFloat4)(ImU32 in);
	ImU32(*ColorConvertFloat4ToU32)(const ImVec4 &in);
	void(*ColorConvertRGBtoHSV)(float r, float g, float b, float &out_h, float &out_s, float &out_v);
	void(*ColorConvertHSVtoRGB)(float h, float s, float v, float &out_r, float &out_g, float &out_b);
	int(*GetKeyIndex)(ImGuiKey imgui_key);
	bool(*IsKeyDown)(int user_key_index);
	bool(*IsKeyPressed)(int user_key_index, bool repeat);
	bool(*IsKeyReleased)(int user_key_index);
	int(*GetKeyPressedAmount)(int user_key_index, float repeat_delay, float rate);
	void(*CaptureKeyboardFromApp)(bool want_capture_keyboard_value);
	bool(*IsMouseDown)(ImGuiMouseButton button);
	bool(*IsMouseClicked)(ImGuiMouseButton button, bool repeat);
	bool(*IsMouseReleased)(ImGuiMouseButton button);
	bool(*IsMouseDoubleClicked)(ImGuiMouseButton button);
	int(*GetMouseClickedCount)(ImGuiMouseButton button);
	bool(*IsMouseHoveringRect)(const ImVec2 &r_min, const ImVec2 &r_max, bool clip);
	bool(*IsMousePosValid)(const ImVec2 *mouse_pos);
	bool(*IsAnyMouseDown)();
	ImVec2(*GetMousePos)();
	ImVec2(*GetMousePosOnOpeningCurrentPopup)();
	bool(*IsMouseDragging)(ImGuiMouseButton button, float lock_threshold);
	ImVec2(*GetMouseDragDelta)(ImGuiMouseButton button, float lock_threshold);
	void(*ResetMouseDragDelta)(ImGuiMouseButton button);
	ImGuiMouseCursor(*GetMouseCursor)();
	void(*SetMouseCursor)(ImGuiMouseCursor cursor_type);
	void(*CaptureMouseFromApp)(bool want_capture_mouse_value);
	const char *(*GetClipboardText)();
	void(*SetClipboardText)(const char *text);
	bool(*DebugCheckVersionAndDataLayout)(const char *version_str, size_t sz_io, size_t sz_style, size_t sz_vec2, size_t sz_vec4, size_t sz_drawvert, size_t sz_drawidx);
	void(*SetAllocatorFunctions)(ImGuiMemAllocFunc alloc_func, ImGuiMemFreeFunc free_func, void *user_data);
	void(*GetAllocatorFunctions)(ImGuiMemAllocFunc *p_alloc_func, ImGuiMemFreeFunc *p_free_func, void **p_user_data);
	void *(*MemAlloc)(size_t size);
	void(*MemFree)(void *ptr);
	int(*ImGuiStorage_GetInt)(const ImGuiStorage *_this, ImGuiID key, int default_val);
	void(*ImGuiStorage_SetInt)(ImGuiStorage *_this, ImGuiID key, int val);
	bool(*ImGuiStorage_GetBool)(const ImGuiStorage *_this, ImGuiID key, bool default_val);
	void(*ImGuiStorage_SetBool)(ImGuiStorage *_this, ImGuiID key, bool val);
	float(*ImGuiStorage_GetFloat)(const ImGuiStorage *_this, ImGuiID key, float default_val);
	void(*ImGuiStorage_SetFloat)(ImGuiStorage *_this, ImGuiID key, float val);
	void *(*ImGuiStorage_GetVoidPtr)(const ImGuiStorage *_this, ImGuiID key);
	void(*ImGuiStorage_SetVoidPtr)(ImGuiStorage *_this, ImGuiID key, void *val);
	int *(*ImGuiStorage_GetIntRef)(ImGuiStorage *_this, ImGuiID key, int default_val);
	bool *(*ImGuiStorage_GetBoolRef)(ImGuiStorage *_this, ImGuiID key, bool default_val);
	float *(*ImGuiStorage_GetFloatRef)(ImGuiStorage *_this, ImGuiID key, float default_val);
	void **(*ImGuiStorage_GetVoidPtrRef)(ImGuiStorage *_this, ImGuiID key, void *default_val);
	void(*ImGuiStorage_SetAllInt)(ImGuiStorage *_this, int val);
	void(*ImGuiStorage_BuildSortByKey)(ImGuiStorage *_this);
	void(*ConstructImGuiListClipper)(imgui_list_clipper_18600 *_this);
	void(*DestructImGuiListClipper)(imgui_list_clipper_18600 *_this);
	void(*ImGuiListClipper_Begin)(imgui_list_clipper_18600 *_this, int items_count, float items_height);
	void(*ImGuiListClipper_End)(imgui_list_clipper_18600 *_this);
	bool(*ImGuiListClipper_Step)(imgui_list_clipper_18600 *_this);
	void(*ImGuiListClipper_ForceDisplayRangeByIndices)(imgui_list_clipper_18600 *_this, int item_min, int item_max);
	void(*ImDrawList_PushClipRect)(ImDrawList *_this, ImVec2 clip_rect_min, ImVec2 clip_rect_max, bool intersect_with_current_clip_rect);
	void(*ImDrawList_PushClipRectFullScreen)(ImDrawList *_this);
	void(*ImDrawList_PopClipRect)(ImDrawList *_this);
	void(*ImDrawList_PushTextureID)(ImDrawList *_this, ImTextureID texture_id);
	void(*ImDrawList_PopTextureID)(ImDrawList *_this);
	void(*ImDrawList_AddLine)(ImDrawList *_this, const ImVec2 &p1, const ImVec2 &p2, ImU32 col, float thickness);
	void(*ImDrawList_AddRect)(ImDrawList *_this, const ImVec2 &p_min, const ImVec2 &p_max, ImU32 col, float rounding, ImDrawFlags flags, float thickness);
	void(*ImDrawList_AddRectFilled)(ImDrawList *_this, const ImVec2 &p_min, const ImVec2 &p_max, ImU32 col, float rounding, ImDrawFlags flags);
	void(*ImDrawList_AddRectFilledMultiColor)(ImDrawList *_this, const ImVec2 &p_min, const ImVec2 &p_max, ImU32 col_upr_left, ImU32 col_upr_right, ImU32 col_bot_right, ImU32 col_bot_left);
	void(*ImDrawList_AddQuad)(ImDrawList *_this, const ImVec2 &p1, const ImVec2 &p2, const ImVec2 &p3, const ImVec2 &p4, ImU32 col, float thickness);
	void(*ImDrawList_AddQuadFilled)(ImDrawList *_this, const ImVec2 &p1, const ImVec2 &p2, const ImVec2 &p3, const ImVec2 &p4, ImU32 col);
	void(*ImDrawList_AddTriangle)(ImDrawList *_this, const ImVec2 &p1, const ImVec2 &p2, const ImVec2 &p3, ImU32 col, float thickness);
	void(*ImDrawList_AddTriangleFilled)(ImDrawList *_this, const ImVec2 &p1, const ImVec2 &p2, const ImVec2 &p3, ImU32 col);
	void(*ImDrawList_AddCircle)(ImDrawList *_this, const ImVec2 &center, float radius, ImU32 col, int num_segments, float thickness);
	void(*ImDrawList_AddCircleFilled)(ImDrawList *_this, const ImVec2 &center, float radius, ImU32 col, int num_segments);
	void(*ImDrawList_AddNgon)(ImDrawList *_this, const ImVec2 &center, float radius, ImU32 col, int num_segments, float thickness);
	void(*ImDrawList_AddNgonFilled)(ImDrawList *_this, const ImVec2 &center, float radius, ImU32 col, int num_segments);
	void(*ImDrawList_AddText)(ImDrawList *_this, const ImVec2 &pos, ImU32 col, const char *text_begin, const char *text_end);
	void(*ImDrawList_AddText2)(ImDrawList *_this, const imgui_font_18600 *font, float font_size, const ImVec2 &pos, ImU32 col, const char *text_begin, const char *text_end, float wrap_width, const ImVec4 *cpu_fine_clip_rect);
	void(*ImDrawList_AddPolyline)(ImDrawList *_this, const ImVec2 *points, int num_points, ImU32 col, ImDrawFlags flags, float thickness);
	void(*ImDrawList_AddConvexPolyFilled)(ImDrawList *_this, const ImVec2 *points, int num_points, ImU32 col);
	void(*ImDrawList_AddBezierCubic)(ImDrawList *_this, const ImVec2 &p1, const ImVec2 &p2, const ImVec2 &p3, const ImVec2 &p4, ImU32 col, float thickness, int num_segments);
	void(*ImDrawList_AddBezierQuadratic)(ImDrawList *_this, const ImVec2 &p1, const ImVec2 &p2, const ImVec2 &p3, ImU32 col, float thickness, int num_segments);
	void(*ImDrawList_AddImage)(ImDrawList *_this, ImTextureID user_texture_id, const ImVec2 &p_min, const ImVec2 &p_max, const ImVec2 &uv_min, const ImVec2 &uv_max, ImU32 col);
	void(*ImDrawList_AddImageQuad)(ImDrawList *_this, ImTextureID user_texture_id, const ImVec2 &p1, const ImVec2 &p2, const ImVec2 &p3, const ImVec2 &p4, const ImVec2 &uv1, const ImVec2 &uv2, const ImVec2 &uv3, const ImVec2 &uv4, ImU32 col);
	void(*ImDrawList_AddImageRounded)(ImDrawList *_this, ImTextureID user_texture_id, const ImVec2 &p_min, const ImVec2 &p_max, const ImVec2 &uv_min, const ImVec2 &uv_max, ImU32 col, float rounding, ImDrawFlags flags);
	void(*ImDrawList_PathArcTo)(ImDrawList *_this, const ImVec2 &center, float radius, float a_min, float a_max, int num_segments);
	void(*ImDrawList_PathArcToFast)(ImDrawList *_this, const ImVec2 &center, float radius, int a_min_of_12, int a_max_of_12);
	void(*ImDrawList_PathBezierCubicCurveTo)(ImDrawList *_this, const ImVec2 &p2, const ImVec2 &p3, const ImVec2 &p4, int num_segments);
	void(*ImDrawList_PathBezierQuadraticCurveTo)(ImDrawList *_this, const ImVec2 &p2, const ImVec2 &p3, int num_segments);
	void(*ImDrawList_PathRect)(ImDrawList *_this, const ImVec2 &rect_min, const ImVec2 &rect_max, float rounding, ImDrawFlags flags);
	void(*ImDrawList_AddCallback)(ImDrawList *_this, ImDrawCallback callback, void *callback_data);
	void(*ImDrawList_AddDrawCmd)(ImDrawList *_this);
	ImDrawList *(*ImDrawList_CloneOutput)(const ImDrawList *_this);
	void(*ImDrawList_PrimReserve)(ImDrawList *_this, int idx_count, int vtx_count);
	void(*ImDrawList_PrimUnreserve)(ImDrawList *_this, int idx_count, int vtx_count);
	void(*ImDrawList_PrimRect)(ImDrawList *_this, const ImVec2 &a, const ImVec2 &b, ImU32 col);
	void(*ImDrawList_PrimRectUV)(ImDrawList *_this, const ImVec2 &a, const ImVec2 &b, const ImVec2 &uv_a, const ImVec2 &uv_b, ImU32 col);
	void(*ImDrawList_PrimQuadUV)(ImDrawList *_this, const ImVec2 &a, const ImVec2 &b, const ImVec2 &c, const ImVec2 &d, const ImVec2 &uv_a, const ImVec2 &uv_b, const ImVec2 &uv_c, const ImVec2 &uv_d, ImU32 col);
	void(*ConstructImFont)(imgui_font_18600 *_this);
	void(*DestructImFont)(imgui_font_18600 *_this);
	const ImFontGlyph *(*ImFont_FindGlyph)(const imgui_font_18600 *_this, ImWchar c);
	const ImFontGlyph *(*ImFont_FindGlyphNoFallback)(const imgui_font_18600 *_this, ImWchar c);
	ImVec2(*ImFont_CalcTextSizeA)(const imgui_font_18600 *_this, float size, float max_width, float wrap_width, const char *text_begin, const char *text_end, const char **remaining);
	const char *(*ImFont_CalcWordWrapPositionA)(const imgui_font_18600 *_this, float scale, const char *text, const char *text_end, float wrap_width);
	void(*ImFont_RenderChar)(const imgui_font_18600 *_this, ImDrawList *draw_list, float size, ImVec2 pos, ImU32 col, ImWchar c);
	void(*ImFont_RenderText)(const imgui_font_18600 *_this, ImDrawList *draw_list, float size, ImVec2 pos, ImU32 col, const ImVec4 &clip_rect, const char *text_begin, const char *text_end, float wrap_width, bool cpu_fine_clip);

};
