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
        /// <summary>Screen UV band below the feet pivot where vertical ripple ramps to full strength.</summary>
        public float PivotStableBand { get; set; } = 0.012f;
        /// <summary>Horizontal ripple strength at the pivot (0 = locked, 1 = full).</summary>
        public float PivotHorizontalRipple { get; set; } = 0.25f;
        /// <summary>Surface highlight shimmer added to final color (does not move reflection UVs).</summary>
        public float SurfaceShimmerStrength { get; set; } = 0.05f;
        /// <summary>Rim ripple strength near the puddle outer mask edge (does not move reflection UVs).</summary>
        public float EdgeRippleStrength { get; set; } = 0.04f;

        /// <summary>When true, <see cref="Radius"/> and/or <see cref="MaxWaterZ"/> change in discrete steps on a timer.</summary>
        public bool DynamicExpansionEnabled { get; set; }

        public sbyte DynamicMinWaterZ { get; set; }
        public sbyte DynamicMaxWaterZ { get; set; }
        /// <summary>Water Z levels added/removed per timed step.</summary>
        public sbyte DynamicWaterZStep { get; set; } = 1;
        /// <summary>Seconds between water-Z steps.</summary>
        public float DynamicWaterZIntervalSeconds { get; set; } = 1f;
        public float DynamicMinRadius { get; set; } = 22f;
        public float DynamicMaxRadius { get; set; } = 88f;
        /// <summary>Radius change in pixels per timed step.</summary>
        public float DynamicRadiusStep { get; set; } = 10f;
        /// <summary>Seconds between radius steps.</summary>
        public float DynamicRadiusIntervalSeconds { get; set; } = 0.5f;

        internal int DynamicRadiusDirection = 1;
        internal int DynamicWaterZDirection = 1;
        internal uint NextRadiusUpdateTick;
        internal uint NextWaterZUpdateTick;

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

        internal bool AnimatesRadius =>
            DynamicRadiusStep > 0f
            && DynamicRadiusIntervalSeconds > 0f
            && DynamicMinRadius < DynamicMaxRadius;

        internal bool AnimatesWaterZ =>
            DynamicWaterZStep > 0
            && DynamicWaterZIntervalSeconds > 0f
            && DynamicMinWaterZ < DynamicMaxWaterZ;

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
