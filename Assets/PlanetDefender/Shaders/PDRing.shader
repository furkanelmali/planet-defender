// Saturn's rings seen at a low angle. Drawn twice: the far half behind the planet (_Front = 0) and the near
// half in front of it (_Front = 1). Banded procedurally with the Cassini division.
Shader "PD/PlanetRing"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Front ("Front half", Float) = 1
        _Flatten ("Flatten", Float) = 0.30
        _Inner ("Inner radius", Float) = 0.50
        _Outer ("Outer radius", Float) = 0.98
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Ring"
            HLSLPROGRAM
            #pragma vertex PDVertSprite
            #pragma fragment frag
            #include "PDCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Front, _Flatten, _Inner, _Outer;
            CBUFFER_END

            half4 frag(PDVaryings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float2 e = float2(p.x, p.y / _Flatten);
                float rr = length(e);
                float aa = max(fwidth(rr) * 1.5, 1e-4);
                float band = smoothstep(_Inner - aa, _Inner + aa, rr) * PDFall(_Outer + aa, _Outer - aa, rr);
                float half_ = _Front > 0.5 ? step(p.y, 0.0) : step(0.0, p.y);
                float t = (rr - _Inner) / (_Outer - _Inner);
                float bands = 0.55 + 0.22 * sin(t * 70.0) + 0.15 * sin(t * 23.0 + 1.3) + (PDNoise(float2(t * 140.0, 0.5)) - 0.5) * 0.3;
                float cassini = smoothstep(0.012, 0.03, abs(t - 0.66));
                float density = saturate(bands) * cassini * lerp(0.55, 1.0, smoothstep(0.0, 0.25, t)) * lerp(1.0, 0.5, smoothstep(0.8, 1.0, t));
                float3 col = lerp(float3(0.62, 0.52, 0.38), float3(0.96, 0.88, 0.70), saturate(bands));
                float light = 0.75 + 0.35 * saturate(-p.x * 0.6 + 0.5);
                float a = band * half_ * density * 0.92;
                return half4(col * light * a, a);
            }
            ENDHLSL
        }
    }
}
