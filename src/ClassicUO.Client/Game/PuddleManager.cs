// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game
{
    public static class PuddleManager
    {
        private static readonly List<PuddleRegion> _regions = new();

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

        public static void Remove(uint id)
        {
            for (int i = _regions.Count - 1; i >= 0; i--)
            {
                if (_regions[i].Id == id)
                {
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
        }

        public static IReadOnlyList<PuddleRegion> GetRegions() => _regions.ToArray();

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
