sampler2D s0 : register(s0);
float4 preview_constants : register(c4); // z = texture preview mode (offset 72)

void main(float4 vpos : VPOS, float4 vcol : COLOR0, float2 uv : TEXCOORD0, out float4 col : COLOR)
{
	col = tex2D(s0, uv);
	if (preview_constants.z == 1.0)
		col.a = 1.0;
	else if (preview_constants.z == 2.0)
		col = float4(saturate(col.aaa), 1.0);
	col *= vcol; // Blend vertex color and texture
}
