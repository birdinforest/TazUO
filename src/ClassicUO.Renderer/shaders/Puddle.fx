// Puddle.fx — Ground puddle effect for TazUO
// Compile: fxc.exe /T fx_2_0 /O3 /Fo Puddle.fxc Puddle.fx

float4x4 MatrixTransform;
float     Time;
float2    PuddleCenterUV;
float     PuddleRadiusU;
float     PuddleRadiusV;
float     Alpha;
float     ReflectStrength;
float     WaveStrength;
float     WaveSpeed;
float     WaveScale;

float UseHeightMask; // 1 = sample combined puddle mask (shape + optional height)
float MaskTileStepU;
float MaskTileStepV;
float MaskMinTileX;
float MaskMinTileY;
float MaskTileCols;
float MaskTileRows;
float PuddleTileX;
float PuddleTileY;
float MaskSubScale;

sampler ReflectionSampler : register(s0);
sampler HeightMaskSampler : register(s1);

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

float2 GetPuddleMaskPixelCoord(float2 uv)
{
    float2 duv = uv - PuddleCenterUV;
    float dTileX = (duv.x / MaskTileStepU + duv.y / MaskTileStepV) * 0.5f;
    float dTileY = (duv.y / MaskTileStepV - duv.x / MaskTileStepU) * 0.5f;

    float subScale = max(MaskSubScale, 1.0f);
    float2 tileCoord = float2(PuddleTileX - MaskMinTileX + dTileX, PuddleTileY - MaskMinTileY + dTileY);
    return tileCoord * subScale + 0.5f;
}

float SamplePuddleMaskRawPixel(float2 pixelCoord, float2 texSize)
{
    if (pixelCoord.x < 0.0 || pixelCoord.y < 0.0 || pixelCoord.x >= texSize.x || pixelCoord.y >= texSize.y)
        return 0.0;

    return tex2D(HeightMaskSampler, pixelCoord / texSize).r;
}

// Combined CPU mask: organic outer rim + optional height shoreline (9-tap soften).
float SamplePuddleMask(float2 uv)
{
    float subScale = max(MaskSubScale, 1.0f);
    float2 texSize = float2(MaskTileCols * subScale, MaskTileRows * subScale);
    float2 pixelCoord = GetPuddleMaskPixelCoord(uv);
    float2 texel = float2(1.0, 1.0);

    float m0 = SamplePuddleMaskRawPixel(pixelCoord, texSize);
    float m1 = SamplePuddleMaskRawPixel(pixelCoord + float2(texel.x, 0.0), texSize);
    float m2 = SamplePuddleMaskRawPixel(pixelCoord - float2(texel.x, 0.0), texSize);
    float m3 = SamplePuddleMaskRawPixel(pixelCoord + float2(0.0, texel.y), texSize);
    float m4 = SamplePuddleMaskRawPixel(pixelCoord - float2(0.0, texel.y), texSize);
    float m5 = SamplePuddleMaskRawPixel(pixelCoord + texel, texSize);
    float m6 = SamplePuddleMaskRawPixel(pixelCoord + float2(-texel.x, texel.y), texSize);
    float m7 = SamplePuddleMaskRawPixel(pixelCoord + float2(texel.x, -texel.y), texSize);
    float m8 = SamplePuddleMaskRawPixel(pixelCoord - texel, texSize);

    return (m0 * 2.0 + m1 + m2 + m3 + m4 + m5 + m6 + m7 + m8) / 10.0;
}

// Legacy fallback when no mask texture is bound (old embedded .fxc).
float ComputeOuterMaskFallback(float2 uv)
{
    float2 delta = (uv - PuddleCenterUV) / float2(PuddleRadiusU, PuddleRadiusV);
    float dist = length(delta);
    float angle = atan2(delta.y, delta.x);
    float2 edgeSeed = float2(PuddleTileX * 0.173 + PuddleTileY * 0.291, PuddleTileY * 0.317 - PuddleTileX * 0.109);

    float n1 = valueNoise(float2(angle * 1.55 + edgeSeed.x, edgeSeed.y));
    float n2 = valueNoise(float2(angle * 3.10 + edgeSeed.x + 5.3, edgeSeed.y + 1.9));
    float n3 = valueNoise(float2(angle * 5.20 + edgeSeed.x + 11.1, edgeSeed.y + 4.7));
    float wobble = (n1 * 0.42 + n2 * 0.28 + n3 * 0.18 - 0.5) * 0.24;

    return 1.0 - smoothstep(0.68, 1.12, dist - wobble);
}

float4 PuddlePS(PS_INPUT IN) : COLOR0
{
    float2 uv = IN.ScreenUV;

    float outerMask;
    if (UseHeightMask > 0.5f)
        outerMask = SamplePuddleMask(uv);
    else
        outerMask = ComputeOuterMaskFallback(uv);

    if (outerMask <= 0.002f)
        discard;

    float timeScale = max(WaveSpeed, 0.001f);
    float wn1 = valueNoise(uv * WaveScale + float2(Time * timeScale * 0.65f, Time * timeScale * 0.30f));
    float wn2 = valueNoise(uv * (WaveScale * 0.8f) + float2(-Time * timeScale * 0.45f, Time * timeScale * 0.55f));
    float2 distort = (float2(wn1, wn2) - 0.5f) * WaveStrength;

    float2 reflUV = clamp(float2(uv.x + distort.x, uv.y + distort.y), 0.001f, 0.999f);
    float4 reflection = tex2D(ReflectionSampler, reflUV);

    float4 waterTint = float4(0.28f, 0.38f, 0.55f, 1.0f);
    float4 waterColor = lerp(waterTint, reflection * waterTint, reflection.a * ReflectStrength);

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
