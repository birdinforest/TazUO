// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Globalization;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>Dev UI for spawning ground puddles with full parameter control.</summary>
    public sealed class PuddleSetupGump : Gump
    {
        private const ushort HUE = 0xFFFF;
        private const int LABEL_W = 158;
        private const int FIELD_W = 132;
        private const int ROW_H = 28;
        private const int GUMP_W = 360;
        private const int GUMP_H = 876;

        private enum ButtonId
        {
            Spawn = 1,
            PlayerPos = 2,
            Clear = 3,
            Close = 4
        }

        private readonly World _world;
        private readonly StbTextBox _tileX;
        private readonly StbTextBox _tileY;
        private readonly StbTextBox _tileZ;
        private readonly StbTextBox _radius;
        private readonly StbTextBox _alpha;
        private readonly StbTextBox _reflect;
        private readonly StbTextBox _waveStrength;
        private readonly StbTextBox _waveSpeed;
        private readonly StbTextBox _waveScale;
        private readonly StbTextBox _pivotStableBand;
        private readonly StbTextBox _pivotHorizontalRipple;
        private readonly StbTextBox _surfaceShimmer;
        private readonly StbTextBox _edgeRipple;
        private readonly StbTextBox _maxWaterZ;
        private readonly Checkbox _dynamicExpansion;
        private readonly StbTextBox _dynamicMinWaterZ;
        private readonly StbTextBox _dynamicMaxWaterZ;
        private readonly StbTextBox _dynamicWaterZStep;
        private readonly StbTextBox _dynamicWaterZInterval;
        private readonly StbTextBox _dynamicMinRadius;
        private readonly StbTextBox _dynamicMaxRadius;
        private readonly StbTextBox _dynamicRadiusStep;
        private readonly StbTextBox _dynamicRadiusInterval;

        public PuddleSetupGump(World world)
            : base(world, 0, 0)
        {
            _world = world;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;

            X = 120;
            Y = 40;
            Width = GUMP_W;
            Height = GUMP_H;

            Add(new AlphaBlendControl { X = 0, Y = 0, Width = GUMP_W, Height = GUMP_H, Alpha = 0.88f });

            Add(new Label("Ground Puddle Setup", true, 0x44, 0, 255, FontStyle.BlackBorder) { X = 10, Y = 8 });
            Add(new Label("(shimmer/edge = surface only; wave = reflection UV)", false, 0x35, 0, 255, FontStyle.BlackBorder) { X = 10, Y = 24 });
            Add(new Label("(blank maxWaterZ = terrain clip at tile Z+1)", false, 0x35, 0, 255, FontStyle.BlackBorder) { X = 10, Y = 38 });

            PuddleSpawnOptions defaults = world.Player != null
                ? PuddleSpawnOptions.FromPlayer(world.Player.X, world.Player.Y, world.Player.Z)
                : new PuddleSpawnOptions();

            sbyte groundZ = world.Player != null && world.Map != null
                ? world.Map.GetTileZ(world.Player.X, world.Player.Y)
                : (sbyte)defaults.TileZ;

            int y = 60;
            _tileX = AddRow(ref y, "Tile X", defaults.TileX.ToString(CultureInfo.InvariantCulture));
            _tileY = AddRow(ref y, "Tile Y", defaults.TileY.ToString(CultureInfo.InvariantCulture));
            _tileZ = AddRow(ref y, "Tile Z", defaults.TileZ.ToString(CultureInfo.InvariantCulture));
            _radius = AddRow(ref y, "Radius (px)", defaults.Radius.ToString(CultureInfo.InvariantCulture));
            _alpha = AddRow(ref y, "Alpha (0-1)", defaults.Alpha.ToString("0.00", CultureInfo.InvariantCulture));
            _reflect = AddRow(ref y, "Reflect strength", defaults.ReflectStrength.ToString("0.00", CultureInfo.InvariantCulture));
            _waveStrength = AddRow(ref y, "Wave strength", defaults.WaveStrength.ToString("0.000", CultureInfo.InvariantCulture));
            _waveSpeed = AddRow(ref y, "Wave speed", defaults.WaveSpeed.ToString("0.0", CultureInfo.InvariantCulture));
            _waveScale = AddRow(ref y, "Wave scale", defaults.WaveScale.ToString("0", CultureInfo.InvariantCulture));
            _pivotStableBand = AddRow(ref y, "Pivot stable band", defaults.PivotStableBand.ToString("0.000", CultureInfo.InvariantCulture));
            _pivotHorizontalRipple = AddRow(ref y, "Pivot horiz ripple", defaults.PivotHorizontalRipple.ToString("0.00", CultureInfo.InvariantCulture));
            _surfaceShimmer = AddRow(ref y, "Surface shimmer", defaults.SurfaceShimmerStrength.ToString("0.000", CultureInfo.InvariantCulture));
            _edgeRipple = AddRow(ref y, "Edge ripple", defaults.EdgeRippleStrength.ToString("0.000", CultureInfo.InvariantCulture));
            _maxWaterZ = AddRow(ref y, "Max water Z", string.Empty);

            Add(new Label("Dynamic expansion", true, 0x44, 0, 255, FontStyle.BlackBorder) { X = 10, Y = y + 4 });
            y += ROW_H;

            _dynamicExpansion = new Checkbox(0x00D2, 0x00D3, "Enable dynamic expansion", 1, HUE)
            {
                X = 10,
                Y = y + 2,
                IsChecked = false
            };
            Add(_dynamicExpansion);
            y += ROW_H;

            _dynamicMinWaterZ = AddRow(ref y, "Dyn min water Z", groundZ.ToString(CultureInfo.InvariantCulture));
            _dynamicMaxWaterZ = AddRow(ref y, "Dyn max water Z", (groundZ + 2).ToString(CultureInfo.InvariantCulture));
            _dynamicWaterZStep = AddRow(ref y, "Dyn water Z step", defaults.DynamicWaterZStep.ToString(CultureInfo.InvariantCulture));
            _dynamicWaterZInterval = AddRow(ref y, "Dyn water Z interval (s)", defaults.DynamicWaterZIntervalSeconds.ToString("0.0", CultureInfo.InvariantCulture));
            _dynamicMinRadius = AddRow(ref y, "Dyn min radius", defaults.DynamicMinRadius.ToString("0", CultureInfo.InvariantCulture));
            _dynamicMaxRadius = AddRow(ref y, "Dyn max radius", defaults.DynamicMaxRadius.ToString("0", CultureInfo.InvariantCulture));
            _dynamicRadiusStep = AddRow(ref y, "Dyn radius step (px)", defaults.DynamicRadiusStep.ToString("0", CultureInfo.InvariantCulture));
            _dynamicRadiusInterval = AddRow(ref y, "Dyn radius interval (s)", defaults.DynamicRadiusIntervalSeconds.ToString("0.0", CultureInfo.InvariantCulture));

            int by = GUMP_H - 36;
            Add(new NiceButton(10, by, 100, 25, ButtonAction.Activate, "Spawn") { ButtonParameter = (int)ButtonId.Spawn });
            Add(new NiceButton(118, by, 100, 25, ButtonAction.Activate, "Player pos") { ButtonParameter = (int)ButtonId.PlayerPos });
            Add(new NiceButton(226, by, 60, 25, ButtonAction.Activate, "Clear") { ButtonParameter = (int)ButtonId.Clear });
            Add(new NiceButton(292, by, 58, 25, ButtonAction.Activate, "Close") { ButtonParameter = (int)ButtonId.Close });
        }

        private StbTextBox AddRow(ref int y, string label, string value)
        {
            int fx = 10 + LABEL_W;

            Add(new Label(label, true, HUE, 0, 255, FontStyle.BlackBorder) { X = 10, Y = y + 4 });

            Add(new ResizePic(0x0BB8) { X = fx, Y = y, Width = FIELD_W, Height = 22 });

            var box = new StbTextBox(0xFF, 24, FIELD_W, true, FontStyle.BlackBorder | FontStyle.Fixed)
            {
                X = fx,
                Y = y,
                Width = FIELD_W,
                Height = 22,
                Text = value
            };
            Add(box);

            y += ROW_H;
            return box;
        }

        public override void OnButtonClick(int buttonId)
        {
            switch ((ButtonId)buttonId)
            {
                case ButtonId.Spawn:
                    SpawnFromFields();
                    break;

                case ButtonId.PlayerPos:
                    ResetToPlayer();
                    break;

                case ButtonId.Clear:
                    PuddleManager.Clear();
                    GameActions.Print(_world, "All puddles cleared.", 68);
                    break;

                case ButtonId.Close:
                    Dispose();
                    break;
            }
        }

        private void ResetToPlayer()
        {
            if (_world.Player == null)
            {
                return;
            }

            _tileX.SetText(_world.Player.X.ToString(CultureInfo.InvariantCulture));
            _tileY.SetText(_world.Player.Y.ToString(CultureInfo.InvariantCulture));
            _tileZ.SetText(_world.Player.Z.ToString(CultureInfo.InvariantCulture));

            if (_world.Map != null)
            {
                sbyte groundZ = _world.Map.GetTileZ(_world.Player.X, _world.Player.Y);
                _dynamicMinWaterZ.SetText(groundZ.ToString(CultureInfo.InvariantCulture));
                _dynamicMaxWaterZ.SetText((groundZ + 2).ToString(CultureInfo.InvariantCulture));
            }
        }

        private void SpawnFromFields()
        {
            if (!TryBuildOptions(out PuddleSpawnOptions options, out string error))
            {
                GameActions.Print(_world, error, 33);
                return;
            }

            if (PuddleSpawn.TryCreate(_world, options, out string message) != null)
            {
                GameActions.Print(_world, message, 68);
            }
            else
            {
                GameActions.Print(_world, message, 33);
            }
        }

        private bool TryBuildOptions(out PuddleSpawnOptions options, out string error)
        {
            options = new PuddleSpawnOptions();
            error = string.Empty;

            if (!int.TryParse(_tileX.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tileX))
            {
                error = "Invalid Tile X.";
                return false;
            }

            if (!int.TryParse(_tileY.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tileY))
            {
                error = "Invalid Tile Y.";
                return false;
            }

            if (!int.TryParse(_tileZ.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tileZ))
            {
                error = "Invalid Tile Z.";
                return false;
            }

            if (!float.TryParse(_radius.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float radius))
            {
                error = "Invalid radius.";
                return false;
            }

            if (!float.TryParse(_alpha.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float alpha))
            {
                error = "Invalid alpha.";
                return false;
            }

            if (!float.TryParse(_reflect.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float reflect))
            {
                error = "Invalid reflect strength.";
                return false;
            }

            if (!float.TryParse(_waveStrength.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float waveStrength))
            {
                error = "Invalid wave strength.";
                return false;
            }

            if (!float.TryParse(_waveSpeed.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float waveSpeed))
            {
                error = "Invalid wave speed.";
                return false;
            }

            if (!float.TryParse(_waveScale.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float waveScale))
            {
                error = "Invalid wave scale.";
                return false;
            }

            if (!float.TryParse(_pivotStableBand.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float pivotStableBand))
            {
                error = "Invalid pivot stable band.";
                return false;
            }

            if (!float.TryParse(_pivotHorizontalRipple.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float pivotHorizontalRipple))
            {
                error = "Invalid pivot horizontal ripple.";
                return false;
            }

            if (!float.TryParse(_surfaceShimmer.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float surfaceShimmer))
            {
                error = "Invalid surface shimmer.";
                return false;
            }

            if (!float.TryParse(_edgeRipple.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float edgeRipple))
            {
                error = "Invalid edge ripple.";
                return false;
            }

            options.TileX = tileX;
            options.TileY = tileY;
            options.TileZ = tileZ;
            options.Radius = Math.Max(1f, radius);
            options.Alpha = Math.Clamp(alpha, 0f, 1f);
            options.ReflectStrength = Math.Clamp(reflect, 0f, 1f);
            options.WaveStrength = Math.Max(0f, waveStrength);
            options.WaveSpeed = Math.Max(0.001f, waveSpeed);
            options.WaveScale = Math.Max(1f, waveScale);
            options.PivotStableBand = Math.Max(0.0001f, pivotStableBand);
            options.PivotHorizontalRipple = Math.Clamp(pivotHorizontalRipple, 0f, 1f);
            options.SurfaceShimmerStrength = Math.Max(0f, surfaceShimmer);
            options.EdgeRippleStrength = Math.Max(0f, edgeRipple);
            options.DynamicExpansionEnabled = _dynamicExpansion.IsChecked;

            if (options.DynamicExpansionEnabled)
            {
                if (!sbyte.TryParse(_dynamicMinWaterZ.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte dynamicMinWaterZ))
                {
                    error = "Invalid dynamic min water Z.";
                    return false;
                }

                if (!sbyte.TryParse(_dynamicMaxWaterZ.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte dynamicMaxWaterZ))
                {
                    error = "Invalid dynamic max water Z.";
                    return false;
                }

                if (!sbyte.TryParse(_dynamicWaterZStep.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte dynamicWaterZStep))
                {
                    error = "Invalid dynamic water Z step.";
                    return false;
                }

                if (!float.TryParse(_dynamicWaterZInterval.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float dynamicWaterZInterval))
                {
                    error = "Invalid dynamic water Z interval.";
                    return false;
                }

                if (!float.TryParse(_dynamicMinRadius.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float dynamicMinRadius))
                {
                    error = "Invalid dynamic min radius.";
                    return false;
                }

                if (!float.TryParse(_dynamicMaxRadius.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float dynamicMaxRadius))
                {
                    error = "Invalid dynamic max radius.";
                    return false;
                }

                if (!float.TryParse(_dynamicRadiusStep.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float dynamicRadiusStep))
                {
                    error = "Invalid dynamic radius step.";
                    return false;
                }

                if (!float.TryParse(_dynamicRadiusInterval.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float dynamicRadiusInterval))
                {
                    error = "Invalid dynamic radius interval.";
                    return false;
                }

                options.DynamicMinWaterZ = dynamicMinWaterZ;
                options.DynamicMaxWaterZ = dynamicMaxWaterZ;
                options.DynamicWaterZStep = dynamicWaterZStep;
                options.DynamicWaterZIntervalSeconds = Math.Max(0f, dynamicWaterZInterval);
                options.DynamicMinRadius = Math.Max(1f, dynamicMinRadius);
                options.DynamicMaxRadius = Math.Max(1f, dynamicMaxRadius);
                options.DynamicRadiusStep = Math.Max(0f, dynamicRadiusStep);
                options.DynamicRadiusIntervalSeconds = Math.Max(0f, dynamicRadiusInterval);

                bool radiusActive = options.DynamicRadiusStep > 0f
                    && options.DynamicRadiusIntervalSeconds > 0f
                    && options.DynamicMinRadius < options.DynamicMaxRadius;
                bool waterZActive = options.DynamicWaterZStep > 0
                    && options.DynamicWaterZIntervalSeconds > 0f
                    && options.DynamicMinWaterZ < options.DynamicMaxWaterZ;

                if (!radiusActive && !waterZActive)
                {
                    error = "Enable at least one timed step (radius or water Z with step > 0 and interval > 0).";
                    return false;
                }
            }
            else
            {
                string maxZText = _maxWaterZ.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(maxZText))
                {
                    if (!sbyte.TryParse(maxZText, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte maxWaterZ))
                    {
                        error = "Invalid max water Z (sbyte).";
                        return false;
                    }

                    options.UseHeightMask = true;
                    options.MaxWaterZ = maxWaterZ;
                }
            }

            return true;
        }
    }
}
