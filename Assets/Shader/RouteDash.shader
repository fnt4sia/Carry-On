// Dashed route between two stage-select nodes, for a LineRenderer in Tile texture mode.
//
// The dashes are drawn from the UVs rather than a texture: Tile mode runs uv.x in metres along
// the line, so each dash is a rounded capsule measured in metres with an outline ring, and it
// stays crisp at any zoom. The dashes march slowly toward the far end, which is also the way
// to the next stage. MapRoute tells it the line's width and length.
Shader "Carry On/Route Dash"
{
    Properties
    {
        _FillColor ("Dash", Color) = (1, 0.96, 0.86, 1)
        _EdgeColor ("Dash outline", Color) = (0.13, 0.17, 0.38, 1)
        _DashLength ("Dash length (m)", Float) = 3.4
        _GapLength ("Gap (m)", Float) = 2.4
        _EdgeWidth ("Outline width (share of the dash radius)", Range(0, 0.8)) = 0.3
        _Speed ("March speed (m/s)", Float) = 1.2
        _EndFade ("Fade at each end (m)", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Route"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                half4 _EdgeColor;
                float _DashLength;
                float _GapLength;
                float _EdgeWidth;
                float _Speed;
                float _EndFade;
            CBUFFER_END

            // Set per route by MapRoute.
            float _LineWidth;
            float _RouteLength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float along = input.uv.x;
                float period = _DashLength + _GapLength;
                float local = frac((along - _Time.y * _Speed) / period) * period - period * 0.5;

                // Signed distance to a capsule as long as the dash and as wide as the line.
                float radius = _LineWidth * 0.5;
                float across = (input.uv.y - 0.5) * _LineWidth;
                float halfStraight = max(_DashLength * 0.5 - radius, 0.0);
                float dist = length(float2(max(abs(local) - halfStraight, 0.0), across)) - radius;

                float aa = max(fwidth(dist), 1e-4);
                float shape = 1.0 - smoothstep(-aa, aa, dist);
                float rim = smoothstep(-radius * _EdgeWidth - aa, -radius * _EdgeWidth + aa, dist);
                half3 color = lerp(_FillColor.rgb, _EdgeColor.rgb, rim);

                float fade = saturate(along / max(_EndFade, 0.01))
                           * saturate((_RouteLength - along) / max(_EndFade, 0.01));
                return half4(color, shape * fade);
            }
            ENDHLSL
        }
    }
}
