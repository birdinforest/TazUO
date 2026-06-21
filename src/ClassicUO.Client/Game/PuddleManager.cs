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

        public static void Remove(uint id) => _regions.RemoveAll(r => r.Id == id);

        public static void Clear() => _regions.Clear();

        public static IReadOnlyList<PuddleRegion> GetRegions() => _regions.ToArray();
    }
}
