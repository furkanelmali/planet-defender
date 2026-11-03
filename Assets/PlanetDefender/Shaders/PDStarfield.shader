// Deep-space backdrop: three parallax star layers that twinkle and drift, a faint two-tone nebula and the glow of
// a distant sun in the top-left corner (matching the scene's light direction). World-space so scale never stretches it.
Shader "PD/Starfield"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Base ("Base", Color) = (0.006, 0.009, 0.026, 1)
        _NebulaA ("Nebula A", Color) = (0.07, 0.02, 0.12, 1)
        _NebulaB ("Nebula B", Color) = (0.01, 0.07, 0.12, 1)
        _StarBrightness ("Star brightness", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Plane" }
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Starfield"
            HLSLPROGRAM
            #pragma vertex PDVertSprite
            #pragma fragment frag
            #include "PDCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Base, _NebulaA, _NebulaB;
                float _StarBrightness;
            CBUFFER_END

            float Stars(float2 p, float density, float size, float seed)
            {
                float2 cell = floor(p), f = frac(p);
                float2 rnd = PDHash22(cell + seed);
                if (rnd.x > density) return 0;
                float2 c = 0.15 + rnd * 0.7;
                float d = length(f - c);
                float tw = 0.65 + 0.35 * sin(_Time.y * (1.0 + rnd.y * 3.0) + rnd.x * 40.0);
                float b = lerp(0.3, 1.0, PDHash21(cell + seed + 7.0));
                return PDFall(size, 0.0, d) * b * tw;
            }

            half4 frag(PDVaryings i) : SV_Target
            {
                float2 w = i.positionWS.xy;
                float t = _Time.y;
                float3 col = _Base.rgb;
                float n = PDFbm(w * 0.09 + float2(t * 0.004, -t * 0.003), 4);
                float n2 = PDFbm(w * 0.16 + 5.3 - float2(t * 0.006, 0), 3);
                col += _NebulaA.rgb * smoothstep(0.42, 0.85, n) * 0.9;
                col += _NebulaB.rgb * smoothstep(0.45, 0.9, n2) * 0.9;
                float sun = exp(-length(w - float2(-9.0, 10.0)) * 0.22);
                col += float3(0.10, 0.12, 0.22) * sun * 0.35;

                float s = 0;
                s += Stars(w * 1.2 + float2(t * 0.02, t * 0.01), 0.30, 0.06, 1.0) * 0.5;
                s += Stars(w * 0.7 + float2(t * 0.04, t * 0.02), 0.22, 0.055, 2.0) * 0.8;
                s += Stars(w * 0.35 + float2(t * 0.07, t * 0.035), 0.16, 0.05, 3.0) * 1.2;
                float3 starTint = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.9, 0.75), PDNoise(w * 0.5));
                col += starTint * s * _StarBrightness;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
