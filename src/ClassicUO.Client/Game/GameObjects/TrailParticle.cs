// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Managers;
using ClassicUO.Utility;

namespace ClassicUO.Game.GameObjects
{
    /// <summary>
    /// Fade-out particle effect for projectile speed trails.
    /// Spawns behind moving projectiles to create a visual trail effect.
    /// </summary>
    public sealed class TrailParticle : FixedEffect
    {
        private readonly float _alphaStart;
        private readonly float _alphaEnd;
        private readonly long _creationTicks;

        /// <summary>
        /// Create a trail particle that fades out over time.
        /// </summary>
        /// <param name="world">World instance</param>
        /// <param name="manager">Effect manager</param>
        /// <param name="graphic">Particle graphic ID</param>
        /// <param name="hue">Particle hue</param>
        /// <param name="x">World X coordinate</param>
        /// <param name="y">World Y coordinate</param>
        /// <param name="z">World Z coordinate</param>
        /// <param name="duration">Particle lifetime in milliseconds</param>
        /// <param name="alphaStart">Starting alpha (0.0-1.0)</param>
        /// <param name="alphaEnd">Ending alpha (0.0-1.0)</param>
        public TrailParticle(
            World world,
            EffectManager manager,
            ushort graphic,
            ushort hue,
            ushort x,
            ushort y,
            sbyte z,
            int duration,
            float alphaStart = 0.6f,
            float alphaEnd = 0.0f)
            : base(world, manager, graphic, hue, duration, 0)
        {
            _alphaStart = alphaStart;
            _alphaEnd = alphaEnd;
            _creationTicks = Time.Ticks;

            SetSource(x, y, z);

            // Set initial alpha
            AlphaHue = (byte)(_alphaStart * 255);
        }

        public override void Update()
        {
            base.Update();

            if (!IsDestroyed && Duration > 0)
            {
                // Calculate fade progress (0.0 to 1.0)
                long elapsed = Time.Ticks - _creationTicks;
                float progress = elapsed / (float)Duration;

                if (progress >= 1.0f)
                {
                    // Fully faded - destroy particle
                    Destroy();
                    return;
                }

                // Interpolate alpha from start to end
                float currentAlpha = _alphaStart + (_alphaEnd - _alphaStart) * progress;
                AlphaHue = (byte)(currentAlpha * 255);
            }
        }
    }
}
