using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Effects
{
    internal class BasicUOEffect : Effect
    {
        public BasicUOEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Resources.GetUOShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            WorldMatrix = Parameters["WorldMatrix"];
            Viewport = Parameters["Viewport"];
            Brighlight = Parameters["Brightlight"];
            TexelSize = Parameters["TexelSize"];

            SupportsContactReflect = TryGetParameter(this, "ContactWriteSupported", out _);

            CurrentTechnique = Techniques["HueTechnique"];
            Pass = CurrentTechnique.Passes[0];
        }

        public bool SupportsContactReflect { get; }

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

        public EffectParameter MatrixTransform { get; }
        public EffectParameter WorldMatrix { get; }
        public EffectParameter Viewport { get; }
        public EffectParameter Brighlight { get; }
        public EffectParameter TexelSize { get; }
        public EffectPass Pass { get; }
    }
}
