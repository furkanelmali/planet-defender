// Gold: a faceted four-point star with bright edges and rotating glints. Deliberately clean and geometric so it
// never reads as rock. Vertex colour r = per-instance seed.
Shader "PD/Gold"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1.7
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Gold"
            HLSLPROGRAM
            #pragma vertex PDVertSprite
            #pragma fragment frag
            #include "PDCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            half4 frag(PDVaryings i) : SV_Target
            {
                float seed = i.color.r * 31.7;
                float2 p = (i.uv - 0.5) * 2.0;
                float a = atan2(p.y, p.x);
                float r = length(p);
                float point4 = pow(abs(cos(2.0 * a)), 2.6);
                float edge = lerp(0.30, 0.74, point4);
                float aa = max(fwidth(r) * 1.4, 1e-4);
                float mask = PDFall(edge, edge - aa, r);

                // Facets: each arm is split along its axis; the facet normal tilts toward its half.
                float arm = round(a / (PI * 0.5)) * (PI * 0.5);
                float side = sign(sin(2.0 * (a - arm)) + 1e-4);
                float fa = arm + side * 0.55;
                float3 N = normalize(float3(cos(fa), sin(fa), 1.25));
                float3 L = normalize(PD_SUN);
                float shade = saturate(dot(N, L) * 0.9 + 0.35);
                float3 deep = float3(0.80, 0.42, 0.05), mid = float3(1.0, 0.78, 0.22), light = float3(1.0, 0.96, 0.72);
                float3 col = lerp(deep, mid, shade);
                col = lerp(col, light, smoothstep(0.75, 1.0, shade));
                float rim = smoothstep(edge - 0.08, edge - aa, r);
                col += light * rim * 0.6;
                float core = exp(-r * 6.0);
                col += light * core * 0.8;

                // Glints sweep across the arms.
                float t = _Time.y * 2.2 + seed;
                float glint = pow(saturate(1.0 - abs(frac(t * 0.25) * 2.4 - 1.2 - (p.x + p.y) * 0.6)), 10.0);
                col += float3(1.2, 1.1, 0.8) * glint * mask;

                float halo = exp(-max(r - edge, 0) * 7.0) * (1.0 - mask) * 0.55 * PDFall(1.0, 0.8, r);
                float3 rgb = col * _Intensity * mask + float3(1.0, 0.72, 0.15) * halo * _Intensity;
                return half4(rgb, saturate(mask + halo * 0.5));
            }
            ENDHLSL
        }
    }
}
