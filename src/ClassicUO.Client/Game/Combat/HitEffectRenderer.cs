// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;

namespace ClassicUO.Game.Combat
{
    /// <summary>
    /// Renders visual feedback when a mobile is hit by a projectile.
    /// Handles hit flash, particle effects, and screen shake.
    /// </summary>
    public static class HitEffectRenderer
    {
        /// <summary>
        /// Apply hit visual effects to a mobile that was hit by a projectile.
        /// </summary>
        /// <param name="target">The mobile that was hit</param>
        /// <param name="impactGraphic">Graphic ID for impact particles (0 = no particles)</param>
        /// <param name="intensity">Hit intensity (0-255), affects particle count and flash duration</param>
        public static void ApplyHitEffect(Mobile target, ushort impactGraphic, byte intensity)
        {
            if (target == null || target.IsDestroyed)
                return;

            // 1. Flash effect on mobile (tint red briefly)
            // Duration scales with intensity (50-255ms)
            int flashDuration = 50 + (intensity / 5);
            target.SetHitFlash(flashDuration);

            // 2. Particle burst at hit location
            if (impactGraphic != 0)
            {
                int particleCount = Math.Max(1, intensity / 50);
                for (int i = 0; i < particleCount; i++)
                {
                    SpawnHitParticle(target.X, target.Y, target.Z, impactGraphic, target.World);
                }
            }

            // 3. Screen shake for player hit (if intensity is high enough)
            // Note: Camera shake not implemented yet, can be added later if needed
            // if (target == World.Player && intensity > 30)
            // {
            //     Camera.Shake(intensity: Math.Min(3, intensity / 30), duration: 100);
            // }
        }

        /// <summary>
        /// Spawn a hit particle at the specified location.
        /// </summary>
        private static void SpawnHitParticle(int x, int y, sbyte z, ushort graphic, World world)
        {
            if (world == null || world.EffectManager == null)
                return;

            // Add slight random offset for particle spread
            int offsetX = Utility.RandomHelper.GetValue(-1, 1);
            int offsetY = Utility.RandomHelper.GetValue(-1, 1);

            world.EffectManager.CreateEffect(
                Game.Data.GraphicEffectType.FixedXYZ,
                0,  // source serial
                0,  // target serial
                graphic,
                0,  // hue
                (ushort)(x + offsetX),
                (ushort)(y + offsetY),
                z,
                0,  // targetX
                0,  // targetY
                0,  // targetZ
                0,  // speed
                200,  // duration (short-lived particle)
                false,  // fixedDir
                false,  // explodes
                false,  // hasparticles
                Game.Data.GraphicEffectBlendMode.Normal);
        }
    }
}
