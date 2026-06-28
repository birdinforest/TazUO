// Puddle.fx — Ground puddle effect for TazUO
// Plan C: reflection UV distortion (pivot-stabilized) is separate from surface decoration
// (shimmer + edge ripple on final color / tint — does not move ReflectionSampler UVs).
// Compile: fxc.exe /T fx_2_0 /O3 /Fo Puddle.fxc Puddle.fx  (developers compile manually; see TazUO/CLAUDE.md)

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
float     PivotStableBand;
float     PivotHorizontalRipple;
float     SurfaceShimmerStrength;
float     EdgeRippleStrength;
float     UseContactMap;

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
sampler ContactMapSampler : register(s2);

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

float SamplePuddleMask(float2 uv)
{
    float subScale = max(MaskSubScale, 1.0f);
    float2 texSize = float2(MaskTileCols * subScale, MaskTileRows * subScale);
    float2 pixelCoord = GetPuddleMaskPixelCoord(uv);
    float2 maskUV = (pixelCoord + 0.5) / texSize;

    if (maskUV.x < 0.0 || maskUV.y < 0.0 || maskUV.x > 1.0 || maskUV.y > 1.0)
        return 0.0;

    return tex2D(HeightMaskSampler, maskUV).r;
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

float ComputeReflectionAlphaPivotFalloff(float2 uv)
{
    float alpha = tex2D(ReflectionSampler, uv).a;
    float alphaAbove = tex2D(ReflectionSampler, uv + float2(0.0, -0.0015)).a;
    float pivotEdge = saturate((alpha - alphaAbove) * 10.0 + alpha * 0.35);
    return saturate(1.0 - pivotEdge * 0.92);
}

float ComputeReflectionDistortFalloff(float2 uv)
{
    if (UseContactMap > 0.5f)
    {
        float contactY = tex2D(ContactMapSampler, uv).r;
        // 1.0 = cleared sentinel (no reflector pivot at this pixel)
        if (contactY >= 0.999f)
            return ComputeReflectionAlphaPivotFalloff(uv);

        float distBelowPivot = max(0.0, uv.y - contactY);
        return smoothstep(0.0, max(PivotStableBand, 0.0001f), distBelowPivot);
    }

    return ComputeReflectionAlphaPivotFalloff(uv);
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

    // --- Reflection UV distortion (pivot-stabilized; does not include surface shimmer) ---
    float wn1 = valueNoise(uv * WaveScale + float2(Time * timeScale * 0.65f, Time * timeScale * 0.30f));
    float wn2 = valueNoise(uv * (WaveScale * 0.8f) + float2(-Time * timeScale * 0.45f, Time * timeScale * 0.55f));
    float2 distortRaw = (float2(wn1, wn2) - 0.5f) * WaveStrength;

    float falloff = ComputeReflectionDistortFalloff(uv);
    float horizRipple = lerp(PivotHorizontalRipple, 1.0f, falloff);
    float2 distort;
    distort.x = distortRaw.x * horizRipple;
    distort.y = distortRaw.y * falloff;

    float2 reflUV = clamp(float2(uv.x + distort.x, uv.y + distort.y), 0.001f, 0.999f);
    float4 reflection = tex2D(ReflectionSampler, reflUV);

    float4 waterTint = float4(0.28f, 0.38f, 0.55f, 1.0f);

    // --- Surface decoration (independent noise; does not affect reflection sampling) ---
    float shimmerNoise = valueNoise(uv * WaveScale * 2.0f + float2(Time * timeScale, Time * timeScale * 0.7f));
    float shimmer = (shimmerNoise - 0.5f) * 2.0f * SurfaceShimmerStrength;

    // Rim band peaks where outerMask transitions (puddle edge)
    float edgeBand = 4.0f * outerMask * (1.0f - outerMask);
    float edgeNoise = valueNoise(uv * (WaveScale * 2.5f) + float2(-Time * timeScale * 0.8f, Time * timeScale * 0.4f));
    float edgeRipple = (edgeNoise - 0.5f) * 2.0f * EdgeRippleStrength * edgeBand;

    float tintMod = 1.0f + shimmer * outerMask * 0.6f;
    float4 modulatedTint = waterTint * tintMod;

    float4 waterColor = lerp(modulatedTint, reflection * modulatedTint, reflection.a * ReflectStrength);

    float4 finalColor;
    finalColor.rgb = waterColor.rgb + shimmer * outerMask + edgeRipple;
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
