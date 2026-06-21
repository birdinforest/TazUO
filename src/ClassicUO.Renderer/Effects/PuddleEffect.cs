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

            CurrentTechnique = Techniques["PuddleTechnique"];
        }

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
    }
}
