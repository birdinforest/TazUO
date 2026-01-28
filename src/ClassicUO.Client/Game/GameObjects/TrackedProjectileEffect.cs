// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.GameObjects
{
    /// <summary>
    /// Server-synchronized projectile effect for free-aim arrows and similar projectiles.
    /// Uses time-based animation that matches server physics for accurate collision sync.
    /// This is separate from MovingEffect to preserve legacy spell/arrow behavior.
    /// </summary>
    public sealed class TrackedProjectileEffect : GameEffect
    {
        // Server-synchronized tracking
        public uint ServerSerial { get; set; }

        // Time-based animation (matches server physics)
        private readonly float _speedTilesPerSec;
        private readonly float _expectedTravelTimeMs;
        private readonly long _creationTicks;
        private readonly Vector2 _directionVector;
        private readonly float _totalDistance;

        // Source/target coordinates (not entity references - coordinates only for free-aim)
        private readonly int _startX;
        private readonly int _startY;
        private readonly int _startZ;
        private readonly int _targetX;
        private readonly int _targetY;
        private readonly int _targetZ;

        // Trail particle configuration
        private bool _trailEnabled = false;
        private ushort _trailGraphic = 0x3818;  // Default: spark
        private ushort _trailHue = 0;
        private int _trailSpawnIntervalMs = 50;
        private int _trailParticleDurationMs = 200;
        private float _trailAlphaStart = 0.6f;
        private float _trailAlphaEnd = 0.0f;
        private long _lastTrailSpawn = 0;

        public TrackedProjectileEffect(
            World world,
            EffectManager manager,
            ushort srcX, ushort srcY, sbyte srcZ,
            ushort tgtX, ushort tgtY, sbyte tgtZ,
            ushort graphic, ushort hue,
            float speedTilesPerSec,
            float expectedTravelTimeMs
        ) : base(world, manager, graphic, hue, (int)expectedTravelTimeMs + 1000, 1)  // Add safety margin to duration
        {
            _speedTilesPerSec = speedTilesPerSec;
            _expectedTravelTimeMs = expectedTravelTimeMs;
            _creationTicks = Time.Ticks;

            _startX = srcX;
            _startY = srcY;
            _startZ = srcZ;
            _targetX = tgtX;
            _targetY = tgtY;
            _targetZ = tgtZ;

            // Calculate direction vector
            float dx = tgtX - srcX;
            float dy = tgtY - srcY;
            _totalDistance = (float)Math.Sqrt(dx * dx + dy * dy);
            _directionVector = _totalDistance > 0
                ? new Vector2(dx / _totalDistance, dy / _totalDistance)
                : new Vector2(1, 0);

            SetSource(srcX, srcY, srcZ);
            SetTarget(tgtX, tgtY, tgtZ);

            // Enable trail by default for tracked projectiles
            EnableTrail();

            Console.WriteLine($"[TrackedProjectileEffect] Created: ({srcX},{srcY},{srcZ}) -> ({tgtX},{tgtY},{tgtZ}), speed={speedTilesPerSec} t/s, travelTime={expectedTravelTimeMs}ms, distance={_totalDistance:F2}");
        }

        /// <summary>
        /// Enable trail effect for this projectile.
        /// </summary>
        public void EnableTrail(
            ushort graphic = 0x3818,
            ushort hue = 0,
            int spawnIntervalMs = 50,
            int particleDurationMs = 200,
            float alphaStart = 0.6f,
            float alphaEnd = 0.0f)
        {
            _trailEnabled = true;
            _trailGraphic = graphic;
            _trailHue = hue;
            _trailSpawnIntervalMs = spawnIntervalMs;
            _trailParticleDurationMs = particleDurationMs;
            _trailAlphaStart = alphaStart;
            _trailAlphaEnd = alphaEnd;
            _lastTrailSpawn = Time.Ticks;
        }

        public override void Update()
        {
            base.Update();

            if (!IsDestroyed)
            {
                UpdateTimeBasedPosition();

                // Spawn trail particles if enabled
                if (_trailEnabled)
                {
                    SpawnTrailParticle();
                }
            }
        }

        private void UpdateTimeBasedPosition()
        {
            // Calculate progress using real elapsed time (matches server physics)
            float elapsedMs = Time.Ticks - _creationTicks;
            float progress = Math.Min(1.0f, elapsedMs / _expectedTravelTimeMs);

            // Linear interpolation to current position
            float currentX = _startX + (_directionVector.X * _totalDistance * progress);
            float currentY = _startY + (_directionVector.Y * _totalDistance * progress);

            // Interpolate Z as well
            float currentZ = _startZ + ((_targetZ - _startZ) * progress);

            // Update tile position
            int newTileX = (int)Math.Round(currentX);
            int newTileY = (int)Math.Round(currentY);
            sbyte newTileZ = (sbyte)Math.Round(currentZ);

            if (newTileX != X || newTileY != Y || newTileZ != Z)
            {
                SetSource((ushort)newTileX, (ushort)newTileY, newTileZ);
            }

            // Sub-tile offset for smooth rendering (isometric projection)
            // UO formula: screenX = (X - Y) * 22, screenY = (X + Y) * 22 - Z * 4
            float subTileX = currentX - newTileX;
            float subTileY = currentY - newTileY;
            float subTileZ = currentZ - newTileZ;
            Offset.X = (subTileX - subTileY) * 22f + 22f;
            Offset.Y = (subTileX + subTileY) * 22f - (subTileZ * 4f);

            // Rotation angle (screen-space for isometric projection)
            float screenDx = (_directionVector.X - _directionVector.Y) * 22f;
            float screenDy = (_directionVector.X + _directionVector.Y) * 22f;
            AngleToTarget = (float)Math.Atan2(-screenDy, -screenDx);

            IsPositionChanged = true;

            // Debug: Log position for comparison with server
            Console.WriteLine($"[TrackedProjectileEffect] Update: tile=({newTileX},{newTileY},{newTileZ}), offset=({Offset.X:F1},{Offset.Y:F1}), progress={progress:F3}, elapsed={elapsedMs:F0}ms");

            // Self-dispose when reaching target (if no collision packet received)
            if (progress >= 1.0f)
            {
                Console.WriteLine($"[TrackedProjectileEffect] Reached end of travel time, self-disposing");
                Destroy();
            }
        }

        /// <summary>
        /// Spawn a trail particle at the current projectile position.
        /// </summary>
        private void SpawnTrailParticle()
        {
            if (Time.Ticks - _lastTrailSpawn < _trailSpawnIntervalMs)
                return;

            // Create trail particle at current position
            var trail = new TrailParticle(
                World,
                World.EffectManager,
                _trailGraphic,
                _trailHue,
                X,
                Y,
                Z,
                _trailParticleDurationMs,
                _trailAlphaStart,
                _trailAlphaEnd);

            World.EffectManager.PushToBack(trail);

            _lastTrailSpawn = Time.Ticks;
        }

        /// <summary>
        /// Force dispose at collision point (called by collision packet handler).
        /// Snaps the projectile to the collision location before destroying.
        /// </summary>
        public void ForceDisposeAtCollision(int collisionX, int collisionY, int collisionZ, bool createImpact)
        {
            Console.WriteLine($"[TrackedProjectileEffect] ForceDisposeAtCollision at ({collisionX},{collisionY},{collisionZ}), createImpact={createImpact}");

            // Snap to collision location before disposing
            SetSource((ushort)collisionX, (ushort)collisionY, (sbyte)collisionZ);
            // Apply the same +22 X offset used during flight for visual consistency
            Offset = new Vector3(22f, 0f, 0f);

            if (createImpact)
            {
                CreateExplosionEffect();
            }

            Destroy();
        }

        // Note: We inherit Draw() from GameEffect partial class (GameEffectView.cs)
        // which already handles rotation via AngleToTarget and all blend modes

        public override void Destroy()
        {
            // Unregister from serial tracking
            if (ServerSerial != 0)
            {
                World.EffectManager?.UnregisterTrackedProjectile(ServerSerial);
            }

            base.Destroy();
        }
    }
}
