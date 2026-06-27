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
    }
}
