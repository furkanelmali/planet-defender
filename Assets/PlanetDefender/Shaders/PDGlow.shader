// Additive HDR glow for sprites and particles (bolts, halos, reticle, sparks). Vertex colour carries hue/alpha
// (it is 8-bit), _Intensity pushes it past 1 so bloom picks it up.
Shader "PD/Glow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Glow"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local PD_PARTICLE
            #include "PDCommon.hlsl"

            PDVaryings vert(PDAttributes v)
            {
            #ifdef PD_PARTICLE
                return PDVert(v);       // particle colours are plain vertex colours
            #else
                return PDVertSprite(v);
            #endif
            }

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
            CBUFFER_END

            half4 frag(PDVaryings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float a = tex.a * i.color.a;
                return half4(tex.rgb * i.color.rgb * a * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
