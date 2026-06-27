// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Globalization;

namespace ClassicUO.Game
{
    public sealed class PuddleSpawnOptions
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int TileZ { get; set; }
        public float Radius { get; set; } = 44f;
        public float Alpha { get; set; } = 0.85f;
        public float ReflectStrength { get; set; } = 0.45f;
        public float WaveStrength { get; set; } = 0.018f;
        public float WaveSpeed { get; set; } = 1.0f;
        public float WaveScale { get; set; } = 18.0f;
        public float PivotStableBand { get; set; } = 0.012f;
        public float PivotHorizontalRipple { get; set; } = 0.25f;
        public float SurfaceShimmerStrength { get; set; } = 0.05f;
        public float EdgeRippleStrength { get; set; } = 0.04f;
        public bool UseHeightMask { get; set; }
        public sbyte MaxWaterZ { get; set; }

        public static PuddleSpawnOptions FromPlayer(int tileX, int tileY, int tileZ)
        {
            return new PuddleSpawnOptions
            {
                TileX = tileX,
                TileY = tileY,
                TileZ = tileZ
            };
        }

        public static bool TryParseCommand(string[] args, int playerX, int playerY, int playerZ, out PuddleSpawnOptions options)
        {
            options = FromPlayer(playerX, playerY, playerZ);

            if (args.Length > 1 && float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float radius))
            {
                options.Radius = radius;
            }

            if (args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float reflect))
            {
                options.ReflectStrength = Math.Clamp(reflect, 0f, 1f);
            }

            if (args.Length > 3 && float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float waveStrength))
            {
                options.WaveStrength = Math.Max(0f, waveStrength);
            }

            if (args.Length > 4 && float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float waveSpeed))
            {
                options.WaveSpeed = Math.Max(0.001f, waveSpeed);
            }

            if (args.Length > 5 && float.TryParse(args[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float waveScale))
            {
                options.WaveScale = Math.Max(1f, waveScale);
            }

            if (args.Length > 6 && sbyte.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte maxWaterZ))
            {
                options.UseHeightMask = true;
                options.MaxWaterZ = maxWaterZ;
            }

            return true;
        }

        /// <summary>
        /// When max water Z is not specified, clip at one Z above the spawn tile so shorelines follow terrain.
        /// </summary>
        public static void ApplyDefaultHeightMask(PuddleSpawnOptions options, World world)
        {
            if (options.UseHeightMask || world.Map == null)
            {
                return;
            }

            sbyte groundZ = world.Map.GetTileZ(options.TileX, options.TileY);
            options.UseHeightMask = true;
            options.MaxWaterZ = (sbyte)Math.Clamp(groundZ + 1, sbyte.MinValue, sbyte.MaxValue);
        }
    }
}
