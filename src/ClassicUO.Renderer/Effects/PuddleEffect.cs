// SPDX-License-Identifier: BSD-2-Clause

using System;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Effects
{
    public sealed class PuddleEffect : Effect
    {
        // D3D9 Effects Framework binary magic: 0xFEFF0901 (LE: 01 09 FF FE)
        private static readonly byte[] _d3d9EffectsMagic = { 0x01, 0x09, 0xFF, 0xFE };

        public PuddleEffect(GraphicsDevice graphicsDevice)
            : base(graphicsDevice, Resources.GetPuddleShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            Time = Parameters["Time"];
            PuddleCenterUV = Parameters["PuddleCenterUV"];
            PuddleRadiusU = Parameters["PuddleRadiusU"];
            PuddleRadiusV = Parameters["PuddleRadiusV"];
            Alpha = Parameters["Alpha"];
            ReflectStrength = Parameters["ReflectStrength"];
            WaveStrength = Parameters["WaveStrength"];
            WaveSpeed = Parameters["WaveSpeed"];
            WaveScale = Parameters["WaveScale"];

            if (TryGetParameter(this, "PivotStableBand", out EffectParameter? pivotStableBand))
            {
                PivotStableBand = pivotStableBand;
            }

            if (TryGetParameter(this, "PivotHorizontalRipple", out EffectParameter? pivotHorizontalRipple))
            {
                PivotHorizontalRipple = pivotHorizontalRipple;
            }

            if (TryGetParameter(this, "SurfaceShimmerStrength", out EffectParameter? surfaceShimmerStrength))
            {
                SurfaceShimmerStrength = surfaceShimmerStrength;
            }

            if (TryGetParameter(this, "EdgeRippleStrength", out EffectParameter? edgeRippleStrength))
            {
                EdgeRippleStrength = edgeRippleStrength;
            }

            SupportsHeightMask = TryGetParameter(this, "UseHeightMask", out EffectParameter? useHeightMask);
            if (SupportsHeightMask)
            {
                UseHeightMask = useHeightMask!;
                MaskTileStepU = Parameters["MaskTileStepU"];
                MaskTileStepV = Parameters["MaskTileStepV"];
                MaskMinTileX = Parameters["MaskMinTileX"];
                MaskMinTileY = Parameters["MaskMinTileY"];
                MaskTileCols = Parameters["MaskTileCols"];
                MaskTileRows = Parameters["MaskTileRows"];
                if (TryGetParameter(this, "MaskSubScale", out EffectParameter? maskSubScale))
                {
                    MaskSubScale = maskSubScale;
                }
            }

            if (TryGetParameter(this, "PuddleTileX", out EffectParameter? puddleTileX))
            {
                PuddleTileX = puddleTileX;
            }

            if (TryGetParameter(this, "PuddleTileY", out EffectParameter? puddleTileY))
            {
                PuddleTileY = puddleTileY;
            }

            SupportsContactMap = TryGetParameter(this, "UseContactMap", out EffectParameter? useContactMap);
            if (SupportsContactMap)
            {
                UseContactMap = useContactMap!;
            }

            CurrentTechnique = Techniques["PuddleTechnique"];
        }

        public bool SupportsHeightMask { get; }
        public bool SupportsContactMap { get; }

        public static bool TryCreate(GraphicsDevice graphicsDevice, out PuddleEffect? effect)
        {
            ReadOnlySpan<byte> shaderBytes = Resources.GetPuddleShader();
            if (!IsValidD3D9Effect(shaderBytes))
            {
                uint magic = shaderBytes.Length >= 4
                    ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(shaderBytes)
                    : 0;
                Console.WriteLine(
                    $"[PuddleEffect] Embedded Puddle.fxc rejected: length={shaderBytes.Length}, magic=0x{magic:X8} (expected 0xFEFF0901). " +
                    "The on-disk .fxc may be fine — FileEmbed embeds at C# compile time. After recompiling shaders, run: dotnet build --no-incremental"
                );
                effect = null;
                return false;
            }

            effect = new PuddleEffect(graphicsDevice);
            return true;
        }

        private static bool TryGetParameter(Effect effect, string name, out EffectParameter? parameter)
        {
            foreach (EffectParameter p in effect.Parameters)
            {
                if (p.Name == name)
                {
                    parameter = p;
                    return true;
                }
            }

            parameter = null;
            return false;
        }

        private static bool IsValidD3D9Effect(ReadOnlySpan<byte> data)
        {
            if (data.Length < 4)
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                if (data[i] != _d3d9EffectsMagic[i])
                {
                    return false;
                }
            }

            return true;
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter Time { get; }
        public EffectParameter PuddleCenterUV { get; }
        public EffectParameter PuddleRadiusU { get; }
        public EffectParameter PuddleRadiusV { get; }
        public EffectParameter Alpha { get; }
        public EffectParameter ReflectStrength { get; }
        public EffectParameter WaveStrength { get; }
        public EffectParameter WaveSpeed { get; }
        public EffectParameter WaveScale { get; }
        public EffectParameter? UseHeightMask { get; }
        public EffectParameter? MaskTileStepU { get; }
        public EffectParameter? MaskTileStepV { get; }
        public EffectParameter? MaskMinTileX { get; }
        public EffectParameter? MaskMinTileY { get; }
        public EffectParameter? MaskTileCols { get; }
        public EffectParameter? MaskTileRows { get; }
        public EffectParameter? PuddleTileX { get; }
        public EffectParameter? PuddleTileY { get; }
        public EffectParameter? MaskSubScale { get; }
        public EffectParameter? UseContactMap { get; }
        public EffectParameter? PivotStableBand { get; }
        public EffectParameter? PivotHorizontalRipple { get; }
        public EffectParameter? SurfaceShimmerStrength { get; }
        public EffectParameter? EdgeRippleStrength { get; }
    }
}
