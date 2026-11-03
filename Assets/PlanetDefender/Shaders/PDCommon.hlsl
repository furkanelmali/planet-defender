#ifndef PD_COMMON_INCLUDED
#define PD_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct PDAttributes
{
    float4 positionOS : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
};

struct PDVaryings
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
};

PDVaryings PDVert(PDAttributes v)
{
    PDVaryings o;
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.color = v.color;
    o.uv = v.uv;
    return o;
}

// SpriteRenderer colour arrives through unity_SpriteColor in Unity 6 (SRP-batched sprites keep white vertex
// colours), so sprite shaders combine both, exactly like URP's own Sprite-Unlit shader.
PDVaryings PDVertSprite(PDAttributes v)
{
    PDVaryings o = PDVert(v);
    o.color = v.color * unity_SpriteColor;
    return o;
}

// One light for the whole scene: a sun up and to the left, slightly in front.
static const float3 PD_SUN = float3(-0.55, 0.58, 0.60);

float PDHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 PDHash22(float2 p)
{
    float n = PDHash21(p);
    return float2(n, PDHash21(p + n + 17.17));
}

float PDNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = PDHash21(i), b = PDHash21(i + float2(1, 0)), c = PDHash21(i + float2(0, 1)), d = PDHash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float PDFbm(float2 p, int octaves)
{
    float sum = 0, amp = 0.5;
    [unroll(5)] for (int i = 0; i < octaves; i++) { sum += amp * PDNoise(p); p = p * 2.03 + 11.7; amp *= 0.5; }
    return sum;
}

// Falling edge: 1 below lo, 0 above hi. GLSL leaves smoothstep(edge0 >= edge1) undefined, so WebGL never sees
// reversed edges.
float PDFall(float hi, float lo, float x) { return 1.0 - smoothstep(lo, hi, x); }

float2x2 PDRot(float a) { float c = cos(a), s = sin(a); return float2x2(c, -s, s, c); }

#endif
