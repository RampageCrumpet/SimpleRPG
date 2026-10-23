Shader "Hidden/SimpleRPG/InteractableOutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Composite"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _OutlineColor;
            float _OutlineWidth;

            static const float2 Directions[8] =
            {
                float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                float2(0.7071, 0.7071), float2(-0.7071, 0.7071), float2(0.7071, -0.7071), float2(-0.7071, -0.7071)
            };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy;

                // Pixels covered by the object keep their own colour; the outline is only the ring outside it.
                if (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r > 0.5)
                {
                    return 0;
                }

                float nearest = 0;
                [loop] for (int d = 0; d < 8; d++)
                {
                    [loop] for (int s = 1; s <= (int)_OutlineWidth; s++)
                    {
                        nearest = max(nearest, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + Directions[d] * texel * s).r);
                    }
                }

                return half4(_OutlineColor.rgb, _OutlineColor.a * step(0.5, nearest));
            }
            ENDHLSL
        }
    }
}
