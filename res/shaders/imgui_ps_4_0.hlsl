Texture2D t0 : register(t0);
SamplerState s0 : register(s0);

cbuffer PushConstants : register(b0)
{
	// Offset from the orthographic projection matrix used in the vertex shader
	uint color_space : packoffset(c4.x);
	float hdr_overlay_brightness : packoffset(c4.y);
	float texture_preview_mode : packoffset(c4.z);
};

#include "imgui_hdr.hlsl"

void main(float4 vpos : SV_POSITION, float4 vcol : COLOR0, float2 uv : TEXCOORD0, out float4 col : SV_TARGET)
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

	col = t0.Sample(s0, uv);
	if (texture_preview_mode == 1.0)
		col.a = 1.0;
	else if (texture_preview_mode == 2.0)
		col = float4(saturate(col.aaa), 1.0);
	col *= vcol; // Blend vertex color and texture
}
