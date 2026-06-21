// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Game
{
    public sealed class PuddleRegion
    {
        private static uint _nextId = 1;

        public uint Id { get; } = _nextId++;
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int TileZ { get; set; }
        public float Radius { get; set; } = 44f;
        public float Alpha { get; set; } = 0.85f;
    }
}
