// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO;

namespace ClassicUO.Game
{
    public static class PuddleManager
    {
        private static readonly List<PuddleRegion> _regions = new();
        private static int _dynamicPuddleCount;

        public static PuddleRegion Add(int tileX, int tileY, int tileZ, float radius = 44f, float alpha = 0.85f)
        {
            var region = new PuddleRegion
            {
                TileX = tileX,
                TileY = tileY,
                TileZ = tileZ,
                Radius = radius,
                Alpha = alpha
            };
            _regions.Add(region);
            return region;
        }

        internal static void RegisterDynamicExpansion(PuddleRegion region)
        {
            if (region.DynamicExpansionEnabled)
            {
                _dynamicPuddleCount++;
            }
        }

        public static void Remove(uint id)
        {
            for (int i = _regions.Count - 1; i >= 0; i--)
            {
                if (_regions[i].Id == id)
                {
                    UnregisterDynamicExpansion(_regions[i]);
                    _regions[i].DisposeHeightMask();
                    _regions.RemoveAt(i);
                }
            }
        }

        public static void Clear()
        {
            foreach (PuddleRegion region in _regions)
            {
                region.DisposeHeightMask();
            }

            _regions.Clear();
            _dynamicPuddleCount = 0;
        }

        /// <summary>
        /// Live region list for rendering. Do not modify while iterating during draw.
        /// </summary>
        public static IReadOnlyList<PuddleRegion> GetRegions() => _regions;

        /// <summary>
        /// Applies discrete timed steps for dynamic puddles. No-op when none are registered.
        /// </summary>
        public static void Update(World world)
        {
            if (_dynamicPuddleCount == 0 || world == null)
            {
                return;
            }

            uint now = Time.Ticks;

            for (int i = 0; i < _regions.Count; i++)
            {
                PuddleRegion region = _regions[i];

                if (!region.DynamicExpansionEnabled)
                {
                    continue;
                }

                if (TryApplyDiscreteExpansionStep(region, world, now))
                {
                    region.MaskDirty = true;
                }
            }
        }

        private static void UnregisterDynamicExpansion(PuddleRegion region)
        {
            if (region.DynamicExpansionEnabled && _dynamicPuddleCount > 0)
            {
                _dynamicPuddleCount--;
            }
        }

        private static bool TryApplyDiscreteExpansionStep(PuddleRegion region, World world, uint now)
        {
            bool changed = false;

            if (region.AnimatesRadius && now >= region.NextRadiusUpdateTick)
            {
                region.NextRadiusUpdateTick = now + IntervalToMilliseconds(region.DynamicRadiusIntervalSeconds);
                changed |= ApplyRadiusStep(region);
            }

            if (region.UseHeightMask
                && world.Map != null
                && region.AnimatesWaterZ
                && now >= region.NextWaterZUpdateTick)
            {
                region.NextWaterZUpdateTick = now + IntervalToMilliseconds(region.DynamicWaterZIntervalSeconds);
                changed |= ApplyWaterZStep(region);
            }

            return changed;
        }

        private static bool ApplyRadiusStep(PuddleRegion region)
        {
            float next = region.Radius + (region.DynamicRadiusDirection * region.DynamicRadiusStep);

            if (next >= region.DynamicMaxRadius)
            {
                region.Radius = region.DynamicMaxRadius;
                region.DynamicRadiusDirection = -1;
                return true;
            }

            if (next <= region.DynamicMinRadius)
            {
                region.Radius = region.DynamicMinRadius;
                region.DynamicRadiusDirection = 1;
                return true;
            }

            region.Radius = next;
            return true;
        }

        private static bool ApplyWaterZStep(PuddleRegion region)
        {
            int delta = region.DynamicWaterZDirection * region.DynamicWaterZStep;
            int next = region.MaxWaterZ + delta;

            if (next >= region.DynamicMaxWaterZ)
            {
                region.MaxWaterZ = region.DynamicMaxWaterZ;
                region.DynamicWaterZDirection = -1;
                return true;
            }

            if (next <= region.DynamicMinWaterZ)
            {
                region.MaxWaterZ = region.DynamicMinWaterZ;
                region.DynamicWaterZDirection = 1;
                return true;
            }

            region.MaxWaterZ = (sbyte)next;
            return true;
        }

        private static uint IntervalToMilliseconds(float intervalSeconds)
        {
            return (uint)Math.Max(1, intervalSeconds * 1000f);
        }

        /// <summary>
        /// True when any registered puddle covers the given absolute isometric point.
        /// </summary>
        public static bool ContainsWorldPoint(float worldX, float worldY, World world)
        {
            if (world == null || _regions.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _regions.Count; i++)
            {
                if (PuddleRenderer.ContainsWorldPoint(_regions[i], worldX, worldY, world))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when any puddle covers the given tile (shape mask + optional height clip).
        /// </summary>
        public static bool ContainsTile(int tileX, int tileY, sbyte tileZ, World world)
        {
            if (world == null || _regions.Count == 0)
            {
                return false;
            }

            float sampleTileX = tileX + 0.5f;
            float sampleTileY = tileY + 0.5f;

            for (int i = 0; i < _regions.Count; i++)
            {
                PuddleRegion region = _regions[i];

                if (PuddleRenderer.SampleOuterShapeAlpha(region, sampleTileX, sampleTileY) <= 0.02f)
                {
                    continue;
                }

                if (region.UseHeightMask && world.Map != null)
                {
                    if (world.Map.GetTileZ(tileX, tileY) >= region.MaxWaterZ)
                    {
                        continue;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Spawns a footstep ripple when a mobile lands on a visible puddle tile.
        /// </summary>
        public static void TryCreateFootstepRipple(int tileX, int tileY, sbyte tileZ, World world)
        {
            if (world == null || !world.InGame)
            {
                return;
            }

            if (_regions.Count == 0 || !ContainsTile(tileX, tileY, tileZ, world))
            {
                return;
            }

            float worldX = (tileX - tileY) * 22f;
            float worldY = (tileX + tileY) * 22f - (tileZ << 2);
            world.FootstepRippleEffect.CreateRipple(worldX, worldY);
        }
    }
}
