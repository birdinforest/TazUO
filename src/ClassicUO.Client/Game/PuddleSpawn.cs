// SPDX-License-Identifier: BSD-2-Clause

using System.Globalization;

namespace ClassicUO.Game
{
    public static class PuddleSpawn
    {
        public static PuddleRegion? TryCreate(World world, PuddleSpawnOptions options, out string message)
        {
            if (world.Player == null)
            {
                message = "No player.";
                return null;
            }

            PuddleRegion region = PuddleManager.Add(
                options.TileX,
                options.TileY,
                options.TileZ,
                options.Radius,
                options.Alpha
            );

            region.ReflectStrength = options.ReflectStrength;
            region.WaveStrength = options.WaveStrength;
            region.WaveSpeed = options.WaveSpeed;
            region.WaveScale = options.WaveScale;
            region.PivotStableBand = options.PivotStableBand;
            region.PivotHorizontalRipple = options.PivotHorizontalRipple;

            PuddleSpawnOptions.ApplyDefaultHeightMask(options, world);
            if (options.UseHeightMask)
            {
                region.UseHeightMask = true;
                region.MaxWaterZ = options.MaxWaterZ;
                region.MaskDirty = true;
                region.MinWaterZ = PuddleRenderer.ComputeMinGroundZ(region, world);
                region.TileZ = region.MinWaterZ;
            }

            string heightInfo = region.UseHeightMask
                ? string.Format(CultureInfo.InvariantCulture, " minZ={0} maxWaterZ={1}", region.MinWaterZ, region.MaxWaterZ)
                : string.Empty;

            message = string.Format(
                CultureInfo.InvariantCulture,
                "Puddle #{0} at ({1},{2},{3}) radius={4:0} alpha={5:0.00} reflect={6:0.00} wave={7:0.000} speed={8:0.0} scale={9:0}{10}.",
                region.Id,
                region.TileX,
                region.TileY,
                region.TileZ,
                options.Radius,
                options.Alpha,
                options.ReflectStrength,
                options.WaveStrength,
                options.WaveSpeed,
                options.WaveScale,
                heightInfo
            );

            return region;
        }
    }
}
