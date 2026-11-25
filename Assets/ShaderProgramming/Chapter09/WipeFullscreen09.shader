Shader "ShaderProgramming/Chapter9/WipeFullscreen09"
{
	Properties
	{
		_WipeColor ("Wipe Color", Color) = (0,0,0,1)
		_EdgeColor ("Edge Color", Color) = (1,0.6,0.1,1)
		_Progress ("Progress", Range(0,1)) = 0
		_EdgeWidth ("Edge Width", Range(0,0.1)) = 0.02
		_Softness ("Softness", Range(0.001,0.3)) = 0.03
		_Mode ("Mode (0=Iris,1=Horizontal)", Float) = 0
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" }

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "WipeFullscreen"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			half4 _WipeColor;
			half4 _EdgeColor;
			float _Progress;
			float _EdgeWidth;
			float _Softness;
			float _Mode;

			half4 Frag(Varyings input) : SV_Target
			{
				float2 uv = input.texcoord;

				// 距離 d を 0〜1 に正規化する
				float d;
				if (_Mode < 0.5)
				{
					// Iris：画面の縦横比を補正した中心からの距離（角で 1）
					float aspect = _ScreenParams.x / _ScreenParams.y;
					float2 p = (uv - 0.5) * float2(aspect, 1.0);
					float halfDiag = 0.5 * length(float2(aspect, 1.0));
					d = length(p) / halfDiag;
				}
				else
				{
					// Horizontal：左から右へ 0〜1
					d = uv.x;
				}

				// 境界 R：Progress 0 で 1（覆わない）、Progress 1 で 0（全部覆う）
				float R = 1.0 - _Progress;

				// 覆う領域（滑らかな境界）
				float mask = smoothstep(R - _Softness, R + _Softness, d);

				// 縁の線：境界付近だけを強める（進行中のみ）
				float active = step(0.001, _Progress) * step(_Progress, 0.999);
				float edge = (1.0 - smoothstep(0.0, _EdgeWidth, abs(d - R))) * active;

				// 元の画像（_BlitTexture は直前までの描画結果）
				half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

				half3 col = lerp(scene, _WipeColor.rgb, mask);
				col = lerp(col, _EdgeColor.rgb, edge);
				return half4(col, 1.0h);
			}
			ENDHLSL
		}
	}
}