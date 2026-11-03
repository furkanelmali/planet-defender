// Laser beam on a quad stretched along X: white-hot core, coloured falloff and energy streaks flowing outward.
Shader "PD/Laser"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1, 0.31, 0.64, 1)
        _Intensity ("Intensity", Float) = 3.5
        _Length ("World length", Float) = 30
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Laser"
            HLSLPROGRAM
            #pragma vertex PDVertSprite
            #pragma fragment frag
            #include "PDCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity, _Length;
            CBUFFER_END

            half4 frag(PDVaryings i) : SV_Target
            {
                float y = abs(i.uv.y - 0.5) * 2.0;
                float x = i.uv.x * _Length;
                float core = exp(-y * 9.0);
                float glow = exp(-y * 2.6);
                float streak = PDNoise(float2(x * 1.5 - _Time.y * 40.0, y * 3.0)) * 0.6 + 0.4;
                float start = smoothstep(0.0, 0.6, x);
                float3 col = (float3(1, 1, 1) * core * 1.4 + _Color.rgb * glow * streak) * start;
                return half4(col * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
