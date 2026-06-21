// Puddle.fx — Ground puddle effect for TazUO
// Compile: fxc.exe /T fx_2_0 /O3 /Fo Puddle.fxc Puddle.fx

float4x4 MatrixTransform;
float     Time;
float2    PuddleCenterUV;
float     PuddleRadiusU;
float     PuddleRadiusV;
float     Alpha;
float     ReflectStrength; // 0 = ground-tint only, 1 = full mirror reflection
float     WaveStrength;    // UV ripple amplitude (typical 0.01–0.04)
float     WaveSpeed;       // ripple animation time multiplier
float     WaveScale;       // ripple noise frequency

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

    // Ellipse distance — normalised so 1.0 = puddle boundary
    float2 delta = (uv - PuddleCenterUV) / float2(PuddleRadiusU, PuddleRadiusV);
    float dist = length(delta);

    // Clean fixed edge: soft fade from 0.82 (fully opaque) to 1.0 (transparent)
    // No animated noise on the boundary — edge does not flow
    float outerMask = 1.0f - smoothstep(0.82f, 1.0f, dist);
    if (outerMask <= 0.002f)
        discard;

    // Slow internal ripple distortion — amplitude/speed/scale are per-puddle parameters
    float timeScale = max(WaveSpeed, 0.001f);
    float wn1 = valueNoise(uv * WaveScale + float2(Time * timeScale * 0.65f, Time * timeScale * 0.30f));
    float wn2 = valueNoise(uv * (WaveScale * 0.8f) + float2(-Time * timeScale * 0.45f, Time * timeScale * 0.55f));
    float2 distort = (float2(wn1, wn2) - 0.5f) * WaveStrength;

    // ReflectionRT contains each sprite flipped downward at its own feet anchor.
    // No centre correction needed — direct UV sample gives correct per-object reflection.
    float2 reflUV = clamp(float2(uv.x + distort.x, uv.y + distort.y), 0.001f, 0.999f);
    float4 reflection = tex2D(ReflectionSampler, reflUV);

    // Dark wet-ground water tint (desaturated blue-gray, matches damp soil)
    float4 waterTint = float4(0.28f, 0.38f, 0.55f, 1.0f);

    // Blend: no reflection → water tint; reflection present → tint-tinted reflection
    float4 waterColor = lerp(waterTint, reflection * waterTint, reflection.a * ReflectStrength);

    // Final: water color inside, smooth fade to transparent at edge
    float4 finalColor;
    finalColor.rgb = waterColor.rgb;
    finalColor.a = outerMask * 0.80f * Alpha;

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
