// Procedural meteor: lumpy rock silhouette, lit pseudo-sphere with bump from layered noise, impact craters,
// a burning leading edge facing the planet, and type-specific detail (iron plates, magma seam, glowing damage
// cracks). Per-instance data comes from the vertex colour so every meteor batches:
//   r = shape seed, g = type / 8, b = hit flash, a = damage (0..1).
Shader "PD/Meteor"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _HeatColor ("Heat", Color) = (1, 0.42, 0.12, 1)
        _HeatIntensity ("Heat intensity", Float) = 2.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Meteor"
            HLSLPROGRAM
            #pragma vertex PDVertSprite
            #pragma fragment frag
            #include "PDCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HeatColor;
                float _HeatIntensity;
            CBUFFER_END
            float2 _PD_PlanetPos;
            float _PD_Freeze;

            // Crater field: returns height contribution and voronoi edge distance (for cracks).
            float2 Craters(float2 p, float seed, float density)
            {
                float2 cell = floor(p), f = frac(p);
                float h = 0, f1 = 8, f2 = 8;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 rnd = PDHash22(cell + g + seed);
                    float2 c = g + rnd * 0.8 + 0.1;
                    float d = length(f - c);
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                    float cr = lerp(0.12, 0.42, PDHash21(cell + g + seed * 1.37)) * step(1.0 - density, rnd.x);
                    float bowl = -PDFall(max(cr, 1e-4), 0.0, d) * 0.55 * step(0.001, cr);
                    float rim = exp(-pow((d - cr) / max(cr * 0.28, 1e-3), 2.0)) * 0.22 * step(0.001, cr);
                    h += bowl + rim;
                }
                return float2(h, f2 - f1);
            }

            half4 frag(PDVaryings i) : SV_Target
            {
                float seed = i.color.r * 97.13 + 3.1;
                int type = (int)round(i.color.g * 8.0);   // 0 normal, 1 fast, 2 armored, 3 splitter, 4 big, 5 shard
                float flash = i.color.b;
                float damage = i.color.a;

                float2 p = (i.uv - 0.5) * 2.0;
                float spin = (frac(seed * 0.37) - 0.5) * (type == 1 ? 2.6 : 1.4);
                float2 q = mul(PDRot(_Time.y * spin + seed), p);
                if (type == 1) q.x *= 1.12;

                // Silhouette
                float a = atan2(q.y, q.x);
                float r = length(q);
                float2 ring = float2(cos(a), sin(a));
                float lumps = PDFbm(ring * 1.6 + seed, 3) - 0.5;
                float edge;
                if (type == 2 || type == 5)
                {
                    float n = type == 2 ? 7.0 : 5.0;
                    float seg = 2.0 * PI / n;
                    float k = fmod(a + PI + seed, seg) - seg * 0.5;
                    edge = 0.74 * cos(seg * 0.5) / cos(k) + lumps * 0.08;
                }
                else if (type == 3) edge = 0.70 + 0.10 * cos(2.0 * a + seed) + lumps * 0.16;
                else edge = 0.72 + lumps * (type == 4 ? 0.26 : 0.24) + (PDNoise(ring * 6.0 + seed) - 0.5) * 0.04;

                float aa = max(fwidth(r) * 1.4, 1e-4);
                float mask = PDFall(edge, edge - aa, r);

                // Heat halo outside the rock, strongest on the side facing the planet.
                float2 toPlanet = normalize(_PD_PlanetPos - i.positionWS.xy + 1e-5);
                float2 pn = p / max(edge, 1e-3);
                float facing = dot(normalize(pn + 1e-5), toPlanet);
                float burn = saturate(facing * 0.85 + 0.15);
                float halo = exp(-max(r - edge, 0) * 9.0) * (1.0 - mask) * burn * (type == 1 ? 1.4 : 1.0) * PDFall(1.0, 0.86, length(p));
                float3 heat = _HeatColor.rgb * _HeatIntensity;
                heat = lerp(heat, float3(0.45, 0.85, 1.6), _PD_Freeze * 0.85);

                float3 color = 0;
                if (mask > 0.001)
                {
                    float2 n2 = q / edge;
                    float z = sqrt(saturate(1.0 - dot(n2, n2)));
                    float density = type == 4 ? 0.85 : type == 2 ? 0.25 : 0.55;
                    float2 cr = Craters(q * (type == 4 ? 3.2 : 2.6) + seed, seed, density);
                    float h = (PDFbm(q * 3.4 + seed, 4) - 0.5) * 0.9 + PDFbm(q * 11.0 + seed * 1.7, 2) * 0.25 + cr.x;
                    // Bump from screen-space derivatives, normalised by p's own derivatives so the sign is
                    // correct on every graphics API (ddy runs top-down on some, bottom-up on others).
                    float dpx = ddx(p.x), dpy = ddy(p.y);
                    float2 g = float2(ddx(h) / (abs(dpx) > 1e-5 ? dpx : 1e-5), ddy(h) / (abs(dpy) > 1e-5 ? dpy : 1e-5));
                    float ang = _Time.y * spin + seed;
                    float3 N = float3(mul(PDRot(-ang), n2) * 1.1, z + 0.15);
                    N.xy -= g * 0.045;
                    N = normalize(N);
                    float3 L = normalize(PD_SUN);
                    float ndl = dot(N, L);
                    float diffuse = saturate(ndl);
                    diffuse = diffuse * diffuse * 0.6 + diffuse * 0.6;

                    float tone = frac(seed * 3.1);
                    float3 rock = lerp(float3(0.26, 0.15, 0.09), float3(0.22, 0.17, 0.14), tone);
                    if (type == 4) rock = float3(0.24, 0.13, 0.08);
                    float3 albedo = rock * lerp(0.55, 1.25, saturate(h + 0.55));
                    albedo *= 0.85 + PDNoise(q * 26.0 + seed) * 0.3;    // grain

                    float spec = 0;
                    if (type == 2)
                    {
                        albedo = lerp(float3(0.36, 0.36, 0.40), float3(0.55, 0.54, 0.60), PDFbm(q * 5.0 + seed, 3));
                        float seg = 2.0 * PI / 7.0;
                        float k = abs(fmod(a + PI + seed, seg) - seg * 0.5);
                        float seam = PDFall(0.035, 0.0, k * r) * smoothstep(0.15, 0.4, r);
                        float inner = PDFall(0.02, 0.0, abs(r - edge * 0.55));
                        albedo *= 1.0 - (seam + inner) * 0.55;
                        spec = pow(saturate(dot(reflect(-L, N), float3(0, 0, 1))), 18.0) * 0.6;
                    }

                    color = albedo * (0.06 + diffuse * 1.1) + spec;
                    color += float3(0.02, 0.03, 0.06) * saturate(-N.x - N.y);   // cool bounce from the void

                    // Leading edge glows as the rock burns in.
                    float lead = pow(saturate(dot(normalize(pn + 1e-5), toPlanet)), 2.0) * smoothstep(0.45, 1.0, length(pn));
                    color += heat * lead * (type == 1 ? 0.9 : 0.55);
                    // Thin warm rim keeps silhouettes readable on the dark field.
                    color += heat * 0.12 * smoothstep(0.82, 1.0, length(n2));

                    if (type == 3)
                    {
                        float seamLine = abs(q.x * 0.95 + (PDFbm(q * 3.0 + seed, 3) - 0.5) * 0.45);
                        float magma = PDFall(0.075, 0.0, seamLine) * PDFall(1.0, 0.7, length(n2));
                        color += float3(3.2, 1.25, 0.28) * magma * (0.75 + 0.25 * sin(_Time.y * 7.0 + seed));
                    }
                    if (damage > 0.01)
                    {
                        float crack = PDFall(0.06 * damage + 0.01, 0.0, cr.y) * step(0.25, PDNoise(q * 4.0 + seed));
                        color += float3(3.0, 1.0, 0.25) * crack * damage;
                    }
                    color = lerp(color, color * float3(0.6, 0.85, 1.2) + float3(0.05, 0.12, 0.2) * smoothstep(0.6, 1.0, length(n2)), _PD_Freeze);
                    color = lerp(color, float3(2.2, 2.2, 2.2), flash * 0.75);
                }
                float3 rgb = color * mask + heat * halo * 0.9;
                float alpha = saturate(mask + halo * 0.55);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
