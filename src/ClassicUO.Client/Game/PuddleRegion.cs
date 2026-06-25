// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework.Graphics;

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
        public float ReflectStrength { get; set; } = 0.45f;
        /// <summary>UV distortion amplitude for internal water ripples (typical 0.01–0.04).</summary>
        public float WaveStrength { get; set; } = 0.018f;
        /// <summary>Time multiplier for ripple animation (1 = default speed).</summary>
        public float WaveSpeed { get; set; } = 1.0f;
        /// <summary>Noise frequency scale for ripples (typical 12–24).</summary>
        public float WaveScale { get; set; } = 18.0f;

        /// <summary>When true, tiles at or above <see cref="MaxWaterZ"/> stay dry (land visible).</summary>
        public bool UseHeightMask { get; set; }

        /// <summary>Water surface Z — ground below this Z shows the puddle.</summary>
        public sbyte MaxWaterZ { get; set; }

        /// <summary>Lowest ground Z found in the region (computed when the height mask is built).</summary>
        public sbyte MinWaterZ { get; set; }

        internal Texture2D? HeightMask;
        internal bool MaskDirty = true;
        internal int MaskMinTileX;
        internal int MaskMinTileY;
        internal int MaskTileCols;
        internal int MaskTileRows;
        internal int MaskSubScale = 1;

        internal void SetMaskBounds(int minTileX, int minTileY, int cols, int rows)
        {
            MaskMinTileX = minTileX;
            MaskMinTileY = minTileY;
            MaskTileCols = cols;
            MaskTileRows = rows;
        }

        public void DisposeHeightMask()
        {
            HeightMask?.Dispose();
            HeightMask = null;
            MaskDirty = true;
        }
    }
}
