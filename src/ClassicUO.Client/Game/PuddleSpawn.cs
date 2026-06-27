// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Globalization;

namespace ClassicUO.Game
{
    public static class PuddleSpawn
    {
        private static void ApplyDynamicExpansion(PuddleRegion region, PuddleSpawnOptions options, World world)
        {
            region.DynamicMinRadius = Math.Min(options.DynamicMinRadius, options.DynamicMaxRadius);
            region.DynamicMaxRadius = Math.Max(options.DynamicMinRadius, options.DynamicMaxRadius);
            region.DynamicRadiusStep = Math.Max(0f, options.DynamicRadiusStep);
            region.DynamicRadiusIntervalSeconds = Math.Max(0f, options.DynamicRadiusIntervalSeconds);
            region.DynamicMinWaterZ = options.DynamicMinWaterZ <= options.DynamicMaxWaterZ
                ? options.DynamicMinWaterZ
                : options.DynamicMaxWaterZ;
            region.DynamicMaxWaterZ = options.DynamicMinWaterZ <= options.DynamicMaxWaterZ
                ? options.DynamicMaxWaterZ
                : options.DynamicMinWaterZ;
            region.DynamicWaterZStep = options.DynamicWaterZStep;
            region.DynamicWaterZIntervalSeconds = Math.Max(0f, options.DynamicWaterZIntervalSeconds);
            region.DynamicRadiusDirection = 1;
            region.DynamicWaterZDirection = 1;

            uint now = Time.Ticks;
            region.NextRadiusUpdateTick = now + (uint)Math.Max(1, region.DynamicRadiusIntervalSeconds * 1000f);
            region.NextWaterZUpdateTick = now + (uint)Math.Max(1, region.DynamicWaterZIntervalSeconds * 1000f);

            region.Radius = region.DynamicMinRadius;
            region.MaskDirty = true;

            if (region.AnimatesWaterZ)
            {
                region.UseHeightMask = true;
                region.MaxWaterZ = region.DynamicMinWaterZ;
                region.MinWaterZ = PuddleRenderer.ComputeMinGroundZ(region, world);
                region.TileZ = region.MinWaterZ;
            }
            else
            {
                PuddleSpawnOptions.ApplyDefaultHeightMask(options, world);
                if (options.UseHeightMask)
                {
                    region.UseHeightMask = true;
                    region.MaxWaterZ = options.MaxWaterZ;
                    region.MinWaterZ = PuddleRenderer.ComputeMinGroundZ(region, world);
                    region.TileZ = region.MinWaterZ;
                }
            }

            PuddleManager.RegisterDynamicExpansion(region);
        }

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
            region.SurfaceShimmerStrength = options.SurfaceShimmerStrength;
            region.EdgeRippleStrength = options.EdgeRippleStrength;
            region.DynamicExpansionEnabled = options.DynamicExpansionEnabled;

            if (options.DynamicExpansionEnabled)
            {
                ApplyDynamicExpansion(region, options, world);
            }
            else
            {
                PuddleSpawnOptions.ApplyDefaultHeightMask(options, world);
                if (options.UseHeightMask)
                {
                    region.UseHeightMask = true;
                    region.MaxWaterZ = options.MaxWaterZ;
                    region.MaskDirty = true;
                    region.MinWaterZ = PuddleRenderer.ComputeMinGroundZ(region, world);
                    region.TileZ = region.MinWaterZ;
                }
            }

            string heightInfo = region.UseHeightMask
                ? string.Format(CultureInfo.InvariantCulture, " minZ={0} maxWaterZ={1}", region.MinWaterZ, region.MaxWaterZ)
                : string.Empty;

            string dynamicInfo = region.DynamicExpansionEnabled
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    " dynamic[r={0:0}-{1:0} step={2:0}@{3:0.0}s z={4}-{5} step={6}@{7:0.0}s]",
                    region.DynamicMinRadius,
                    region.DynamicMaxRadius,
                    region.DynamicRadiusStep,
                    region.DynamicRadiusIntervalSeconds,
                    region.DynamicMinWaterZ,
                    region.DynamicMaxWaterZ,
                    region.DynamicWaterZStep,
                    region.DynamicWaterZIntervalSeconds)
                : string.Empty;

            message = string.Format(
                CultureInfo.InvariantCulture,
                "Puddle #{0} at ({1},{2},{3}) radius={4:0} alpha={5:0.00} reflect={6:0.00} wave={7:0.000} speed={8:0.0} scale={9:0}{10}{11}.",
                region.Id,
                region.TileX,
                region.TileY,
                region.TileZ,
                region.Radius,
                options.Alpha,
                options.ReflectStrength,
                options.WaveStrength,
                options.WaveSpeed,
                options.WaveScale,
                heightInfo,
                dynamicInfo
            );

            return region;
        }
    }
}
