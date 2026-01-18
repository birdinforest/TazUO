// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Combat
{
    /// <summary>
    /// Client-side projectile debug visualizer.
    /// Renders collision markers locally without network packets.
    /// Controlled by CUOEnviroment.Debug (master toggle) and Profile config (sub-toggles).
    /// </summary>
    public static class ProjectileDebugVisualizer
    {
        private static List<DebugMarker> _activeMarkers = new List<DebugMarker>();
        private static List<DebugLine> _activeLines = new List<DebugLine>();

        public static bool IsEnabled => CUOEnviroment.Debug &&
                                        ProfileManager.CurrentProfile?.DebugVisualizeProjectileCollisions == true;

        public static bool ShowTrajectory => CUOEnviroment.Debug &&
                                             ProfileManager.CurrentProfile?.DebugShowProjectileTrajectory == true;

        public static bool ShowDirectionLine => CUOEnviroment.Debug &&
                                                ProfileManager.CurrentProfile?.DebugShowDirectionLine == true;

        /// <summary>
        /// Called when a MovingEffect ends (approximate collision point)
        /// </summary>
        public static void OnMovingEffectEnd(ClassicUO.Game.GameObjects.Point3D location, ushort graphic)
        {
            Console.WriteLine($"[ProjectileDebugVisualizer] OnMovingEffectEnd:isEnabled: {IsEnabled}, profile: {ProfileManager.CurrentProfile?.DebugVisualizeProjectileCollisions}, \nlocation: {location}, graphic: {graphic}");
            if (!IsEnabled)
                return;

            // Create local visual marker (no network packets)
            var marker = new DebugMarker(
                location,
                GetColorForEffectType(graphic),
                ProfileManager.CurrentProfile?.DebugCollisionMarkerDuration ?? 3000
            );

            _activeMarkers.Add(marker);

        }

        /// <summary>
        /// Add a debug direction line from server
        /// </summary>
        /// <param name="startLocation">Start point</param>
        /// <param name="endLocation">End point</param>
        /// <param name="isCursorLine">If true, renders as cursor line (yellow), else arrow direction (cyan)</param>
        public static void AddDirectionLine(GameObjects.Point3D startLocation, GameObjects.Point3D endLocation, bool isCursorLine = false)
        {
            if (!ShowDirectionLine)
            {
                return;
            }

            int duration = ProfileManager.CurrentProfile?.DebugCollisionMarkerDuration ?? 5000;
            var line = new DebugLine(startLocation, endLocation, duration, isCursorLine);
            _activeLines.Add(line);
        }

        /// <summary>
        /// Add a client-calculated character-to-cursor line for comparison.
        /// This line uses client-side calculated positions independently from server.
        /// </summary>
        /// <param name="characterLocation">Character's world position (from client)</param>
        /// <param name="cursorLocation">Cursor's world position (calculated by client)</param>
        public static void AddClientCalculatedLine(GameObjects.Point3D characterLocation, GameObjects.Point3D cursorLocation)
        {
            if (!ShowDirectionLine)
            {
                return;
            }

            int duration = ProfileManager.CurrentProfile?.DebugCollisionMarkerDuration ?? 5000;
            var line = new DebugLine(characterLocation, cursorLocation, duration, false, true); // Use green color for client-calculated line
            _activeLines.Add(line);
        }

        /// <summary>
        /// Called each frame to update/render markers
        /// </summary>
        public static void Update()
        {
            if (!IsEnabled)
            {
                _activeMarkers.Clear();
            }

            // Update and remove expired markers
            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                if (_activeMarkers[i].IsExpired)
                {
                    _activeMarkers.RemoveAt(i);
                }
            }

            // Update and remove expired lines
            if (!ShowDirectionLine)
            {
                _activeLines.Clear();
            }
            else
            {
                for (int i = _activeLines.Count - 1; i >= 0; i--)
                {
                    if (_activeLines[i].IsExpired)
                    {
                        _activeLines.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Render debug markers and lines on screen (called from GameScene.Draw)
        /// </summary>
        public static void Render(UltimaBatcher2D batcher, Point cameraOffset)
        {
            // Render markers
            if (IsEnabled && _activeMarkers.Count > 0)
            {
                foreach (DebugMarker marker in _activeMarkers)
                {
                    marker.Render(batcher, cameraOffset);
                }
            }

            // Render direction lines
            if (ShowDirectionLine && _activeLines.Count > 0)
            {
                foreach (DebugLine line in _activeLines)
                {
                    line.Render(batcher, cameraOffset);
                }
            }
        }

        private static Color GetColorForEffectType(ushort graphic) =>
            // Approximate collision type by effect graphic
            // Arrow effects are typically 0x0F42-0x0F50
            graphic >= 0x0F42 && graphic <= 0x0F50
                ? new Color(255, 0, 0, 180)    // Red for arrows
                : new Color(0, 255, 255, 180); // Cyan for other effects

        private class DebugMarker
        {
            private GameObjects.Point3D _location;
            private Color _color;
            private long _expirationTime;
            private const int MARKER_SIZE = 10;

            public DebugMarker(GameObjects.Point3D location, Color color, int durationMs)
            {
                _location = location;
                _color = color;
                _expirationTime = Time.Ticks + durationMs;
            }

            public bool IsExpired => Time.Ticks >= _expirationTime;

            public void Render(UltimaBatcher2D batcher, Point cameraOffset)
            {
                // Convert world coordinates to screen coordinates (isometric projection)
                // Match UpdateRealScreenPosition formula: screenX = (worldX - worldY) * 22 - offsetX - 22
                int screenX = (_location.X - _location.Y) * 22 - cameraOffset.X - 22;
                int screenY = (_location.X + _location.Y) * 22 - _location.Z * 4 - cameraOffset.Y - 22;

                // Fade out over time
                float remainingTime = _expirationTime - Time.Ticks;
                float alpha = Math.Min(1.0f, remainingTime / 1000.0f);
                var fadedColor = new Color(_color.R, _color.G, _color.B, (byte)(_color.A * alpha));

                // Create hue vector for rendering (required for shader)
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0, false, alpha, true);

                // Draw marker (filled square)
                batcher.Draw(
                    SolidColorTextureCache.GetTexture(fadedColor),
                    new Rectangle(
                        screenX - MARKER_SIZE / 2,
                        screenY - MARKER_SIZE / 2,
                        MARKER_SIZE,
                        MARKER_SIZE
                    ),
                    hueVector
                );
            }
        }

        private class DebugLine
        {
            private GameObjects.Point3D _startLocation;
            private GameObjects.Point3D _endLocation;
            private long _expirationTime;
            private Color _color;
            private const int LINE_WIDTH = 2;
            private bool _isClientCalculated;
            private bool _isCursorLine;

            public DebugLine(GameObjects.Point3D startLocation, GameObjects.Point3D endLocation, int durationMs, bool isCursorLine = false, bool isClientCalculated = false)
            {
                _startLocation = startLocation;
                _endLocation = endLocation;
                _expirationTime = Time.Ticks + durationMs;
                _isClientCalculated = isClientCalculated;
                _isCursorLine = isCursorLine;

                // Use different colors:
                // - Green for client-calculated line (independent from server)
                // - Yellow for server-sent cursor line
                // - Cyan for server-sent arrow direction line
                if (isClientCalculated)
                {
                    _color = new Color(0, 255, 0, 200); // Green for client-calculated line
                }
                else if (isCursorLine)
                {
                    _color = new Color(255, 255, 0, 200);  // Yellow for cursor line
                }
                else
                {
                    _color = new Color(0, 255, 255, 200); // Cyan for arrow direction line
                }
            }

            public bool IsExpired => Time.Ticks >= _expirationTime;

            public void Render(UltimaBatcher2D batcher, Point cameraOffset)
            {
                // Convert world coordinates to screen coordinates (isometric projection)
                // Match UpdateRealScreenPosition formula: screenX = (worldX - worldY) * 22 - offsetX - 22
                int startScreenX = (_startLocation.X - _startLocation.Y) * 22 - cameraOffset.X - 22;
                int startScreenY = (_startLocation.X + _startLocation.Y) * 22 - _startLocation.Z * 4 - cameraOffset.Y - 22;
                int endScreenX = (_endLocation.X - _endLocation.Y) * 22 - cameraOffset.X - 22;
                int endScreenY = (_endLocation.X + _endLocation.Y) * 22 - _endLocation.Z * 4 - cameraOffset.Y - 22;

                // Add slight perpendicular offset to yellow line (server) so it's visible when overlapping with green line (client)
                // This allows us to verify they calculate the same values or identify differences
                if (_isCursorLine && !_isClientCalculated)
                {
                    // Calculate perpendicular offset (rotate line direction 90 degrees)
                    float dx = endScreenX - startScreenX;
                    float dy = endScreenY - startScreenY;
                    float length = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (length > 0.1f)
                    {
                        // Perpendicular vector (rotate 90 degrees): (-dy, dx)
                        float perpX = -dy / length;
                        float perpY = dx / length;
                        float offsetAmount = 3.0f; // 3 pixels offset

                        startScreenX += (int)(perpX * offsetAmount);
                        startScreenY += (int)(perpY * offsetAmount);
                        endScreenX += (int)(perpX * offsetAmount);
                        endScreenY += (int)(perpY * offsetAmount);
                    }
                }

                // Fade out over time
                float remainingTime = _expirationTime - Time.Ticks;
                float alpha = Math.Min(1.0f, remainingTime / 1000.0f);
                var fadedColor = new Color(_color.R, _color.G, _color.B, (byte)(_color.A * alpha));

                // Create hue vector for rendering
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0, false, alpha, true);

                // Draw line using rectangles (simple line rendering)
                DrawLine(batcher, startScreenX, startScreenY, endScreenX, endScreenY, fadedColor, hueVector);
            }

            private void DrawLine(UltimaBatcher2D batcher, int x1, int y1, int x2, int y2, Color color, Vector3 hueVector)
            {
                float dx = x2 - x1;
                float dy = y2 - y1;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);

                if (length < 1.0f)
                {
                    // Draw a dot if line is too short
                    batcher.Draw(
                        SolidColorTextureCache.GetTexture(color),
                        new Rectangle(x1 - LINE_WIDTH / 2, y1 - LINE_WIDTH / 2, LINE_WIDTH, LINE_WIDTH),
                        hueVector
                    );
                    return;
                }

                // Draw line using multiple small rectangles (segments)
                int segments = Math.Max(1, (int)(length / 5)); // One segment every 5 pixels
                float segDx = dx / segments;
                float segDy = dy / segments;

                for (int i = 0; i < segments; i++)
                {
                    int segX1 = (int)(x1 + segDx * i);
                    int segY1 = (int)(y1 + segDy * i);
                    int segX2 = (int)(x1 + segDx * (i + 1));
                    int segY2 = (int)(y1 + segDy * (i + 1));

                    float segLength = (float)Math.Sqrt((segX2 - segX1) * (segX2 - segX1) + (segY2 - segY1) * (segY2 - segY1));
                    if (segLength < 0.5f)
                        continue;

                    float angle = (float)Math.Atan2(segY2 - segY1, segX2 - segX1);
                    float cos = (float)Math.Cos(angle);
                    float sin = (float)Math.Sin(angle);

                    // Draw segment as rotated rectangle
                    float halfWidth = LINE_WIDTH / 2.0f;
                    float perpX = -sin * halfWidth;
                    float perpY = cos * halfWidth;

                    int x = (int)(segX1 - perpX);
                    int y = (int)(segY1 - perpY);
                    int width = Math.Max(1, (int)segLength);
                    int height = LINE_WIDTH;

                    batcher.Draw(
                        SolidColorTextureCache.GetTexture(color),
                        new Rectangle(x, y, width, height),
                        hueVector
                    );
                }

                // Draw endpoint markers
                int markerSize = LINE_WIDTH * 2;
                batcher.Draw(
                    SolidColorTextureCache.GetTexture(color),
                    new Rectangle(x1 - markerSize / 2, y1 - markerSize / 2, markerSize, markerSize),
                    hueVector
                );
                batcher.Draw(
                    SolidColorTextureCache.GetTexture(color),
                    new Rectangle(x2 - markerSize / 2, y2 - markerSize / 2, markerSize, markerSize),
                    hueVector
                );
            }
        }
    }
}

