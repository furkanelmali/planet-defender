// Energy bubble around the planet: hex cells that shimmer, a bright fresnel rim and a slow sweep. Additive.
Shader "PD/Shield"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Shield"
            HLSLPROGRAM
            #pragma vertex PDVertSprite
            #pragma fragment frag
            #include "PDCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            float Hex(float2 p)
            {
                p.x *= 1.1547;
                p.y += fmod(floor(p.x), 2.0) * 0.5;
                p = abs(frac(p) - 0.5);
                return abs(max(p.x * 1.5 + p.y, p.y * 2.0) - 1.0);
            }

            half4 frag(PDVaryings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                if (r > 1.0) return 0;
                float z = sqrt(saturate(1.0 - r * r));
                float fres = pow(1.0 - z, 3.0);
                float rim = smoothstep(0.86, 0.97, r) * PDFall(1.0, 0.975, r);
                float2 sp = p / max(z, 0.25);
                float hex = PDFall(0.12, 0.0, Hex(sp * 3.2 + _Time.y * 0.15));
                float sweep = pow(saturate(1.0 - abs(frac(_Time.y * 0.35) * 2.6 - 1.3 - p.y)), 6.0);
                float v = rim * 1.2 + fres * 0.45 + hex * (0.08 + fres * 0.5 + sweep * 0.6);
                return half4(i.color.rgb * v * i.color.a * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
