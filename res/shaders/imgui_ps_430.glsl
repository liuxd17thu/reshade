#version 430

layout(binding = 0) uniform sampler2D s0;

layout(std140, binding = 0) uniform PushConstants
{
	mat4 ortho_projection;
	uint color_space;
	float hdr_overlay_brightness;
	float texture_preview_mode;
	float padding;
};

in vec4 frag_col;
in vec2 frag_tex;

out vec4 col;

void main()
{
	col = texture(s0, frag_tex);
	if (texture_preview_mode == 1.0)
		col.a = 1.0;
	else if (texture_preview_mode == 2.0)
		col = vec4(clamp(col.aaa, 0.0, 1.0), 1.0);
	col *= frag_col; // Blend vertex color and texture
}
