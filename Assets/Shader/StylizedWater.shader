// Stylized sea for the stage-select map.
//
// Colour comes from how deep the water is, read from the seabed terrain's heightmap and from
// each island's shallow shelf (WaterSeabed feeds both in): see-through turquoise over the
// shallows, opaque blue in the deep, a foam rim at the shore, shore-wave lines rolling in, and
// sparkle lines on open water. The island shelves aren't in the terrain, so they move with the
// islands (IslandShallows).
// It receives the main light's shadows so the drifting clouds darken it.
//
// Why it doesn't read the camera depth texture like most water: the outline pass draws edges
// from the depth and normals textures, so anything under a water surface that doesn't write
// them shows through as ghost outlines. This water sits at the end of the opaque range with
// its own DepthOnly/DepthNormals passes, so both textures see one flat sea — and that means the
// depth texture holds the water itself, not the seabed. The terrain heightmap is the seabed.
Shader "Carry On/Stylized Water"
{
    Properties
    {
        [Header(Depth colour)]
        _ShallowColor ("Shallow (alpha = see-through)", Color) = (0.36, 0.86, 0.86, 0.45)
        _DeepColor ("Deep", Color) = (0.16, 0.45, 0.86, 1)
        _DepthRange ("Depth to reach Deep (m)", Float) = 5
        _Bands ("Colour bands (0 = smooth)", Float) = 3
        _ShelfDepth ("Island shelf depth (m)", Float) = 0.26

        [Header(Foam)]
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _FoamDepth ("Foam rim depth (m)", Float) = 0.6
        _ShoreWaveRange ("Shore waves reach (m of depth)", Float) = 2.5
        _ShoreWaveSpacing ("Shore wave spacing (m of depth)", Float) = 0.9
        _ShoreWaveSpeed ("Shore wave speed", Float) = 0.35

        [Header(Sparkles)]
        _SparkleColor ("Glint (alpha = strength)", Color) = (1, 1, 1, 0.8)
        _SparkleScale ("Glint cells per metre", Float) = 0.25
        _SparkleSpeed ("Glint speed", Float) = 1
        _SparkleWidth ("Glint size", Range(0.02, 0.4)) = 0.035
        _SparkleCoverage ("Glint coverage", Range(0, 1)) = 0.5

        [Header(Lighting)]
        _ShadowStrength ("Cloud shadow strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        // Geometry+450 keeps the sea inside the opaque range, which is what the depth-normals
        // prepass renders, while still drawing after the islands and seabed it blends over.
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+450" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _DeepColor;
            float _DepthRange;
            float _Bands;
            float _ShelfDepth;
            half4 _FoamColor;
            float _FoamDepth;
            float _ShoreWaveRange;
            float _ShoreWaveSpacing;
            float _ShoreWaveSpeed;
            half4 _SparkleColor;
            float _SparkleScale;
            float _SparkleSpeed;
            float _SparkleWidth;
            float _SparkleCoverage;
            half _ShadowStrength;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Set per renderer by WaterSeabed. Without a seabed every pixel reads as deep.
            TEXTURE2D(_SeabedHeightmap);
            SAMPLER(sampler_SeabedHeightmap);
            float4 _SeabedOrigin;   // xyz = terrain corner, w = 1 when a seabed is bound
            float4 _SeabedSize;     // x = width, y = heightmap value -> metres, z = length, w = heightmap resolution

            // Set per renderer by WaterSeabed from every IslandShallows. Must match its MaxIslands.
            #define MAX_ISLAND_SHALLOWS 32
            float4 _IslandShallows[MAX_ISLAND_SHALLOWS];   // xy = centre (world xz), z = platform radius, w = slope width
            float _IslandShallowCount;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash2(i).x, b = Hash2(i + float2(1, 0)).x;
                float c = Hash2(i + float2(0, 1)).x, d = Hash2(i + float2(1, 1)).x;
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Sun glints: one short horizontal dash per cell, wandering inside it and fading in
            // and out on its own clock, so the open sea twinkles instead of carrying lines.
            float Glints(float2 p, float t, float size)
            {
                float2 n = floor(p), f = frac(p);
                // Antialias width from p itself: r jumps where the cell changes inside a pixel
                // quad, and fwidth(r) there draws the cell grid onto the sea.
                float aa = length(fwidth(p)) * 0.7;
                float glint = 0.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 h = Hash2(n + g);
                    float2 o = 0.5 + 0.35 * sin(t * 0.5 + 6.2831 * h);
                    float2 d = (g + o - f) * float2(0.12, 1.0);
                    float twinkle = saturate(sin(t * 1.7 + h.x * 6.2831) * 1.6 - 0.6);
                    float r = length(d);
                    glint = max(glint, (1.0 - smoothstep(size - aa, size + aa, r)) * twinkle);
                }
                return glint;
            }

            // The highest seabed any island's shelf puts here: a flat platform _ShelfDepth under the
            // surface, then a slope down past the deep colour. Overlapping shelves merge into one.
            float IslandShelfY(float2 xz, float surfaceY)
            {
                // One wobble shared by every shelf, so the edges wander instead of tracing circles.
                float wobble = (ValueNoise(xz * 0.22) - 0.5) * 2.2;
                float seabedY = -1000.0;
                int count = min((int)_IslandShallowCount, MAX_ISLAND_SHALLOWS);
                for (int i = 0; i < count; i++)
                {
                    float4 shelf = _IslandShallows[i];
                    float slope = saturate((length(xz - shelf.xy) + wobble - shelf.z) / max(shelf.w, 0.01));
                    seabedY = max(seabedY, lerp(surfaceY - _ShelfDepth, surfaceY - 8.0, smoothstep(0.0, 1.0, slope)));
                }
                return seabedY;
            }

            float WaterDepth(float3 positionWS)
            {
                float seabedY = -1000.0;
                if (_SeabedOrigin.w > 0.5)
                {
                    // Heightmap samples sit on the terrain's corners, so remap to texel centres.
                    float2 uv = (positionWS.xz - _SeabedOrigin.xz) / _SeabedSize.xz;
                    uv = (uv * (_SeabedSize.w - 1.0) + 0.5) / _SeabedSize.w;
                    seabedY = _SeabedOrigin.y + SAMPLE_TEXTURE2D(_SeabedHeightmap, sampler_SeabedHeightmap, uv).r * _SeabedSize.y;
                }
                seabedY = max(seabedY, IslandShelfY(positionWS.xz, positionWS.y));
                return positionWS.y - seabedY;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = input.positionWS;
                float2 xz = positionWS.xz;
                float time = _Time.y;

                // A little noise on the depth keeps the foam and wave lines from tracing the
                // terrain's contours too neatly.
                float depth = WaterDepth(positionWS) + (ValueNoise(xz * 0.35 + time * 0.2) - 0.5) * 0.5;

                float deepness = saturate(depth / max(_DepthRange, 0.01));
                if (_Bands > 0.5)
                {
                    float banded = deepness * _Bands;
                    float fl = floor(banded);
                    deepness = (fl + smoothstep(0.85, 1.0, banded - fl)) / _Bands;
                }
                half4 color = lerp(_ShallowColor, _DeepColor, deepness);

                float sparkle = Glints(xz * _SparkleScale, time * _SparkleSpeed, _SparkleWidth);
                float patch = ValueNoise(xz * 0.045 + float2(time * 0.04, time * 0.025));
                sparkle *= smoothstep(1.0 - _SparkleCoverage, 1.0 - _SparkleCoverage + 0.12, patch);
                color.rgb = lerp(color.rgb, _SparkleColor.rgb, sparkle * _SparkleColor.a * deepness);

                // Shore waves: thin bands at fixed depths that drift shoreward over time.
                float wavePhase = frac(depth / max(_ShoreWaveSpacing, 0.01) + time * _ShoreWaveSpeed);
                float waveLine = smoothstep(0.78, 0.86, wavePhase) * (1.0 - smoothstep(0.9, 0.98, wavePhase));
                waveLine *= saturate(1.0 - depth / max(_ShoreWaveRange, 0.01));

                float foam = 1.0 - smoothstep(_FoamDepth * 0.75, _FoamDepth, depth);
                float foamAmount = saturate(max(foam, waveLine * 0.85));
                color = lerp(color, _FoamColor, foamAmount);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                color.rgb *= lerp(1.0 - _ShadowStrength, 1.0, mainLight.shadowAttenuation);
                return color;
            }
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half Frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            // One flat, straight-up normal for the whole sea: the outline pass finds no edges
            // inside it, only where an island or the shore breaks the surface.
            void Frag(float4 positionCS : SV_POSITION
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                outNormalWS = half4(0.0, 1.0, 0.0, 0.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }
}
