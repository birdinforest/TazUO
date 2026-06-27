// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO;
using ClassicUO.Renderer;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game
{
    internal sealed class PuddleRenderer : IDisposable
    {
        private const int MAX_PUDDLES = 64;
        private const int VERTS_PER_PUDDLE = 4;
        private const int TRIS_PER_PUDDLE = 2;
        private const float ISO_TILE_WIDTH = 22f;
        /// <summary>Height-mask texels per map tile — higher = smoother organic shorelines.</summary>
        private const int MASK_SUBPIXELS_PER_TILE = 8;
        /// <summary>Draw quad padding so outward organic lobes are not clipped.</summary>
        private const float OUTER_SHAPE_QUAD_PAD = 1.28f;
        /// <summary>Screen AABB half-extent multiplier for iso tile metric (|dTX±dTY| max = R·√5).</summary>
        private const float ISO_SCREEN_AABB = 2.236067977f;
        private const float OUTER_SHAPE_WOBBLE = 0.24f;
        private const float OUTER_SHAPE_MICRO = 0.07f;
        private const float OUTER_SHAPE_FADE_IN = 0.08f;
        private const float OUTER_SHAPE_FADE_OUT = 0.10f;

        private readonly PuddleEffect? _effect;
        private readonly VertexBuffer _vb;
        private readonly IndexBuffer _ib;
        private readonly PuddleVertex[] _verts = new PuddleVertex[MAX_PUDDLES * VERTS_PER_PUDDLE];

        public RenderTarget2D? ReflectionRT { get; private set; }
        public RenderTarget2D? ContactRT { get; private set; }
        public bool SupportsContactMap => _effect?.SupportsContactMap == true && ContactRT != null;

        public PuddleRenderer(GraphicsDevice gd)
        {
            if (!PuddleEffect.TryCreate(gd, out _effect))
            {
                // Detailed diagnostics are logged by PuddleEffect.TryCreate.
            }
            _vb = new VertexBuffer(
                gd,
                PuddleVertex.VertexDeclaration,
                MAX_PUDDLES * VERTS_PER_PUDDLE,
                BufferUsage.WriteOnly
            );
            _ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, MAX_PUDDLES * 6, BufferUsage.WriteOnly);
            _ib.SetData(BuildIndexData(MAX_PUDDLES));
        }

        /// <summary>
        /// Switches the active render target to ReflectionRT and clears it to transparent.
        /// Call before rendering the object-space reflection pass.
        /// </summary>
        public void BeginReflectionTarget(GraphicsDevice gd, int rtW, int rtH)
        {
            EnsureReflectionTargets(gd, rtW, rtH);
            gd.SetRenderTarget(ReflectionRT);
            gd.Clear(Color.Transparent);
        }

        /// <summary>
        /// Clears and binds ContactRT for the feet-pivot contact map pass.
        /// Cleared to 1.0 (sentinel: no pivot). Min-blended writes store the topmost feet line per pixel.
        /// </summary>
        public void BeginContactTarget(GraphicsDevice gd, int rtW, int rtH)
        {
            EnsureReflectionTargets(gd, rtW, rtH);
            gd.SetRenderTarget(ContactRT);
            gd.Clear(new Color(1f, 0f, 0f, 0f));
        }

        private void EnsureReflectionTargets(GraphicsDevice gd, int rtW, int rtH)
        {
            if (
                ReflectionRT == null
                || ReflectionRT.IsDisposed
                || ReflectionRT.Width != rtW
                || ReflectionRT.Height != rtH
            )
            {
                ReflectionRT?.Dispose();
                ReflectionRT = new RenderTarget2D(gd, rtW, rtH, false, SurfaceFormat.Color, DepthFormat.None);
            }

            if (
                ContactRT == null
                || ContactRT.IsDisposed
                || ContactRT.Width != rtW
                || ContactRT.Height != rtH
            )
            {
                ContactRT?.Dispose();
                ContactRT = new RenderTarget2D(gd, rtW, rtH, false, SurfaceFormat.Color, DepthFormat.None);
            }
        }

        /// <summary>
        /// Releases the ReflectionRT render target binding.
        /// GameScene restores _world_render_target immediately after.
        /// </summary>
        public void EndReflectionTarget(GraphicsDevice gd)
        {
            gd.SetRenderTarget(null);
        }

        public void EndContactTarget(GraphicsDevice gd) => EndReflectionTarget(gd);

        public void Draw(
            GraphicsDevice gd,
            IReadOnlyList<PuddleRegion> regions,
            ref Matrix worldRTMatrix,
            int rtW,
            int rtH,
            float elapsedSeconds,
            int cameraOffsetX,
            int cameraOffsetY,
            World world
        )
        {
            if (_effect == null || regions.Count == 0 || ReflectionRT == null)
            {
                return;
            }

            Matrix.CreateOrthographicOffCenter(0f, rtW, rtH, 0f, short.MinValue, short.MaxValue, out Matrix ortho);
            Matrix.Multiply(ref worldRTMatrix, ref ortho, out Matrix fullTransform);

            float tx = worldRTMatrix.M41;
            float ty = worldRTMatrix.M42;

            BlendState prevBlend = gd.BlendState;
            DepthStencilState prevDepth = gd.DepthStencilState;
            RasterizerState prevRaster = gd.RasterizerState;
            VertexBufferBinding[] prevVB = gd.GetVertexBuffers();
            IndexBuffer prevIB = gd.Indices;
            Texture prevTex0 = gd.Textures[0];
            SamplerState prevSamp0 = gd.SamplerStates[0];

            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.Textures[0] = ReflectionRT;
            gd.SamplerStates[0] = SamplerState.LinearClamp;
            if (_effect.SupportsContactMap && ContactRT != null)
            {
                gd.Textures[2] = ContactRT;
                gd.SamplerStates[2] = SamplerState.PointClamp;
            }
            gd.SetVertexBuffer(_vb);
            gd.Indices = _ib;

            int count = Math.Min(regions.Count, MAX_PUDDLES);

            for (int i = 0; i < count; i++)
            {
                PuddleRegion r = regions[i];

                if (r.MaskDirty || r.HeightMask == null)
                {
                    RebuildPuddleMask(gd, r, world);
                }

                if (r.UseHeightMask)
                {
                    r.TileZ = r.MinWaterZ;
                }

                // Match GameObject.UpdateRealScreenPosition / ProjectileDebugVisualizer.
                float cx = ((r.TileX - r.TileY) * ISO_TILE_WIDTH) - cameraOffsetX - ISO_TILE_WIDTH;
                float cy = ((r.TileX + r.TileY) * ISO_TILE_WIDTH - (r.TileZ << 2)) - cameraOffsetY - ISO_TILE_WIDTH;

                float halfExtent = ComputeIsoScreenHalfExtent(r.Radius);
                float hw = halfExtent;
                float hh = halfExtent;

                BuildQuadVerts(i, cx, cy, hw, hh, tx, ty, rtW, rtH);
                _vb.SetData(_verts, i * VERTS_PER_PUDDLE, VERTS_PER_PUDDLE);

                _effect.MatrixTransform.SetValue(fullTransform);
                _effect.Time.SetValue(elapsedSeconds);
                _effect.PuddleCenterUV.SetValue(new Vector2((cx + tx) / rtW, (cy + ty) / rtH));
                _effect.PuddleRadiusU.SetValue(hw / rtW);
                _effect.PuddleRadiusV.SetValue(hh / rtH);
                _effect.Alpha.SetValue(r.Alpha);
                _effect.ReflectStrength.SetValue(r.ReflectStrength);
                _effect.WaveStrength.SetValue(r.WaveStrength);
                _effect.WaveSpeed.SetValue(r.WaveSpeed);
                _effect.WaveScale.SetValue(r.WaveScale);
                _effect.PivotStableBand?.SetValue(r.PivotStableBand);
                _effect.PivotHorizontalRipple?.SetValue(r.PivotHorizontalRipple);
                _effect.UseContactMap?.SetValue(
                    _effect.SupportsContactMap && ContactRT != null ? 1f : 0f
                );

                // Tile seed for stable organic outer edge (independent of height mask).
                _effect.PuddleTileX?.SetValue(r.TileX);
                _effect.PuddleTileY?.SetValue(r.TileY);

                bool bindPuddleMask =
                    r.HeightMask != null
                    && r.MaskTileCols > 0
                    && r.MaskTileRows > 0
                    && _effect.SupportsHeightMask;

                if (bindPuddleMask)
                {
                    gd.Textures[1] = r.HeightMask;
                    gd.SamplerStates[1] = SamplerState.PointClamp;
                    _effect.UseHeightMask!.SetValue(1f);
                    _effect.MaskTileStepU!.SetValue(ISO_TILE_WIDTH / rtW);
                    _effect.MaskTileStepV!.SetValue(ISO_TILE_WIDTH / rtH);
                    _effect.MaskMinTileX!.SetValue(r.MaskMinTileX);
                    _effect.MaskMinTileY!.SetValue(r.MaskMinTileY);
                    _effect.MaskTileCols!.SetValue(r.MaskTileCols);
                    _effect.MaskTileRows!.SetValue(r.MaskTileRows);
                    _effect.MaskSubScale?.SetValue(r.MaskSubScale);
                }
                else if (_effect.SupportsHeightMask)
                {
                    gd.Textures[1] = null;
                    _effect.UseHeightMask!.SetValue(0f);
                }

                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    gd.Textures[0] = ReflectionRT;
                    if (_effect.SupportsContactMap && ContactRT != null)
                    {
                        gd.Textures[2] = ContactRT;
                        gd.SamplerStates[2] = SamplerState.PointClamp;
                    }

                    if (bindPuddleMask)
                    {
                        gd.Textures[1] = r.HeightMask;
                        gd.SamplerStates[1] = SamplerState.PointClamp;
                    }

                    pass.Apply();
                    gd.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        i * VERTS_PER_PUDDLE,
                        0,
                        VERTS_PER_PUDDLE,
                        i * 6,
                        TRIS_PER_PUDDLE
                    );
                }
            }

            gd.BlendState = prevBlend;
            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.Textures[0] = prevTex0;
            gd.SamplerStates[0] = prevSamp0;
            RestoreSharedHueSamplerBindings(gd);

            if (prevVB != null && prevVB.Length > 0)
            {
                gd.SetVertexBuffers(prevVB);
            }

            gd.Indices = prevIB;
        }

        /// <summary>
        /// Puddle.fx reuses global sampler slots s1/s2; restore hue/light lookups for world batcher and weather.
        /// </summary>
        private static void RestoreSharedHueSamplerBindings(GraphicsDevice gd)
        {
            UltimaOnline uo = Client.Game?.UO;
            if (uo == null)
            {
                return;
            }

            if (uo.HueSamplerTexture0 != null && !uo.HueSamplerTexture0.IsDisposed)
            {
                gd.Textures[1] = uo.HueSamplerTexture0;
            }

            if (uo.HueSamplerTexture1 != null && !uo.HueSamplerTexture1.IsDisposed)
            {
                gd.Textures[2] = uo.HueSamplerTexture1;
            }

            gd.SamplerStates[1] = SamplerState.PointClamp;
            gd.SamplerStates[2] = SamplerState.PointClamp;
        }

        internal static void RebuildPuddleMask(GraphicsDevice gd, PuddleRegion region, World world)
        {
            float shapeTileRadius = ComputeShapeTileRadius(region.Radius);
            ComputeMaskTileMargins(shapeTileRadius, out int marginX, out int marginY);
            int minTX = region.TileX - marginX;
            int minTY = region.TileY - marginY;
            int maxTX = region.TileX + marginX;
            int maxTY = region.TileY + marginY;
            int cols = maxTX - minTX + 1;
            int rows = maxTY - minTY + 1;

            var tileWet = new float[cols * rows];
            sbyte minZ = sbyte.MaxValue;
            bool hasHeightClip = region.UseHeightMask && world.Map != null;

            for (int dy = 0; dy < rows; dy++)
            {
                for (int dx = 0; dx < cols; dx++)
                {
                    int tx = minTX + dx;
                    int ty = minTY + dy;
                    int index = dy * cols + dx;

                    float dTileX = tx - region.TileX;
                    float dTileY = ty - region.TileY;
                    float isoDist = MathF.Sqrt(dTileX * dTileX + (dTileY * 0.5f) * (dTileY * 0.5f));

                    if (isoDist > shapeTileRadius + 0.5f)
                    {
                        tileWet[index] = 0f;
                        continue;
                    }

                    if (hasHeightClip)
                    {
                        sbyte groundZ = world.Map!.GetTileZ(tx, ty);

                        if (groundZ < minZ)
                        {
                            minZ = groundZ;
                        }

                        tileWet[index] = groundZ < region.MaxWaterZ ? 1f : 0f;
                    }
                    else
                    {
                        tileWet[index] = 1f;
                    }
                }
            }

            if (hasHeightClip)
            {
                region.MinWaterZ = minZ == sbyte.MaxValue ? (sbyte)0 : minZ;
            }

            region.MaskSubScale = MASK_SUBPIXELS_PER_TILE;

            int outW = cols * MASK_SUBPIXELS_PER_TILE;
            int outH = rows * MASK_SUBPIXELS_PER_TILE;
            var combined = new float[outW * outH];
            var shape = new float[outW * outH];
            BuildOuterShapeMask(
                region,
                minTX,
                minTY,
                region.Radius / ISO_TILE_WIDTH,
                shape,
                outW,
                outH
            );

            if (!hasHeightClip)
            {
                Array.Copy(shape, combined, shape.Length);
            }
            else
            {
                var heightAlpha = new float[outW * outH];
                BuildHeightAlphaMask(tileWet, cols, rows, heightAlpha, outW, outH);
                BlurHeightMask3x3(heightAlpha, outW, outH);

                for (int i = 0; i < combined.Length; i++)
                {
                    combined[i] = shape[i] * heightAlpha[i];
                }

                SoftenMaskEdges(combined, outW, outH, 0.35f);
            }

            var pixels = new Color[outW * outH];
            for (int i = 0; i < combined.Length; i++)
            {
                byte v = (byte)Math.Clamp((int)(combined[i] * 255f), 0, 255);
                pixels[i] = new Color(v, v, v, v);
            }

            region.HeightMask?.Dispose();
            var tex = new Texture2D(gd, outW, outH, false, SurfaceFormat.Color);
            tex.SetData(pixels);
            region.HeightMask = tex;
            region.SetMaskBounds(minTX, minTY, cols, rows);
            region.MaskDirty = false;
        }

        private static float ComputeShapeEdgeScale()
        {
            return OUTER_SHAPE_QUAD_PAD
                * (1f + OUTER_SHAPE_WOBBLE + OUTER_SHAPE_MICRO + OUTER_SHAPE_FADE_OUT);
        }

        private static float ComputeShapeTileRadius(float radiusPixels)
        {
            return radiusPixels / ISO_TILE_WIDTH * ComputeShapeEdgeScale();
        }

        private static float ComputeIsoScreenHalfExtent(float radiusPixels)
        {
            return radiusPixels * ComputeShapeEdgeScale() * ISO_SCREEN_AABB;
        }

        private static void ComputeMaskTileMargins(float shapeTileRadius, out int marginX, out int marginY)
        {
            marginX = Math.Max(2, (int)Math.Ceiling(shapeTileRadius) + 2);
            marginY = Math.Max(2, (int)Math.Ceiling(shapeTileRadius * 2f) + 2);
        }

        private static void BuildOuterShapeMask(
            PuddleRegion region,
            int minTX,
            int minTY,
            float tileRadius,
            float[] output,
            int outW,
            int outH
        )
        {
            const int sub = MASK_SUBPIXELS_PER_TILE;

            for (int py = 0; py < outH; py++)
            {
                for (int px = 0; px < outW; px++)
                {
                    float worldTileX = minTX + (px + 0.5f) / sub;
                    float worldTileY = minTY + (py + 0.5f) / sub;
                    float dTileX = worldTileX - region.TileX;
                    float dTileY = worldTileY - region.TileY;
                    float isoDist = MathF.Sqrt(dTileX * dTileX + (dTileY * 0.5f) * (dTileY * 0.5f));
                    float dist = isoDist / tileRadius;

                    float angle = MathF.Atan2(dTileY, dTileX);
                    float wobble = ComputeOuterShapeWobble(angle, region.TileX, region.TileY, worldTileX, worldTileY);

                    float edgeProximity = SmoothStep(0.72f, 1.08f, dist);
                    float micro =
                        (MaskHash(px * 5 + region.TileX, py * 5 + region.TileY) - 0.5f)
                        * OUTER_SHAPE_MICRO
                        * edgeProximity;
                    float boundary = 1.0f + wobble + micro;

                    output[py * outW + px] = 1.0f - SmoothStep(
                        boundary - OUTER_SHAPE_FADE_IN,
                        boundary + OUTER_SHAPE_FADE_OUT,
                        dist
                    );
                }
            }
        }

        private static void BuildHeightAlphaMask(
            float[] tileWet,
            int cols,
            int rows,
            float[] output,
            int outW,
            int outH
        )
        {
            const int sub = MASK_SUBPIXELS_PER_TILE;

            for (int py = 0; py < outH; py++)
            {
                for (int px = 0; px < outW; px++)
                {
                    float tileX = (px + 0.5f) / sub;
                    float tileY = (py + 0.5f) / sub;
                    float wet = SampleTileWetBilinear(tileWet, cols, rows, tileX, tileY);

                    int ix = Math.Clamp((int)MathF.Floor(tileX), 0, cols - 1);
                    int iy = Math.Clamp((int)MathF.Floor(tileY), 0, rows - 1);

                    if (!IsHeightMaskBoundaryTile(tileWet, cols, rows, ix, iy))
                    {
                        output[py * outW + px] = wet >= 0.5f ? 1f : 0f;
                        continue;
                    }

                    float n1 = MaskHash(px, py);
                    float n2 = MaskHash(px + 137, py + 419);
                    float n3 = MaskHash(px * 3 + 91, py * 3 + 47);
                    float noise = n1 * 0.5f + n2 * 0.3f + n3 * 0.2f;

                    float fx = tileX - MathF.Floor(tileX);
                    float fy = tileY - MathF.Floor(tileY);
                    float ripple = MathF.Sin((fx + fy) * MathF.PI * 2.5f + noise * 6.28f) * 0.06f;
                    float warped = wet + (noise - 0.5f) * 0.52f + ripple;
                    output[py * outW + px] = SmoothStep(0.36f, 0.64f, warped);
                }
            }
        }

        private static float ComputeOuterShapeWobble(
            float angle,
            int tileX,
            int tileY,
            float worldTileX,
            float worldTileY
        )
        {
            float seedX = tileX * 0.173f + tileY * 0.291f;
            float seedY = tileY * 0.317f - tileX * 0.109f;

            float n1 = SampleAngleNoise(angle, seedX, seedY, 1.45f);
            float n2 = SampleAngleNoise(angle, seedX + 5.3f, seedY + 1.9f, 2.85f);
            float n3 = SampleAngleNoise(angle, seedX + 11.1f, seedY + 4.7f, 4.75f);
            float n4 = SampleAngleNoise(angle, seedX + 17.7f, seedY + 9.3f, 7.25f);

            float angular = (n1 * 0.42f + n2 * 0.28f + n3 * 0.18f + n4 * 0.12f - 0.5f) * OUTER_SHAPE_WOBBLE;

            float radialSeed = worldTileX * 0.61f + worldTileY * 0.47f;
            float radial = (valueNoise1D(radialSeed * 3.7f + seedX) - 0.5f) * (OUTER_SHAPE_WOBBLE * 0.35f);

            return angular + radial;
        }

        private static float valueNoise1D(float x)
        {
            float i = MathF.Floor(x);
            float f = x - i;
            float u = f * f * (3f - 2f * f);
            float h0 = MaskHash((int)i, 0);
            float h1 = MaskHash((int)i + 1, 0);
            return h0 + (h1 - h0) * u;
        }

        private static void SoftenMaskEdges(float[] data, int width, int height, float blend)
        {
            var temp = new float[data.Length];
            Array.Copy(data, temp, data.Length);
            blend = Math.Clamp(blend, 0f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    float weight = 0f;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int sx = x + kx;
                            int sy = y + ky;

                            if (sx < 0 || sy < 0 || sx >= width || sy >= height)
                            {
                                continue;
                            }

                            float w = kx == 0 && ky == 0 ? 2f : 1f;
                            sum += temp[sy * width + sx] * w;
                            weight += w;
                        }
                    }

                    float blurred = sum / weight;
                    int idx = y * width + x;
                    data[idx] = temp[idx] + (blurred - temp[idx]) * blend;
                }
            }
        }

        private static float SampleAngleNoise(float angle, float seedX, float seedY, float frequency)
        {
            float x = angle * frequency + seedX;
            float i = MathF.Floor(x);
            float f = x - i;
            float u = f * f * (3f - 2f * f);
            int iy = (int)(seedY * 100f);
            float h0 = MaskHash((int)i, iy);
            float h1 = MaskHash((int)i + 1, iy);
            return h0 + (h1 - h0) * u;
        }

        private static bool IsHeightMaskBoundaryTile(float[] tileWet, int cols, int rows, int x, int y)
        {
            bool center = tileWet[y * cols + x] >= 0.5f;

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    int nx = x + ox;
                    int ny = y + oy;

                    if (nx < 0 || ny < 0 || nx >= cols || ny >= rows)
                    {
                        continue;
                    }

                    if ((tileWet[ny * cols + nx] >= 0.5f) != center)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float SampleTileWetBilinear(
            float[] tileWet,
            int cols,
            int rows,
            float tileX,
            float tileY
        )
        {
            int x0 = (int)MathF.Floor(tileX);
            int y0 = (int)MathF.Floor(tileY);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            float fx = tileX - x0;
            float fy = tileY - y0;

            float v00 = GetTileWet(tileWet, cols, rows, x0, y0);
            float v10 = GetTileWet(tileWet, cols, rows, x1, y0);
            float v01 = GetTileWet(tileWet, cols, rows, x0, y1);
            float v11 = GetTileWet(tileWet, cols, rows, x1, y1);

            float v0 = v00 + (v10 - v00) * fx;
            float v1 = v01 + (v11 - v01) * fx;
            return v0 + (v1 - v0) * fy;
        }

        private static float GetTileWet(float[] tileWet, int cols, int rows, int x, int y)
        {
            if (x < 0 || y < 0 || x >= cols || y >= rows)
            {
                return 0f;
            }

            return tileWet[y * cols + x];
        }

        private static void BlurHeightMask3x3(float[] data, int width, int height)
        {
            var temp = new float[data.Length];
            Array.Copy(data, temp, data.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    float weight = 0f;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int sx = x + kx;
                            int sy = y + ky;

                            if (sx < 0 || sy < 0 || sx >= width || sy >= height)
                            {
                                continue;
                            }

                            float w = kx == 0 && ky == 0 ? 2f : 1f;
                            sum += temp[sy * width + sx] * w;
                            weight += w;
                        }
                    }

                    data[y * width + x] = sum / weight;
                }
            }
        }

        private static float MaskHash(int x, int y)
        {
            uint n = (uint)(x * 374761393 + y * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0xFFFF) / 65535f;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        internal static sbyte ComputeMinGroundZ(PuddleRegion region, World world)
        {
            if (world.Map == null)
            {
                return 0;
            }

            int isoR = Math.Max(2, (int)Math.Ceiling(region.Radius / ISO_TILE_WIDTH) * 2 + 2);
            int minTX = region.TileX - isoR;
            int minTY = region.TileY - isoR;
            int maxTX = region.TileX + isoR;
            int maxTY = region.TileY + isoR;
            sbyte minZ = sbyte.MaxValue;
            float tileRadius = region.Radius / ISO_TILE_WIDTH;

            for (int ty = minTY; ty <= maxTY; ty++)
            {
                for (int tx = minTX; tx <= maxTX; tx++)
                {
                    float dTileX = tx - region.TileX;
                    float dTileY = ty - region.TileY;
                    float isoDist = MathF.Sqrt(dTileX * dTileX + (dTileY * 0.5f) * (dTileY * 0.5f));

                    if (isoDist > tileRadius + 0.5f)
                    {
                        continue;
                    }

                    sbyte groundZ = world.Map.GetTileZ(tx, ty);

                    if (groundZ < minZ)
                    {
                        minZ = groundZ;
                    }
                }
            }

            return minZ == sbyte.MaxValue ? (sbyte)0 : minZ;
        }

        private void BuildQuadVerts(
            int idx,
            float cx,
            float cy,
            float hw,
            float hh,
            float tx,
            float ty,
            float rtW,
            float rtH
        )
        {
            int b = idx * VERTS_PER_PUDDLE;
            _verts[b] = MakeVertex(cx - hw, cy - hh, tx, ty, rtW, rtH);
            _verts[b + 1] = MakeVertex(cx + hw, cy - hh, tx, ty, rtW, rtH);
            _verts[b + 2] = MakeVertex(cx - hw, cy + hh, tx, ty, rtW, rtH);
            _verts[b + 3] = MakeVertex(cx + hw, cy + hh, tx, ty, rtW, rtH);
        }

        private static PuddleVertex MakeVertex(float x, float y, float tx, float ty, float rtW, float rtH)
        {
            return new PuddleVertex
            {
                Position = new Vector2(x, y),
                ScreenUV = new Vector2((x + tx) / rtW, (y + ty) / rtH)
            };
        }

        private static short[] BuildIndexData(int maxSprites)
        {
            short[] idx = new short[maxSprites * 6];

            for (int i = 0; i < maxSprites; i++)
            {
                short v = (short)(i * 4);
                int e = i * 6;
                idx[e] = v;
                idx[e + 1] = (short)(v + 1);
                idx[e + 2] = (short)(v + 2);
                idx[e + 3] = (short)(v + 1);
                idx[e + 4] = (short)(v + 3);
                idx[e + 5] = (short)(v + 2);
            }

            return idx;
        }

        public void Dispose()
        {
            _effect?.Dispose();
            _vb?.Dispose();
            _ib?.Dispose();
            ReflectionRT?.Dispose();
            ContactRT?.Dispose();
        }
    }
}
