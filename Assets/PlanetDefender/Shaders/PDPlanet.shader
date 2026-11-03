// Real equirectangular surface map wrapped on a lit, spinning sphere with a soft terminator and an atmosphere rim.
// The sphere fills 80% of the quad; the outer band is atmosphere glow. Premultiplied alpha.
Shader "PD/Planet"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _SurfaceMap ("Surface map (equirect)", 2D) = "white" {}
        _AtmoColor ("Atmosphere", Color) = (0.37, 0.7, 1, 1)
        _Spin ("Spin (turns/sec)", Float) = 0.02
        _Tilt ("Axial tilt (rad)", Float) = -0.25
        _Lod ("Texture LOD", Float) = 0
        _Flash ("Hit flash", Range(0, 1)) = 0
        _FlashColor ("Flash color", Color) = (1, 0.2, 0.3, 1)
        _AtmoStrength ("Atmosphere strength", Float) = 1.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Planet"
            HLSLPROGRAM
            #pragma vertex PDVertSprite
            #pragma fragment frag
            #include "PDCommon.hlsl"

            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _SurfaceMap_ST;
                float4 _AtmoColor, _FlashColor;
                float _Spin, _Tilt, _Lod, _Flash, _AtmoStrength;
            CBUFFER_END

            half4 frag(PDVaryings i) : SV_Target
            {
                float2 s = (i.uv - 0.5) * 2.0 / 0.8;
                float r = length(s);
                float aa = max(fwidth(r) * 1.5, 1e-4);
                float3 L = normalize(PD_SUN);
                float lit2D = saturate(dot(normalize(s + 1e-5), normalize(L.xy)) * 0.5 + 0.55);

                // Outer atmosphere halo
                float halo = exp(-max(r - 1.0, 0) * 10.0) * step(1.0 - aa, r) * lit2D * PDFall(1.25, 1.12, r);
                float3 haloColor = _AtmoColor.rgb * halo * 0.9 * _AtmoStrength;
                float haloAlpha = saturate(halo * 0.75);

                float mask = PDFall(1.0, 1.0 - aa, r);
                float3 color = 0;
                if (r < 1.0)
                {
                    float z = sqrt(saturate(1.0 - r * r));
                    float3 N = float3(s, z);
                    // Axial tilt: rotate the sampling frame, not the light.
                    float3 n = N;
                    n.xy = mul(PDRot(_Tilt), n.xy);
                    float lon = atan2(n.x, n.z);
                    float lat = asin(clamp(n.y, -1.0, 1.0));
                    float2 uv = float2(lon / (2.0 * PI) + 0.5 + _Time.y * _Spin, lat / PI + 0.5);
                    float3 albedo = SAMPLE_TEXTURE2D_LOD(_SurfaceMap, sampler_SurfaceMap, uv, _Lod).rgb;
                    albedo = saturate(pow(albedo, 0.95) * 1.12);

                    float ndl = dot(N, L);
                    float day = smoothstep(-0.18, 0.42, ndl);
                    float3 V = float3(0, 0, 1);
                    float spec = pow(saturate(dot(reflect(-L, N), V)), 28.0) * 0.18 * day;
                    color = albedo * (0.035 + day * 1.05) + spec;

                    float fres = pow(1.0 - z, 2.4);
                    color += _AtmoColor.rgb * fres * (0.25 + 0.9 * saturate(ndl + 0.35)) * _AtmoStrength;
                    color = lerp(color, _FlashColor.rgb * 1.6, _Flash * 0.55);
                }
                float3 rgb = color * mask + haloColor * (1.0 - mask);
                float a = saturate(mask + haloAlpha * (1.0 - mask));
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
