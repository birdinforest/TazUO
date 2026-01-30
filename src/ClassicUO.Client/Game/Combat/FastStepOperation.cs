// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Combat
{
    /// <summary>
    /// FastStep specific info class
    /// </summary>
    public class FastStepInfo : SpecialCombatOperationInfo
    {
        public Direction StepDirection { get; set; }
        public bool Mounted { get; set; }
        public Point3D StartPosition { get; set; }
        public Point3D EndPosition { get; set; }
        public double MovingDuration { get; set; }
        public int FrameDelay { get; set; }
        public DateTime MovementStartTime { get; set; }
        public bool IsMoving { get; set; }

        public override void UpdateFromServer(SpecialCombatOperationState state, string metadataJson)
        {
            base.UpdateFromServer(state, metadataJson);

            Log.Trace($"[FastStepOperation] UpdateFromServer called: state={state}");
            Log.Trace($"[FastStepOperation] Raw metadata JSON: {metadataJson ?? "NULL"}");

            // Parse FastStep specific metadata
            if (Metadata != null)
            {
                if (Metadata.TryGetValue("direction", out object direction))
                {
                    try
                    {
                        // Handle different types from JSON deserialization
                        if (direction is System.Text.Json.JsonElement jsonElement)
                        {
                            StepDirection = (Direction)jsonElement.GetByte();
                        }
                        else
                        {
                            StepDirection = (Direction)Convert.ToByte(direction);
                        }
                        Log.Trace($"[FastStepOperation] Direction parsed: {StepDirection}");
                    }
                    catch (Exception ex)
                    {
                        StepDirection = Direction.North; // Default
                        Log.Warn($"[FastStepOperation] Failed to parse direction: {ex.Message}");
                    }
                }

                if (Metadata.TryGetValue("mounted", out object mounted))
                {
                    try
                    {
                        // Handle different types from JSON deserialization
                        if (mounted is System.Text.Json.JsonElement jsonElement)
                        {
                            Mounted = jsonElement.GetBoolean();
                        }
                        else
                        {
                            Mounted = Convert.ToBoolean(mounted);
                        }
                        Log.Trace($"[FastStepOperation] Mounted: {Mounted}");
                    }
                    catch (Exception ex)
                    {
                        Mounted = false;
                        Log.Warn($"[FastStepOperation] Failed to parse mounted: {ex.Message}");
                    }
                }

                // Parse movement parameters for smooth interpolation
                if (Metadata.TryGetValue("startX", out object startX) &&
                    Metadata.TryGetValue("startY", out object startY) &&
                    Metadata.TryGetValue("startZ", out object startZ))
                {
                    try
                    {
                        int x = startX is System.Text.Json.JsonElement jsonX ? jsonX.GetInt32() : Convert.ToInt32(startX);
                        int y = startY is System.Text.Json.JsonElement jsonY ? jsonY.GetInt32() : Convert.ToInt32(startY);
                        int z = startZ is System.Text.Json.JsonElement jsonZ ? jsonZ.GetInt32() : Convert.ToInt32(startZ);
                        StartPosition = new Point3D(x, y, z);
                        Log.Trace($"[FastStepOperation] Start position parsed: {StartPosition}");
                    }
                    catch (Exception ex)
                    {
                        StartPosition = new Point3D(0, 0, 0);
                        Log.Warn($"[FastStepOperation] Failed to parse start position: {ex.Message}");
                    }
                }

                if (Metadata.TryGetValue("endX", out object endX) &&
                    Metadata.TryGetValue("endY", out object endY) &&
                    Metadata.TryGetValue("endZ", out object endZ))
                {
                    try
                    {
                        int x = endX is System.Text.Json.JsonElement jsonX ? jsonX.GetInt32() : Convert.ToInt32(endX);
                        int y = endY is System.Text.Json.JsonElement jsonY ? jsonY.GetInt32() : Convert.ToInt32(endY);
                        int z = endZ is System.Text.Json.JsonElement jsonZ ? jsonZ.GetInt32() : Convert.ToInt32(endZ);
                        EndPosition = new Point3D(x, y, z);
                        Log.Trace($"[FastStepOperation] End position parsed: {EndPosition}");
                    }
                    catch (Exception ex)
                    {
                        EndPosition = new Point3D(0, 0, 0);
                        Log.Warn($"[FastStepOperation] Failed to parse end position: {ex.Message}");
                    }
                }

                if (Metadata.TryGetValue("duration", out object duration))
                {
                    try
                    {
                        MovingDuration = duration is System.Text.Json.JsonElement jsonDuration
                            ? jsonDuration.GetDouble()
                            : Convert.ToDouble(duration);
                        Log.Trace($"[FastStepOperation] Parsed moving duration from metadata: {MovingDuration}s");
                    }
                    catch (Exception ex)
                    {
                        MovingDuration = 0.3;
                        Log.Warn($"[FastStepOperation] Failed to parse duration: {ex.Message}, raw value: {duration}");
                    }
                }
                else
                {
                    Log.Warn("[FastStepOperation] Metadata does not contain 'duration' key! Using default 0.3s");
                    MovingDuration = 0.3;
                }

                if (Metadata.TryGetValue("frameDelay", out object frameDelay))
                {
                    try
                    {
                        FrameDelay = frameDelay is System.Text.Json.JsonElement jsonFrameDelay
                            ? jsonFrameDelay.GetInt32()
                            : Convert.ToInt32(frameDelay);
                        Log.Trace($"[FastStepOperation] Parsed frame delay from metadata: {FrameDelay}ms");
                    }
                    catch (Exception ex)
                    {
                        FrameDelay = 75;
                        Log.Warn($"[FastStepOperation] Failed to parse frameDelay: {ex.Message}, raw value: {frameDelay}");
                    }
                }
                else
                {
                    Log.Warn("[FastStepOperation] Metadata does not contain 'frameDelay' key! Using default 75ms");
                    FrameDelay = 75;
                }
            }
        }

        public override void Clear()
        {
            base.Clear();
            StepDirection = Direction.North;
            Mounted = false;
            StartPosition = new Point3D(0, 0, 0);
            EndPosition = new Point3D(0, 0, 0);
            MovingDuration = 0.3;
            IsMoving = false;
        }
    }

    /// <summary>
    /// Client-side FastStep operation - handles animation and visual effects
    /// </summary>
    public class FastStepOperation : SpecialCombatOperation
    {
        private readonly FastStepInfo _info = new FastStepInfo();
        public override string OperationId => "FastStep";
        public override SpecialCombatOperationInfo Info => _info;
        public FastStepInfo StepInfo => _info;

        // Configuration constants from config
        private const int DASH_ACTION_UNMOUNTED = 2;  // Run animation
        private const int DASH_ACTION_MOUNTED = 24;     // Horse run animation
        private const int DASH_FRAME_DELAY = 50;       // Frame delay in ms

        public FastStepOperation(World world) : base(world)
        {
        }

        protected override void OnStateEnter(SpecialCombatOperationState state)
        {
            base.OnStateEnter(state);

            switch (state)
            {
                case SpecialCombatOperationState.Active:
                    _info.IsMoving = true;
                    _info.MovementStartTime = DateTime.Now;
                    PlayDashAnimation();
                    Log.Trace($"[FastStepOperation] Starting movement from {_info.StartPosition} to {_info.EndPosition} over {_info.MovingDuration}s");
                    break;

                case SpecialCombatOperationState.Completed:
                    PlayFightingIdle();
                    break;

            case SpecialCombatOperationState.Canceled:
            case SpecialCombatOperationState.Interrupted:
                // Animation will naturally return to idle
                // No manual intervention needed
                break;
            }
        }

        private void PlayDashAnimation()
        {
            if (_world?.Player == null)
            {
                Log.Warn("[FastStepOperation] Cannot play dash animation - player is null");
                return;
            }

            Mobile player = _world.Player;
            bool isMounted = _info.Mounted;

            // Get animation action based on mount status
            int action = isMounted ? DASH_ACTION_MOUNTED : DASH_ACTION_UNMOUNTED;
            int startFrame = 0;
            int endFrame = isMounted ? 8 : 4; // Config: mounted 0-8, unmounted 0-4

            // Use configured frame delay from server (creates fast looping animation)
            int frameDelay = _info.FrameDelay > 0 ? _info.FrameDelay : DASH_FRAME_DELAY;

            Log.Trace($"[FastStepOperation] Playing looping dash animation: action={action}, frames={startFrame}-{endFrame}, frameDelay={frameDelay}ms, duration={_info.MovingDuration}s, mounted={isMounted}");

            // Play dash animation with looping (repeat indefinitely during movement duration)
            AnimationSystem.Instance.ProcessAdvancedAnimation(
                _world,
                player.Serial,
                (ushort)action,
                AnimationCommand.Play,
                (byte)startFrame,
                (byte)endFrame,
                255, // holdFrame: 255 = no hold frame
                true, // forward
                (byte)frameDelay,
                255 // repeatCount: 255 = loop continuously (will be stopped when state changes)
            );

            // Face the dash direction
            if (_info.StepDirection != Direction.NONE)
            {
                player.Direction = _info.StepDirection;
            }
        }

    private void PlayFightingIdle()
    {
        if (_world?.Player == null)
        {
            Log.Warn("[FastStepOperation] Cannot play fighting idle - player is null");
            return;
        }

        Mobile player = _world.Player;

        Log.Trace("[FastStepOperation] Animation complete - returning to war mode idle");

        // The animation system will automatically return to war mode idle
        // We just need to ensure the player is facing the correct direction
        if (_info.StepDirection != Direction.NONE)
        {
            player.Direction = _info.StepDirection;
        }
    }

        public override void OnUpdate(float deltaTime)
        {
            // Handle smooth client-side interpolation
            if (!_info.IsMoving || _world?.Player == null)
                return;

            Mobile player = _world.Player;

            // Calculate progress (0.0 to 1.0)
            double elapsed = (DateTime.Now - _info.MovementStartTime).TotalSeconds;
            double progress = Math.Min(elapsed / _info.MovingDuration, 1.0);

            if (progress >= 1.0)
            {
                // Movement complete
                _info.IsMoving = false;
                player.Offset.X = 0;
                player.Offset.Y = 0;
                player.Offset.Z = 0;
                Log.Trace("[FastStepOperation] Interpolation complete");
                return;
            }

            // Smooth interpolation using easing function
            double easedProgress = EaseOutCubic(progress);

            // Calculate screen pixel offset with isometric transformation
            // UO uses isometric coordinates where:
            // - Moving +X (east) = screen (+22, +22)
            // - Moving +Y (south) = screen (-22, +22)
            // Transformation: screenX = (tileX - tileY) * 22, screenY = (tileX + tileY) * 22
            int startX = _info.StartPosition.X;
            int startY = _info.StartPosition.Y;
            int endX = _info.EndPosition.X;
            int endY = _info.EndPosition.Y;

            int tileOffsetX = endX - startX;
            int tileOffsetY = endY - startY;

            // Convert tile offset to screen offset using isometric transformation
            int screenOffsetX = (tileOffsetX - tileOffsetY) * 22;
            int screenOffsetY = (tileOffsetX + tileOffsetY) * 22;

            // Apply inverse offset (moving from start to end)
            // Start at full NEGATIVE offset (render at old position), lerp to zero (render at new position)
            // Since server already moved player to endPos, we need to offset BACKWARDS initially
            int offsetX = (int)((easedProgress - 1.0) * screenOffsetX);
            int offsetY = (int)((easedProgress - 1.0) * screenOffsetY);
            player.Offset.X = (sbyte)Math.Max(-127, Math.Min(127, offsetX));
            player.Offset.Y = (sbyte)Math.Max(-127, Math.Min(127, offsetY));
            player.Offset.Z = 0;

            // Log occasionally (every 10th frame) to avoid spam
            if ((int)(elapsed * 60) % 10 == 0)
            {
                Log.Trace($"[FastStepOperation] Interpolating: progress={progress:F2}, offset=({player.Offset.X},{player.Offset.Y})");
            }
        }

        private double EaseOutCubic(double t)
        {
            return 1.0 - Math.Pow(1.0 - t, 3.0);
        }

        protected override IDisposable CreateUIIndicator()
        {
            // FastStep doesn't need UI indicator
            return null;
        }

        public override void Dispose()
        {
            base.Dispose();
            Log.Trace("[FastStepOperation] Disposed");
        }
    }
}
