// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO;
using ClassicUO.Assets;
using ClassicUO.Game.Combat;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using CUO_API;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.GameObjects
{
    public sealed class MovingEffect : GameEffect
    {
        public MovingEffect
        (
            World world,
            EffectManager manager,
            uint src,
            uint trg,
            ushort xSource,
            ushort ySource,
            sbyte zSource,
            ushort xTarget,
            ushort yTarget,
            sbyte zTarget,
            ushort graphic,
            ushort hue,
            bool fixedDir,
            int duration,
            byte speed
        ) : base(world, manager, graphic, hue, 0, speed)
        {
            FixedDir = fixedDir;

            // we override interval time with speed
            int d = Constants.ITEM_EFFECT_ANIMATION_DELAY * 2;

            IntervalInMs = (uint)(d + (speed * d));

            // moving effects want a +22 to the X
            // Offset.X -= 22;

            Entity source = World.Get(src);

            if (SerialHelper.IsValid(src) && source != null)
            {
                SetSource(source);
                // ClassicUO.Utility.Logging.Log.Trace($"[MovingEffect] Using entity source: Serial={src}, Position=({source.X}, {source.Y}, {source.Z})");
            }
            else
            {
                SetSource(xSource, ySource, zSource);
                // ClassicUO.Utility.Logging.Log.Trace($"[MovingEffect] Using coordinate source: Serial={src} (Serial.Zero), Position=({xSource}, {ySource}, {zSource})");
            }


            Entity target = World.Get(trg);

            if (SerialHelper.IsValid(trg) && target != null)
            {
                SetTarget(target);
            }
            else
            {
                SetTarget(xTarget, yTarget, zTarget);
            }

            // DEBUG: Log direction line effects (graphic 14138 = 0x373A)
            if (graphic == 0x373A && CUOEnviroment.Debug)
            {
                (int sX, int sY, int sZ) = GetSource();
                (int tX, int tY, int tZ) = GetTarget();
            }

            // Detect direction-based mode (Serial.Zero source means no entity reference)
            _isDirectionBased = (Source == null);
            _creationTicks = Time.Ticks;

            Console.WriteLine($"[MovingEffect] Source: {Source}, Target: {Target}");

            if (_isDirectionBased)
            {
                // Calculate direction vector and distance for linear movement
                (int sX, int sY, int sZ) = GetSource();
                (int tX, int tY, int tZ) = GetTarget();

                float dx = tX - sX;
                float dy = tY - sY;
                _totalDistance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (_totalDistance > 0)
                {
                    _directionVector = new Vector2(dx / _totalDistance, dy / _totalDistance);
                }
                else
                {
                    _directionVector = new Vector2(1, 0);  // Default east
                }

                // ClassicUO.Utility.Logging.Log.Trace($"[MovingEffect] Direction-based projectile: start=({sX},{sY}), end=({tX},{tY}), direction=({_directionVector.X:F3},{_directionVector.Y:F3}), distance={_totalDistance:F2}");

                // Compensate for sprite rotation origin
                // DrawStaticRotated rotates around top-left corner (Vector2.Zero), but we want
                // the CENTER of the sprite to be at the world position (to match debug line)
                ref readonly SpriteInfo artInfo = ref Client.Game.UO.Arts.GetArt(graphic);
                if (artInfo.Texture != null)
                {
                    // For proper alignment, offset so sprite center is at world position
                    // Since rotation is around top-left, move the drawing position by -half size
                    float centerOffsetX = artInfo.UV.Width / 2f;
                    float centerOffsetY = artInfo.UV.Height / 2f;

                    // Pre-compensate so arrow center aligns with exact position
                    Offset.X = centerOffsetX;
                    Offset.Y = centerOffsetY;

                    // sX += (int)centerOffsetX;
                    // sY -= (int)centerOffsetY;
                    // tX += (int)centerOffsetX;
                    // tY -= (int)centerOffsetY;

                    Console.WriteLine($"[MovingEffect] Constructor: Applied sprite centering offset: ({centerOffsetX}, {centerOffsetY}) for graphic {graphic}, sprite size: ({artInfo.UV.Width}, {artInfo.UV.Height})");
                    Console.WriteLine($"[MovingEffect] Constructor: Initial direction vector: ({_directionVector.X:F3}, {_directionVector.Y:F3}), distance: {_totalDistance:F2}");
                    Console.WriteLine($"[MovingEffect] Constructor: World position: ({sX}, {sY}, {sZ}) -> ({tX}, {tY}, {tZ})");
                }
            }
        }

        public readonly bool FixedDir;

        // Direction-based animation fields (for charged shot arrows, etc.)
        private readonly bool _isDirectionBased;
        private readonly Vector2 _directionVector;
        private readonly float _totalDistance;
        private readonly long _creationTicks;

        // Add field to class to track if this is the first update
        private bool _isFirstUpdate = true;

        public override void Update()
        {
            base.Update();
            UpdateOffset();
        }


        private void UpdateOffset()
        {
            if (_isDirectionBased)
            {
                UpdateTargetBasedOffset_Projectile();
            }
            else
            {
                UpdateTargetBasedOffset();
            }
        }

        private void UpdateDirectionBasedOffset()
        {
            // Simple linear movement for direction-based projectiles (charged shot arrows)
            // No complex interpolation, no sub-tile smoothing that causes drift

            (int startX, int startY, int startZ) = GetSource();
            (int endX, int endY, int endZ) = GetTarget();

            // Calculate elapsed time and distance traveled
            long elapsedTicks = Time.Ticks - _creationTicks;
            float elapsedSeconds = elapsedTicks / 1000f;

            // Calculate distance based on time and speed
            // IntervalInMs controls the total animation duration
            float progress = elapsedTicks / (float)IntervalInMs;
            float distanceTraveled = _totalDistance * progress;

            // Check if reached end
            if (distanceTraveled >= _totalDistance || progress >= 1.0f)
            {
                RemoveMe();
                return;
            }

            // Calculate current world position along straight line
            float currentWorldX = startX + (_directionVector.X * distanceTraveled);
            float currentWorldY = startY + (_directionVector.Y * distanceTraveled);

            // Update world position if crossed tile boundary
            int newTileX = (int)Math.Round(currentWorldX);
            int newTileY = (int)Math.Round(currentWorldY);

            if (newTileX != X || newTileY != Y)
            {
                SetSource((ushort)newTileX, (ushort)newTileY, (sbyte)startZ);
            }

            // Calculate sub-tile offset for smooth rendering (but no accumulation/drift)
            float subTileX = currentWorldX - newTileX;
            float subTileY = currentWorldY - newTileY;

            // Convert world sub-tile offset to screen offset (isometric projection)
            Offset.X = (subTileX - subTileY) * 22f;
            Offset.Y = (subTileX + subTileY) * 22f;
            Offset.Z = 0;

            // Set rotation angle toward movement direction
            IsPositionChanged = true;

            // FIX: Convert world direction to SCREEN direction for proper isometric sprite rotation
            // In isometric projection, world (X,Y) maps to screen differently:
            //   screenDx = (worldX - worldY) * 22
            //   screenDy = (worldX + worldY) * 22
            // The sprite rotation should match the SCREEN direction, not world direction
            float screenDx = (_directionVector.X - _directionVector.Y) * 22f;
            float screenDy = (_directionVector.X + _directionVector.Y) * 22f;

            // Use screen-space angle for sprite rotation (FIX for isometric projection)
            AngleToTarget = (float)Math.Atan2(-screenDy, -screenDx);
        }

        private void UpdateTargetBasedOffset()
        {
            // Original target-based animation for traditional UO moving effects
            if (Target != null && Target.IsDestroyed)
            {
                TargetX = Target.X;
                TargetY = Target.Y;
                TargetZ = Target.Z;
            }

            int playerX = World.Player.X;
            int playerY = World.Player.Y;
            int playerZ = World.Player.Z;

            (int sX, int sY, int sZ) = GetSource();
            int offsetSourceX = sX - playerX;
            int offsetSourceY = sY - playerY;
            int offsetSourceZ = sZ - playerZ;

            (int tX, int tY, int tZ) = GetTarget();
            int offsetTargetX = tX - playerX;
            int offsetTargetY = tY - playerY;
            int offsetTargetZ = tZ - playerZ;

            var source = new Vector2((offsetSourceX - offsetSourceY) * 22, (offsetSourceX + offsetSourceY) * 22 - offsetSourceZ * 4);

            source.X += Offset.X;
            source.Y += Offset.Y;

            var target = new Vector2((offsetTargetX - offsetTargetY) * 22, (offsetTargetX + offsetTargetY) * 22 - offsetTargetZ * 4);

            Vector2 offset = target - source;
            float distance = offset.Length();
            float frameIndependentSpeed = IntervalInMs * Time.Delta;
            Vector2 s0;

            if (distance > frameIndependentSpeed)
            {
                offset.Normalize();
                s0 = offset * frameIndependentSpeed;
            }
            else
            {
                s0 = target;
            }


            if (distance <= 22)
            {
                RemoveMe();

                return;
            }

            int newOffsetX = (int) (source.X / 22f);
            int newOffsetY = (int) (source.Y / 22f);

            TileOffsetOnMonitorToXY(ref newOffsetX, ref newOffsetY, out int newCoordX, out int newCoordY);

            int newX = playerX + newCoordX;
            int newY = playerY + newCoordY;

            if (newX == tX && newY == tY)
            {
                RemoveMe();

                return;
            }


            IsPositionChanged = true;
            AngleToTarget = (float) Math.Atan2(-offset.Y, -offset.X);

            if (newX != sX || newY != sY)
            {
                // TODO: Z is wrong. We have to calculate an average
                SetSource((ushort) newX, (ushort) newY, (sbyte)sZ);

                var nextSource = new Vector2((newCoordX - newCoordY) * 22, (newCoordX + newCoordY) * 22 - offsetSourceZ * 4);

                Offset.X = source.X - nextSource.X;
                Offset.Y = source.Y - nextSource.Y;
            }

            Offset.X += s0.X;
            Offset.Y += s0.Y;
        }

        private void UpdateTargetBasedOffset_Projectile()
        {
            // Original target-based animation for traditional UO moving effects
            if (Target != null && Target.IsDestroyed)
            {
                TargetX = Target.X;
                TargetY = Target.Y;
                TargetZ = Target.Z;
            }

            int playerX = World.Player.X;
            int playerY = World.Player.Y;
            int playerZ = World.Player.Z;

            // (int sX, int sY, int sZ) = GetSource();
            int sX = X;
            int sY = Y;
            int sZ = Z;
            int offsetSourceX = sX - playerX;
            int offsetSourceY = sY - playerY;
            int offsetSourceZ = sZ - playerZ;

            // (int tX, int tY, int tZ) = GetTarget();
            int tX = TargetX;
            int tY = TargetY;
            int tZ = TargetZ;
            int offsetTargetX = tX - playerX;
            int offsetTargetY = tY - playerY;
            int offsetTargetZ = tZ - playerZ;

            // Calculate raw source position (without Offset) for angle calculation
            var rawSource = new Vector2((offsetSourceX - offsetSourceY) * 22, (offsetSourceX + offsetSourceY) * 22 - offsetSourceZ * 4);

            // Calculate source with Offset for movement calculation
            var source = new Vector2(rawSource.X + Offset.X, rawSource.Y + Offset.Y);

            Console.WriteLine($"[MovingEffect.Update] BEFORE adding Offset: source screen=({rawSource.X:F1}, {rawSource.Y:F1}), currentOffset=({Offset.X:F1}, {Offset.Y:F1}, {Offset.Z:F1})");
            Console.WriteLine($"[MovingEffect.Update] AFTER adding Offset: source screen=({source.X:F1}, {source.Y:F1})");

            var target = new Vector2((offsetTargetX - offsetTargetY) * 22, (offsetTargetX + offsetTargetY) * 22 - offsetTargetZ * 4);

            // Use source (with Offset) for movement calculation
            Vector2 offset = target - source;

            // Calculate direction from raw source (character center) to target for rotation angle
            // This matches the debug line calculation which doesn't include sprite offset
            Vector2 directionForAngle = target - rawSource;
            float distance = offset.Length();
            float frameIndependentSpeed = IntervalInMs * Time.Delta;
            Vector2 s0;

            if (distance > frameIndependentSpeed)
            {
                offset.Normalize();
                s0 = offset * frameIndependentSpeed;
            }
            else
            {
                s0 = target;
            }


            if (distance <= 22)
            {
                RemoveMe();

                return;
            }

            int newOffsetX = (int)(source.X / 22f);
            int newOffsetY = (int)(source.Y / 22f);

            TileOffsetOnMonitorToXY(ref newOffsetX, ref newOffsetY, out int newCoordX, out int newCoordY);

            int newX = playerX + newCoordX;
            int newY = playerY + newCoordY;

            if (newX == tX && newY == tY)
            {
                RemoveMe();

                return;
            }


            IsPositionChanged = true;
            // FIX: Use directionForAngle (from raw source to target) instead of offset (from offset source to target)
            // This aligns the arrow rotation with the debug line which uses character center as origin
            AngleToTarget = (float)Math.Atan2(-directionForAngle.Y, -directionForAngle.X);

            Console.WriteLine($"[MovingEffect.Update] Arrow angle: {AngleToTarget * 180 / Math.PI:F1}° (radians: {AngleToTarget:F3})");

            if (newX != sX || newY != sY)
            {
                // TODO: Z is wrong. We have to calculate an average
                SetSource((ushort)newX, (ushort)newY, (sbyte)sZ);

                var nextSource = new Vector2((newCoordX - newCoordY) * 22, (newCoordX + newCoordY) * 22 - offsetSourceZ * 4);

                float oldOffsetX = Offset.X;
                float oldOffsetY = Offset.Y;

                Offset.X = source.X - nextSource.X;
                Offset.Y = source.Y - nextSource.Y;

                Console.WriteLine($"[MovingEffect.Update] Tile changed ({sX},{sY})->({newX},{newY}), Offset changed: ({oldOffsetX:F1},{oldOffsetY:F1})->({Offset.X:F1},{Offset.Y:F1})");
            }

            if (!_isFirstUpdate)
            {
                Console.WriteLine($"[MovingEffect.Update] Adding movement s0=({s0.X:F1}, {s0.Y:F1}) to Offset");
                Offset.X += s0.X;
                Offset.Y += s0.Y;
            }
            else
            {
                _isFirstUpdate = false;
                Console.WriteLine($"[MovingEffect.Update] FIRST UPDATE - Skip movement, Offset stays: ({Offset.X:F1}, {Offset.Y:F1})");
            }

            Console.WriteLine($"[MovingEffect.Update] Final Offset: ({Offset.X:F1}, {Offset.Y:F1}, {Offset.Z:F1}), World pos: ({X},{Y},{Z})");
        }

        private void RemoveMe()
        {
            // DEBUG: Notify visualizer of collision point
            (int tX, int tY, int tZ) = GetTarget();
            ProjectileDebugVisualizer.OnMovingEffectEnd(
                new Point3D(tX, tY, tZ),
                Graphic
            );

            CreateExplosionEffect();

            Destroy();
        }

        private static void TileOffsetOnMonitorToXY(ref int ofsX, ref int ofsY, out int x, out int y)
        {
            y = 0;

            if (ofsX == 0)
            {
                x = y = ofsY >> 1;
            }
            else if (ofsY == 0)
            {
                x = ofsX >> 1;
                y = -x;
            }
            else
            {
                int absX = Math.Abs(ofsX);
                int absY = Math.Abs(ofsY);
                x = ofsX;

                if (ofsY > ofsX)
                {
                    if (ofsX < 0 && ofsY < 0)
                    {
                        y = absX - absY;
                    }
                    else if (ofsX > 0 && ofsY > 0)
                    {
                        y = absY - absX;
                    }
                }
                else if (ofsX > ofsY)
                {
                    if (ofsX < 0 && ofsY < 0)
                    {
                        y = -(absY - absX);
                    }
                    else if (ofsX > 0 && ofsY > 0)
                    {
                        y = -(absX - absY);
                    }
                }

                if (y == 0 && ofsY != ofsX)
                {
                    if (ofsY < 0)
                    {
                        y = -(absX + absY);
                    }
                    else
                    {
                        y = absX + absY;
                    }
                }

                y /= 2;
                x += y;
            }
        }
    }
}
