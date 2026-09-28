#version 450 core
#extension GL_ARB_shading_language_include : require

layout(set = 0, binding = 0) uniform sampler2D s0;

layout(location = 0) in struct { vec4 col; vec2 tex; } i;
vec4 vcol = i.col;
layout(location = 0) out vec4 col;

layout(push_constant) uniform PushConstants
{
	// Offset from the orthographic projection matrix used in the vertex shader
	layout(offset = 64) uint color_space;
	layout(offset = 68) float hdr_overlay_brightness;
	layout(offset = 72) float texture_preview_mode;
};

#include "imgui_hdr.hlsl"

void main()
{
	switch (color_space)
	{
	case COLOR_SPACE_SCRGB:
		vcol.rgb = to_scrgb(vcol.rgb);
		break;
	case COLOR_SPACE_HDR10_PQ:
		vcol.rgb = to_hdr10_pq(vcol.rgb);
		break;
	case COLOR_SPACE_HDR10_HLG:
		vcol.rgb = to_hdr10_hlg(vcol.rgb);
		break;
	}

	col = texture(s0, i.tex);
	if (texture_preview_mode == 1.0)
		col.a = 1.0;
	else if (texture_preview_mode == 2.0)
		col = vec4(clamp(col.aaa, 0.0, 1.0), 1.0);
	col *= vcol; // Blend vertex color and texture
}
