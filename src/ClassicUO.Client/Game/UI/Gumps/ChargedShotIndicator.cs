// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Game.Combat;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Scenes;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using ClassicUO.Assets;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Visual indicator for charged shot progress in traditional UO styling.
    ///
    /// Display:
    /// - Progress bar (140x16 pixels) with traditional UO colors
    /// - Amber/orange while charging, darker green when ready
    /// - Text with black border: "Charging... 67%" or "READY!"
    /// - Positioned below player character (center-bottom of screen)
    ///
    /// Lifecycle:
    /// - Created when ChargedShotOperation enters Preparing state
    /// - Updated every frame to show progress
    /// - Auto-disposed when operation completes/cancels
    /// </summary>
    internal class ChargedShotIndicator : Gump, IDisposable
    {
        private const int BAR_WIDTH = 140;
        private const int BAR_HEIGHT = 16;
        private const int BAR_BORDER = 2;
        private const int TEXT_OFFSET_Y = -22; // Text above bar

        // Traditional UO colors (darker, more muted)
        private static readonly Color UO_BG_COLOR = new Color(20, 20, 20, 220);      // Dark gray background
        private static readonly Color UO_BORDER_COLOR = new Color(100, 100, 100, 255); // Gray border
        private static readonly Color UO_CHARGING_COLOR = new Color(200, 120, 0, 255);  // Amber/orange (traditional UO gold)
        private static readonly Color UO_READY_COLOR = new Color(0, 150, 0, 255);       // Dark green (traditional UO)
        private static readonly Color UO_TEXT_COLOR = new Color(255, 255, 200, 255);    // Light yellow/cream text

        private readonly ChargedShotOperation _operation;
        private readonly ChargedShotInfo _info;
        private RenderedText _renderedText;

        public ChargedShotIndicator(World world, ChargedShotOperation operation)
            : base(world, 0, 0)
        {
            _operation = operation;
            _info = (ChargedShotInfo)operation.Info;

            CanMove = false;
            AcceptMouseInput = false;
            CanCloseWithRightClick = false;
            CanCloseWithEsc = false;

            // Ensure visibility
            IsVisible = true;
            IsEnabled = true;
        }

        public override void Update()
        {
            base.Update();

            // Auto-dispose when operation ends
            if (_operation.CurrentState == SpecialCombatOperationState.Completed ||
                _operation.CurrentState == SpecialCombatOperationState.Canceled ||
                _operation.CurrentState == SpecialCombatOperationState.Interrupted)
            {
                Dispose();
                return;
            }

            // Position near player character (center-bottom of game view)
            if (World.Player != null && GameScene.Instance != null)
            {
                // Get the game viewport bounds (Camera.Bounds)
                Rectangle viewportBounds = GameScene.Instance.Camera.Bounds;

                // Calculate center of viewport
                int viewportCenterX = viewportBounds.X + (viewportBounds.Width / 2);
                int viewportCenterY = viewportBounds.Y + (viewportBounds.Height / 2);

                // Position indicator at center-bottom of viewport
                X = viewportCenterX - (BAR_WIDTH / 2);
                Y = viewportCenterY + 60; // Below character
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            // Call base.Draw first (it checks IsVisible)
            if (!base.Draw(batcher, x, y))
                return false;

            // Calculate progress based on charge time
            TimeSpan elapsed = DateTime.Now - _info.StartTime;
            float progress = Math.Min((float)elapsed.TotalSeconds / _info.ChargeTime, 1.0f);

            // Use server's fullyCharged flag if available
            if (_info.FullyCharged)
                progress = 1.0f;

            // Create hue vector for rendering (required for shader)
            Vector3 hueVector = ShaderHueTranslator.GetHueVector(0, false, 1f, true);

            // Draw background (traditional UO dark gray)
            var bgRect = new Rectangle(x, y, BAR_WIDTH, BAR_HEIGHT);
            batcher.Draw(
                SolidColorTextureCache.GetTexture(UO_BG_COLOR),
                bgRect,
                hueVector
            );

            // Draw progress fill
            int fillWidth = (int)((BAR_WIDTH - 2 * BAR_BORDER) * progress);
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(
                    x + BAR_BORDER,
                    y + BAR_BORDER,
                    fillWidth,
                    BAR_HEIGHT - 2 * BAR_BORDER
                );

                // Traditional UO colors: Amber while charging, dark green when ready
                Color fillColor = progress >= 1.0f
                    ? UO_READY_COLOR
                    : UO_CHARGING_COLOR;

                batcher.Draw(
                    SolidColorTextureCache.GetTexture(fillColor),
                    fillRect,
                    hueVector
                );
            }

            // Draw border (traditional UO gray border)
            DrawBorder(batcher, bgRect, UO_BORDER_COLOR, hueVector);

            // Draw text with traditional UO styling
            string text = progress >= 1.0f
                ? "READY!"
                : $"Charging... {(progress * 100):F0}%";

            DrawText(batcher, text, x + BAR_WIDTH / 2, y + TEXT_OFFSET_Y);

            return true;
        }

        private void DrawBorder(UltimaBatcher2D batcher, Rectangle rect, Color color, Vector3 hueVector)
        {
            // Traditional UO border: 2-pixel border with darker outer edge
            int borderSize = 2;

            // Outer border (darker)
            var outerBorder = new Color(
                Math.Max(0, color.R - 30),
                Math.Max(0, color.G - 30),
                Math.Max(0, color.B - 30),
                255
            );

            // Outer border
            batcher.Draw(
                SolidColorTextureCache.GetTexture(outerBorder),
                new Rectangle(rect.X, rect.Y, rect.Width, borderSize),
                hueVector
            );
            batcher.Draw(
                SolidColorTextureCache.GetTexture(outerBorder),
                new Rectangle(rect.X, rect.Y + rect.Height - borderSize, rect.Width, borderSize),
                hueVector
            );
            batcher.Draw(
                SolidColorTextureCache.GetTexture(outerBorder),
                new Rectangle(rect.X, rect.Y, borderSize, rect.Height),
                hueVector
            );
            batcher.Draw(
                SolidColorTextureCache.GetTexture(outerBorder),
                new Rectangle(rect.X + rect.Width - borderSize, rect.Y, borderSize, rect.Height),
                hueVector
            );

            // Inner border (lighter)
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X + borderSize, rect.Y + borderSize, rect.Width - 2 * borderSize, 1),
                hueVector
            );
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X + borderSize, rect.Y + rect.Height - borderSize - 1, rect.Width - 2 * borderSize, 1),
                hueVector
            );
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X + borderSize, rect.Y + borderSize, 1, rect.Height - 2 * borderSize),
                hueVector
            );
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X + rect.Width - borderSize - 1, rect.Y + borderSize, 1, rect.Height - 2 * borderSize),
                hueVector
            );
        }

        private void DrawText(UltimaBatcher2D batcher, string text, int x, int y)
        {
            // Traditional UO text rendering with black border
            if (_renderedText == null || _renderedText.Text != text)
            {
                _renderedText?.Destroy();
                _renderedText = RenderedText.Create(
                    text,
                    (ushort)UO_TEXT_COLOR.PackedValue,
                    0,
                    true,
                    FontStyle.BlackBorder,
                    TEXT_ALIGN_TYPE.TS_CENTER,
                    BAR_WIDTH,
                    0,
                    false,
                    false
                );
            }

            if (_renderedText != null)
            {
                // Center the text horizontally
                int textX = x - (_renderedText.Width / 2);
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0, false, 1f, true);
                _renderedText.Draw(batcher, textX, y, hueVector.Z);
            }
        }

        void IDisposable.Dispose()
        {
            _renderedText?.Destroy();
            _renderedText = null;
            base.Dispose();
        }
    }
}

