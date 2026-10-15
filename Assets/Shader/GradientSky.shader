// Stylised three-colour sky: a top colour, a horizon colour and a bottom colour, all set on the
// material so the sky can be retinted in the Inspector.
//
// Assign the material in Lighting ▸ Environment ▸ Skybox Material. The sky also lights the scene
// (ambient comes from the skybox), so press Generate Lighting after changing the colours.
Shader "Carry On/Gradient Sky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.30, 0.36, 0.84, 1)
        _HorizonColor ("Horizon Color", Color) = (1, 0.80, 0.84, 1)
        _BottomColor ("Bottom Color", Color) = (0.70, 0.60, 0.88, 1)
        _HorizonSize ("Horizon Size", Range(0.1, 4)) = 0.55
        _HorizonHeight ("Horizon Height", Range(-0.5, 0.5)) = 0
        _Exposure ("Exposure", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                float _HorizonSize;
                float _HorizonHeight;
                float _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz; // the sky mesh is centred on the camera
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = normalize(input.direction).y - _HorizonHeight;
                // 0 at the horizon, 1 straight up or down; a bigger Horizon Size keeps the horizon colour higher.
                float blend = pow(saturate(abs(height)), _HorizonSize);
                half3 farColor = height >= 0 ? _TopColor.rgb : _BottomColor.rgb;
                return half4(lerp(_HorizonColor.rgb, farColor, blend) * _Exposure, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
