// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
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

        private readonly PuddleEffect? _effect;
        private readonly VertexBuffer _vb;
        private readonly IndexBuffer _ib;
        private readonly PuddleVertex[] _verts = new PuddleVertex[MAX_PUDDLES * VERTS_PER_PUDDLE];

        public RenderTarget2D? ReflectionRT { get; private set; }

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

        public void CaptureReflection(UltimaBatcher2D batcher, GraphicsDevice gd, RenderTarget2D worldRT)
        {
            if (worldRT == null || worldRT.IsDisposed)
            {
                return;
            }

            int w = worldRT.Width;
            int h = worldRT.Height;

            if (
                ReflectionRT == null
                || ReflectionRT.IsDisposed
                || ReflectionRT.Width != w
                || ReflectionRT.Height != h
            )
            {
                ReflectionRT?.Dispose();
                ReflectionRT = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
            }

            RenderTargetBinding[] prev = gd.GetRenderTargets();
            gd.SetRenderTarget(ReflectionRT);
            gd.Clear(Color.Black);

            batcher.Begin();
            batcher.Draw(worldRT, Vector2.Zero, new Vector3(0, 0, 1));
            batcher.End();

            if (prev != null && prev.Length > 0)
            {
                gd.SetRenderTargets(prev);
            }
            else
            {
                gd.SetRenderTarget(null);
            }
        }

        public void Draw(
            GraphicsDevice gd,
            IReadOnlyList<PuddleRegion> regions,
            ref Matrix worldRTMatrix,
            int rtW,
            int rtH,
            float elapsedSeconds,
            int cameraOffsetX,
            int cameraOffsetY
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

            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.Textures[0] = ReflectionRT;
            gd.SamplerStates[0] = SamplerState.LinearClamp;
            gd.SetVertexBuffer(_vb);
            gd.Indices = _ib;

            int count = Math.Min(regions.Count, MAX_PUDDLES);

            for (int i = 0; i < count; i++)
            {
                PuddleRegion r = regions[i];

                // Match GameObject.UpdateRealScreenPosition / ProjectileDebugVisualizer.
                float cx = ((r.TileX - r.TileY) * 22f) - cameraOffsetX - 22f;
                float cy = ((r.TileX + r.TileY) * 22f - (r.TileZ << 2)) - cameraOffsetY - 22f;

                float hw = r.Radius * 2.0f;
                float hh = r.Radius;

                BuildQuadVerts(i, cx, cy, hw, hh, tx, ty, rtW, rtH);
                _vb.SetData(_verts, i * VERTS_PER_PUDDLE, VERTS_PER_PUDDLE);

                _effect.MatrixTransform.SetValue(fullTransform);
                _effect.Time.SetValue(elapsedSeconds);
                _effect.PuddleCenterUV.SetValue(new Vector2((cx + tx) / rtW, (cy + ty) / rtH));
                _effect.PuddleRadiusU.SetValue(hw / rtW);
                _effect.PuddleRadiusV.SetValue(hh / rtH);
                _effect.Alpha.SetValue(r.Alpha);

                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
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

            if (prevVB != null && prevVB.Length > 0)
            {
                gd.SetVertexBuffers(prevVB);
            }

            gd.Indices = prevIB;
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
        }
    }
}
