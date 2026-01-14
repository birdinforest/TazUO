// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Game.Combat;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using ClassicUO.Assets;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Visual indicator for charged shot progress.
    ///
    /// Display:
    /// - Progress bar (120x12 pixels)
    /// - Yellow while charging, Green when ready
    /// - Text: "Charging... 67%" or "READY!"
    /// - Positioned below player character (center-bottom of screen)
    ///
    /// Lifecycle:
    /// - Created when ChargedShotOperation enters Preparing state
    /// - Updated every frame to show progress
    /// - Auto-disposed when operation completes/cancels
    /// </summary>
    internal class ChargedShotIndicator : Gump, IDisposable
    {
        private const int BAR_WIDTH = 120;
        private const int BAR_HEIGHT = 12;
        private const int BAR_BORDER = 1;
        private const int TEXT_OFFSET_Y = -20; // Text above bar

        private readonly ChargedShotOperation _operation;
        private readonly ChargedShotInfo _info;

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

            // Position near player character (center-bottom of screen)
            if (World.Player != null)
            {
                X = (Client.Game.Window.ClientBounds.Width / 2) - (BAR_WIDTH / 2);
                Y = (Client.Game.Window.ClientBounds.Height / 2) + 60; // Below character
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

            // Draw background (dark with transparency)
            var bgRect = new Rectangle(x, y, BAR_WIDTH, BAR_HEIGHT);
            batcher.Draw(
                SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 180)),
                bgRect,
                hueVector
            );

            // Draw progress fill
            int fillWidth = (int)((BAR_WIDTH - 2 * BAR_BORDER) * progress);
            var fillRect = new Rectangle(
                x + BAR_BORDER,
                y + BAR_BORDER,
                fillWidth,
                BAR_HEIGHT - 2 * BAR_BORDER
            );

            // Color: Yellow charging, Green ready
            Color fillColor = progress >= 1.0f
                ? new Color(0, 255, 0, 220)    // Green
                : new Color(255, 255, 0, 220); // Yellow

            batcher.Draw(
                SolidColorTextureCache.GetTexture(fillColor),
                fillRect,
                hueVector
            );

            // Draw border (white)
            DrawBorder(batcher, bgRect, Color.White, hueVector);

            // Draw text
            string text = progress >= 1.0f
                ? "READY!"
                : $"Charging... {(progress * 100):F0}%";

            // Draw text above bar
            // Note: Using simple text rendering - TazUO font system integration might be needed
            DrawText(batcher, text, x + BAR_WIDTH / 2, y + TEXT_OFFSET_Y, Color.White);

            return true;
        }

        private void DrawBorder(UltimaBatcher2D batcher, Rectangle rect, Color color, Vector3 hueVector)
        {
            // Top
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X, rect.Y, rect.Width, 1),
                hueVector
            );
            // Bottom
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1),
                hueVector
            );
            // Left
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X, rect.Y, 1, rect.Height),
                hueVector
            );
            // Right
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(rect.X + rect.Width - 1, rect.Y, 1, rect.Height),
                hueVector
            );
        }

        private void DrawText(UltimaBatcher2D batcher, string text, int x, int y, Color color)
        {
            // Placeholder for text rendering
            // TODO: Implement using TazUO's actual font rendering system
            // This would typically use something like:
            var renderedText = RenderedText.Create(text, (ushort)color.PackedValue, 0, true, FontStyle.None, TEXT_ALIGN_TYPE.TS_CENTER, 0, 30, false, false);
            renderedText.Draw(batcher, x, y);

            // For now, this is a placeholder that doesn't render text
            // The progress bar visualization is sufficient for functionality
        }

        void IDisposable.Dispose()
        {
            base.Dispose();
        }
    }
}

