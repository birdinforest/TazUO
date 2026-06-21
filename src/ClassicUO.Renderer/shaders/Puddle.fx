// Puddle.fx — Ground puddle effect for TazUO
// Compile: fxc.exe /T fx_2_0 /O3 /Fo Puddle.fxc Puddle.fx

float4x4 MatrixTransform;
float     Time;
float2    PuddleCenterUV;
float     PuddleRadiusU;
float     PuddleRadiusV;
float     Alpha;

sampler ReflectionSampler : register(s0);

float noiseHash(float2 p)
{
    p = frac(p * float2(443.897f, 441.423f));
    p += dot(p, p.yx + 19.19f);
    return frac(p.x * p.y);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0f - 2.0f * f);
    return lerp(
        lerp(noiseHash(i),               noiseHash(i + float2(1, 0)), u.x),
        lerp(noiseHash(i + float2(0, 1)), noiseHash(i + float2(1, 1)), u.x),
        u.y
    );
}

struct VS_INPUT
{
    float2 Position : POSITION0;
    float2 ScreenUV : TEXCOORD0;
};

struct PS_INPUT
{
    float4 Position : POSITION0;
    float2 ScreenUV : TEXCOORD0;
};

PS_INPUT PuddleVS(VS_INPUT IN)
{
    PS_INPUT OUT;
    OUT.Position = mul(float4(IN.Position, 0, 1), MatrixTransform);
    OUT.ScreenUV = IN.ScreenUV;
    return OUT;
}

float4 PuddlePS(PS_INPUT IN) : COLOR0
{
    float2 uv = IN.ScreenUV;

    float2 delta = (uv - PuddleCenterUV) / float2(PuddleRadiusU, PuddleRadiusV);
    float dist = length(delta);

    float sn1 = valueNoise(uv * 9.0f + float2(Time * 0.22f, Time * 0.14f));
    float sn2 = valueNoise(uv * 6.5f + float2(-Time * 0.18f, Time * 0.20f));
    float shapeNoise = lerp(sn1, sn2, 0.5f);
    float outerEdge = 1.0f + (shapeNoise - 0.5f) * 0.32f;

    float outerMask = 1.0f - smoothstep(outerEdge - 0.08f, outerEdge + 0.08f, dist);
    if (outerMask <= 0.002f)
        discard;

    float in1 = valueNoise(uv * 12.0f + float2(Time * 0.20f, -Time * 0.16f));
    float innerEdge = 0.82f + (in1 - 0.5f) * 0.14f;
    float innerMask = 1.0f - smoothstep(innerEdge - 0.06f, innerEdge + 0.06f, dist);

    float edgeMask = saturate(outerMask - innerMask);

    float en = valueNoise(uv * 17.0f + float2(Time * 1.1f, -Time * 0.75f));
    float4 edgeColor = float4(0.72f, 0.88f, 1.0f, 1.0f) * (edgeMask * en * 2.4f);

    float wn1 = valueNoise(uv * 22.0f + float2(Time * 1.6f, Time * 0.85f));
    float wn2 = valueNoise(uv * 18.0f + float2(-Time * 1.3f, Time * 1.4f));
    float2 distort = (float2(wn1, wn2) - 0.5f) * 0.022f;

    float2 reflUV = float2(uv.x + distort.x, PuddleCenterUV.y * 2.0f - uv.y + distort.y);
    reflUV = clamp(reflUV, 0.001f, 0.999f);
    float4 reflection = tex2D(ReflectionSampler, reflUV);

    float2 groundUV = clamp(uv + distort * 0.35f, 0.001f, 0.999f);
    float4 ground = tex2D(ReflectionSampler, groundUV);

    float4 waterTint = float4(0.54f, 0.71f, 0.90f, 1.0f);

    float reflectStrength = 0.42f;
    float4 interior = lerp(ground * waterTint, reflection * waterTint, reflectStrength);

    float4 finalColor;
    finalColor.rgb = interior.rgb * innerMask + edgeColor.rgb;
    finalColor.a = (innerMask * 0.60f + edgeMask * 0.82f) * Alpha;

    return finalColor;
}

technique PuddleTechnique
{
    pass p0
    {
        VertexShader = compile vs_3_0 PuddleVS();
        PixelShader = compile ps_3_0 PuddlePS();
    }
}
