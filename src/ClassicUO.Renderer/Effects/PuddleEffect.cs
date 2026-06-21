// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Effects
{
    public sealed class PuddleEffect : Effect
    {
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

        public EffectParameter MatrixTransform { get; }
        public EffectParameter Time { get; }
        public EffectParameter PuddleCenterUV { get; }
        public EffectParameter PuddleRadiusU { get; }
        public EffectParameter PuddleRadiusV { get; }
        public EffectParameter Alpha { get; }
    }
}
