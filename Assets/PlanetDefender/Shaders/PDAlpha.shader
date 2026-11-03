// Alpha-blended unlit sprite/particle (rock debris, smoke). Optional lit tint from the sun direction.
Shader "PD/Alpha"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Alpha"
            HLSLPROGRAM
            #pragma vertex PDVert
            #pragma fragment frag
            #include "PDCommon.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
            CBUFFER_END

            half4 frag(PDVaryings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return half4(tex.rgb * i.color.rgb * _Intensity, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
