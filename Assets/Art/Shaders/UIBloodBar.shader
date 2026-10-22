// A UI shader that draws a health bar as a bubbling, glossy pool of blood.
// Put it on a plain Image with no sprite so UV 0..1 spans the bar. HealthBarUI drives _Fill, _Trail and _Aspect.
Shader "SimpleRPG/UI/Blood Bar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Fill)]
        _Fill ("Fill", Range(0, 1)) = 1
        _Trail ("Damage Trail", Range(0, 1)) = 1
        _Aspect ("Width / Height", Float) = 8

        [Header(Colors)]
        [Gamma] _DeepColor ("Deep Blood", Color) = (0.1, 0.0, 0.012, 1)
        [Gamma] _BloodColor ("Blood", Color) = (0.5, 0.02, 0.04, 1)
        [Gamma] _LightColor ("Lit Blood", Color) = (0.85, 0.09, 0.09, 1)
        [Gamma] _TrailColor ("Trail", Color) = (0.75, 0.2, 0.15, 0.6)
        [Gamma] _ShineColor ("Shine", Color) = (1, 0.85, 0.8, 1)

        [Header(Motion)]
        _FlowSpeed ("Flow Speed", Float) = 0.12
        _WaveHeight ("Surface Wave", Float) = 0.006
        _BubbleSpeed ("Bubble Rise Speed", Float) = 0.18
        _BubbleDensity ("Bubble Density", Range(0, 1)) = 0.6
        _Shine ("Shine Strength", Range(0, 1)) = 0.8

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _Fill;
            float _Trail;
            float _Aspect;
            fixed4 _DeepColor;
            fixed4 _BloodColor;
            fixed4 _LightColor;
            fixed4 _TrailColor;
            fixed4 _ShineColor;
            float _FlowSpeed;
            float _WaveHeight;
            float _BubbleSpeed;
            float _BubbleDensity;
            float _Shine;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float hash11(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            float2 hash22(float2 p)
            {
                float n = sin(dot(p, float2(41.0, 289.0)));
                return frac(float2(262144.0, 32768.0) * n);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash22(i).x;
                float b = hash22(i + float2(1, 0)).x;
                float c = hash22(i + float2(0, 1)).x;
                float d = hash22(i + float2(1, 1)).x;
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.5 * valueNoise(p);
                v += 0.25 * valueNoise(p * 2.03 + 7.1);
                v += 0.125 * valueNoise(p * 4.1 + 3.7);
                return v / 0.875;
            }

            // One layer of rising bubbles. Returns x: soft body, y: rim, z: specular dot.
            float3 bubbleLayer(float2 uv, float cells, float speed, float seed)
            {
                float2 p = float2(uv.x * _Aspect, uv.y) * cells;
                p.y -= _Time.y * speed * cells;
                float2 cell = floor(p) + seed;
                float2 f = frac(p) - 0.5;

                float2 h = hash22(cell);
                float present = step(1.0 - _BubbleDensity, hash11(cell.x * 13.7 + cell.y * 57.3));
                float2 center = (h - 0.5) * 0.3;
                center.x += sin(_Time.y * 1.5 + h.y * 6.28) * 0.06;
                float radius = 0.14 + 0.14 * hash11(cell.x + cell.y * 91.7);

                float2 d = (f - center) / radius;
                float r = length(d);
                float body = smoothstep(1.0, 0.85, r) * present;
                float rim = smoothstep(0.55, 0.95, r) * smoothstep(1.0, 0.9, r) * present;
                float spec = smoothstep(0.32, 0.0, length(d - float2(-0.38, 0.38))) * present;
                return float3(body, rim, spec);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float t = _Time.y;

                // Surface: the leading edge wobbles like a liquid meniscus, but sits flat when full or empty.
                float calm = saturate(_Fill * 12.0) * saturate((1.0 - _Fill) * 12.0);
                float wobble = sin(uv.y * 14.0 + t * 2.6) * 0.6 + sin(uv.y * 31.0 - t * 3.7) * 0.4;
                                float edge = _Fill + wobble * _WaveHeight * calm;

                float soft = 0.004;
                float bodyMask = smoothstep(edge + soft, edge - soft, uv.x);
                float trailEdge = max(_Trail, _Fill);
                float trailMask = smoothstep(trailEdge + soft, trailEdge - soft, uv.x) * (1.0 - bodyMask);

                // Swirling body colour: two noise layers drifting against each other.
                float2 flowUv = float2(uv.x * _Aspect, uv.y * 2.0);
                float n1 = fbm(flowUv * 0.9 + float2(t * _FlowSpeed * 4.0, t * 0.05));
                float n2 = fbm(flowUv * 1.6 + float2(-t * _FlowSpeed * 6.0, 11.3 + t * 0.07));
                float swirl = saturate(((n1 + n2) - 0.6) * 2.0);

                // Vertical shading gives the pool a rounded, tube-like depth.
                float depth = smoothstep(0.0, 0.55, uv.y) * (1.0 - 0.45 * smoothstep(0.6, 1.0, uv.y));
                fixed3 col = lerp(_DeepColor.rgb, _BloodColor.rgb, saturate(depth * 0.6 + swirl * 0.5));
                col = lerp(col, _LightColor.rgb, saturate(swirl * depth));

                // Bubbles: a slow large layer and a faster small one.
                float3 b0 = bubbleLayer(uv, 1.4, _BubbleSpeed, 0.0);
                float3 b1 = bubbleLayer(uv, 2.6, _BubbleSpeed * 1.7, 31.0);
                float body = saturate(b0.x * 0.5 + b1.x * 0.35);
                float rim = saturate(b0.y + b1.y * 0.8);
                float spec = saturate(b0.z * 1.4 + b1.z);
                col = lerp(col, _LightColor.rgb * 0.7, body * 0.45);
                col += (_LightColor.rgb * 0.8 + _ShineColor.rgb * 0.25) * rim * _Shine;
                col = lerp(col, _ShineColor.rgb, spec * _Shine);

                // Glossy highlight band along the upper curve, distorted by the flow, plus a slow glint sweeping across.
                float bandY = 0.74 + (n1 - 0.5) * 0.06;
                float band = smoothstep(0.1, 0.0, abs(uv.y - bandY)) * (0.35 + 0.65 * n2);
                float sweepX = frac(t * 0.07) * (_Aspect + 2.0) - 1.0;
                float glint = smoothstep(0.6, 0.0, abs(uv.x * _Aspect - sweepX)) * smoothstep(0.35, 0.8, uv.y);
                col = lerp(col, _ShineColor.rgb, saturate(band * 0.45 + glint * 0.35) * _Shine);

                // Bright, thin lip where the blood meets the empty part of the bar.
                float lip = smoothstep(0.03, 0.0, edge - uv.x) * bodyMask;
                col += _LightColor.rgb * lip * 0.6;

                fixed4 result = fixed4(col, bodyMask);
                result = lerp(result, fixed4(_TrailColor.rgb, _TrailColor.a), trailMask);
                result.a = max(bodyMask, trailMask * _TrailColor.a);
                // The colours above are authored as sRGB values; the framebuffer encodes linear output, so convert to match the sprites.
                #ifndef UNITY_COLORSPACE_GAMMA
                result.rgb = GammaToLinearSpace(result.rgb);
                #endif
                result *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
