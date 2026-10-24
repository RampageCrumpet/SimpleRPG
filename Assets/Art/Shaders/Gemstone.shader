// Real-time gemstone shader (URP, single forward pass, opaque).
//
// Instead of ray tracing a mesh, each pixel traces one ray per colour channel through an analytic
// "pavilion" (a faceted ellipsoid standing in for the back of the stone), bouncing with total
// internal reflection up to _Bounces times. Because every channel uses its own refractive index
// (Cauchy dispersion), the rays leave the stone in slightly different directions: that is the fire.
// The exit directions sample the reflection probe and are also tested against every light, giving
// narrow coloured glints (the caustic sparkle) that move with the camera.
//
// Cost: 3 short ray traces + 4 cubemap taps + a light loop of 1 pow per channel per light.
// Turning off _DISPERSION traces and samples once (about a third of the cost).
//
// Real values (nD = refractive index, dispersion = nG - nB, Fraunhofer lines) live in
// Assets/Scripts/Editor/GemMaterialPresets.cs. Gem axis is local Z, matching Brilliant_Cut.fbx.
Shader "SimpleRPG/Gemstone"
{
    Properties
    {
        [Header(Optics)]
        _IOR ("Refractive Index (nD)", Range(1.3, 2.7)) = 2.417
        _Dispersion ("Dispersion (nG - nB)", Range(0, 0.12)) = 0.044
        _FireBoost ("Fire Exaggeration", Range(0, 8)) = 3
        [Toggle(_DISPERSION_ON)] _DispersionOn ("Per Channel Dispersion", Float) = 1
        _Bounces ("Internal Bounces", Range(1, 4)) = 3

        [Header(Body)]
        [HDR] _Transmittance ("Transmitted Colour (per stone width)", Color) = (0.97, 0.98, 1, 1)
        _Density ("Absorption Density", Range(0, 6)) = 1
        _AmbientGlow ("Ambient Light Inside", Range(0, 4)) = 1.5

        [Header(Interior Shape)]
        _CenterOS ("Centre (object space)", Vector) = (0, 0, 0.00242, 0)
        _RadiiOS ("Semi Axes (object space)", Vector) = (0.0066, 0.0066, 0.0040, 0)
        _FacetsAround ("Pavilion Facets Around", Range(0, 16)) = 8
        _FacetBands ("Pavilion Facet Bands", Range(0, 6)) = 3

        [Header(Light)]
        _EnvIntensity ("Environment Intensity", Range(0, 8)) = 3
        _EnvBlur ("Environment Blur (mip)", Range(0, 6)) = 1
        _FireIntensity ("Light Fire Intensity", Range(0, 40)) = 8
        _FireSharpness ("Light Fire Sharpness", Range(8, 2048)) = 80
        _SurfaceSparkle ("Surface Sparkle", Range(0, 40)) = 12
        _SurfaceSharpness ("Surface Sparkle Sharpness", Range(16, 4096)) = 1400
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _IOR;
        half _Dispersion;
        half _FireBoost;
        half _Bounces;
        half4 _Transmittance;
        half _Density;
        half _AmbientGlow;
        float4 _CenterOS;
        float4 _RadiiOS;
        half _FacetsAround;
        half _FacetBands;
        half _EnvIntensity;
        half _EnvBlur;
        half _FireIntensity;
        half _FireSharpness;
        half _SurfaceSparkle;
        half _SurfaceSharpness;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _DISPERSION_ON
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.positionOS = i.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            // Colour channel offsets from nD for red 650nm, green 540nm, blue 450nm. Derived from a
            // Cauchy fit n(l) = A + B / l^2 through the published nD and nG - nB values.
            static const float3 kDispersionWeights = float3(-0.157, 0.168, 0.630);

            half3 SampleEnv(float3 dirWS)
            {
                half4 e = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, dirWS, _EnvBlur);
                return DecodeHDREnvironment(e, unity_SpecCube0_HDR);
            }

            // Snaps an outward interior normal to a faceted pavilion: azimuth into _FacetsAround wedges,
            // polar angle into _FacetBands rings. Gem axis is local Z.
            float3 FacetNormal(float3 n)
            {
                if (_FacetsAround < 1.5h) return n;
                float step = 6.2831853 / round(_FacetsAround);
                float az = (floor(atan2(n.y, n.x) / step) + 0.5) * step;
                float bands = max(round(_FacetBands), 1.0);
                float z = (floor(n.z * bands) + 0.5) / bands;
                z = clamp(z, -0.98, 0.98);
                float r = sqrt(1.0 - z * z);
                return float3(r * cos(az), r * sin(az), z);
            }

            // Follows one wavelength through the stone. dirOS starts as the ray just after the entry
            // refraction. Returns the exit direction (object space) and the path length in stone widths.
            float3 TraceInterior(float3 posOS, float3 dirOS, float ior, out float pathLen)
            {
                float3 invR = 1.0 / _RadiiOS.xyz;
                float3 q = (posOS - _CenterOS.xyz) * invR;
                float3 d = dirOS;
                float travelled = 0; // object-space distance, reported in mean stone widths
                float invWidth = 3.0 / (2.0 * (_RadiiOS.x + _RadiiOS.y + _RadiiOS.z));
                int bounces = (int)_Bounces;

                [loop]
                for (int b = 0; b < bounces; b++)
                {
                    float3 qd = d * invR;
                    float a = dot(qd, qd);
                    float bq = dot(q, qd);
                    float c = dot(q, q) - 1.0;
                    float t = (-bq + sqrt(max(bq * bq - a * c, 0.0))) / a;
                    q += qd * t;
                    travelled += t;

                    float3 n = FacetNormal(normalize(q * invR));
                    float3 exitDir = refract(d, -n, ior);
                    if (dot(exitDir, exitDir) > 0.0)
                    {
                        pathLen = travelled * invWidth;
                        return exitDir;
                    }
                    d = reflect(d, n); // total internal reflection
                }

                // Out of bounces and still trapped: let it leak out along its last direction, as if refracted.
                pathLen = travelled * invWidth;
                return d;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Flat facet normal from screen derivatives, so the cut reads even on smooth-shaded meshes.
                float3 dx = ddx(i.positionWS);
                float3 dy = ddy(i.positionWS);
                float3 N = normalize(cross(dx, dy));
                N *= sign(dot(N, i.normalWS) + 1e-5);

                float3 V = normalize(i.positionWS - _WorldSpaceCameraPos); // camera to surface
                float cosI = saturate(dot(-V, N));

                // Real Fresnel from the index of refraction (Schlick, F0 = ((n-1)/(n+1))^2).
                half f0 = (_IOR - 1.0h) / (_IOR + 1.0h);
                f0 *= f0;
                half fresnel = f0 + (1.0h - f0) * pow(1.0h - cosI, 5.0h);

                float3 iors = _IOR + _Dispersion * _FireBoost * kDispersionWeights;
                #ifndef _DISPERSION_ON
                    iors = _IOR.xxx;
                #endif

                // One trace per channel (or just one without dispersion).
                float3 exitWS[3];
                float3 pathLen = 0;
                [unroll]
                for (int ch = 0; ch < 3; ch++)
                {
                    #ifndef _DISPERSION_ON
                        if (ch > 0) { exitWS[ch] = exitWS[0]; pathLen[ch] = pathLen[0]; continue; }
                    #endif
                    float3 refrWS = refract(V, N, 1.0 / iors[ch]);
                    float3 refrOS = normalize(TransformWorldToObjectDir(refrWS, false));
                    float pl;
                    float3 exitOS = TraceInterior(i.positionOS, refrOS, iors[ch], pl);
                    exitWS[ch] = normalize(TransformObjectToWorldDir(exitOS, false));
                    pathLen[ch] = pl;
                }

                // Colour absorption by Beer-Lambert: transmitted colour is defined per stone width.
                half3 tint = pow(max(_Transmittance.rgb, 1e-3h), (_Density * pathLen));

                // Light that exits through the crown towards the eye: probe + ambient + analytic lights.
                half3 inner;
                inner.r = SampleEnv(exitWS[0]).r;
                inner.g = SampleEnv(exitWS[1]).g;
                inner.b = SampleEnv(exitWS[2]).b;
                inner *= _EnvIntensity;
                inner += SampleSH(exitWS[1]) * _AmbientGlow;

                float3 reflWS = reflect(V, N);
                half3 spec = SampleEnv(reflWS) * _EnvIntensity;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                half3 fire = 0;      // coloured glints that come from inside
                half3 sparkle = 0;   // white glints straight off the facets

                Light mainLight = GetMainLight();
                #define ACCUM_LIGHT(L) \
                { \
                    half3 lc = L.color * L.distanceAttenuation; \
                    half3 cosFire = half3(saturate(dot(exitWS[0], L.direction)), \
                                          saturate(dot(exitWS[1], L.direction)), \
                                          saturate(dot(exitWS[2], L.direction))); \
                    fire += lc * pow(cosFire, _FireSharpness); \
                    float3 H = normalize(L.direction - V); \
                    sparkle += lc * pow(saturate(dot(N, H)), _SurfaceSharpness); \
                }
                ACCUM_LIGHT(mainLight)

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint dl = 0; dl < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dl++)
                        {
                            Light dLight = GetAdditionalLight(dl, i.positionWS, 1.0h);
                            ACCUM_LIGHT(dLight)
                        }
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light aLight = GetAdditionalLight(lightIndex, i.positionWS, 1.0h);
                        ACCUM_LIGHT(aLight)
                    LIGHT_LOOP_END
                #endif

                half3 body = (inner + fire * _FireIntensity) * tint;
                half3 color = body * (1.0h - fresnel) + (spec * fresnel) + sparkle * _SurfaceSparkle;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            half4 NormalsFrag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }

    FallBack Off
}
