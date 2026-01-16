// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Game.Managers;
using ClassicUO.Utility.Logging;
using ClassicUO.Utility;

namespace ClassicUO.Game.Combat
{
    /// <summary>
    /// Charged shot specific info class
    /// </summary>
    public class ChargedShotInfo : SpecialCombatOperationInfo
    {
        public float ChargeTime { get; set; } = 3.0f;
        public bool FullyCharged { get; set; } = false;

        // Client-side UI start time (for smooth progress bar animation starting from 0%)
        // StartTime (from base class) is the server's authoritative time
        public DateTime ClientUIStartTime { get; set; } = DateTime.MinValue;

        public override void UpdateFromServer(SpecialCombatOperationState state, string metadataJson)
        {
            base.UpdateFromServer(state, metadataJson);


            // Parse charged shot specific metadata
            if (Metadata != null)
            {
                if (Metadata.TryGetValue("chargeTime", out object chargeTime))
                {
                    try
                    {
                        // Handle different types from JSON deserialization
                        if (chargeTime is System.Text.Json.JsonElement jsonElement)
                        {
                            ChargeTime = jsonElement.GetSingle();
                        }
                        else
                        {
                            ChargeTime = Convert.ToSingle(chargeTime);
                        }
                        Log.Trace($"[ChargedShotOperation] Charge time parsed: {ChargeTime}s");
                    }
                    catch (Exception ex)
                    {
                        ChargeTime = 1.0f; // Default to 1 second (matching config default)
                        Log.Warn($"[ChargedShotOperation] Failed to parse chargeTime: {ex.Message}, using fallback: {ChargeTime}s");
                    }
                }

                // Use server's startTime if provided for precise synchronization
                if (Metadata.TryGetValue("startTime", out object startTimeObj))
                {
                    try
                    {
                        long startTimeBinary;

                        // Handle different types from JSON deserialization
                        if (startTimeObj is System.Text.Json.JsonElement jsonElement)
                        {
                            startTimeBinary = jsonElement.GetInt64();
                        }
                        else if (startTimeObj is long longValue)
                        {
                            startTimeBinary = longValue;
                        }
                        else
                        {
                            startTimeBinary = Convert.ToInt64(startTimeObj);
                        }

                        StartTime = DateTime.FromBinary(startTimeBinary);
                        Log.Trace($"[ChargedShotOperation] Start time parsed: {StartTime} (binary: {startTimeBinary})");
                    }
                    catch (Exception ex)
                    {
                        // Fall back to current time if parsing fails
                        StartTime = DateTime.Now;
                        Log.Warn($"[ChargedShotOperation] Failed to parse startTime: {ex.Message}, using DateTime.Now");
                    }
                }

                if (Metadata.TryGetValue("fullyCharged", out object fullyCharged))
                {
                    try
                    {
                        // Handle different types from JSON deserialization
                        if (fullyCharged is System.Text.Json.JsonElement jsonElement)
                        {
                            FullyCharged = jsonElement.GetBoolean();
                        }
                        else
                        {
                            FullyCharged = Convert.ToBoolean(fullyCharged);
                        }

                        TimeSpan elapsed = DateTime.Now - StartTime;
                        Log.Trace($"[ChargedShotOperation] Fully charged: {FullyCharged}, elapsed: {elapsed.TotalSeconds:F3}s (start: {StartTime:HH:mm:ss.fff})");
                    }
                    catch (Exception ex)
                    {
                        FullyCharged = false;
                        Log.Warn($"[ChargedShotOperation] Failed to parse fullyCharged: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Client-side charged shot operation.
    /// Purpose: Display charging UI, play animations/sounds.
    /// Server handles: validation, bonuses, damage calculation.
    ///
    /// This class is purely presentational - it reacts to server state updates.
    /// </summary>
    public class ChargedShotOperation : SpecialCombatOperation
    {
        private readonly ChargedShotInfo _info = new ChargedShotInfo();

        // Animation frame constants (must match server-side values)
        private const int DRAW_START_FRAME_MOUNT = 0;
        private const int DRAW_END_FRAME_MOUNT = 4;
        private const int HOLD_FRAME_MOUNT = 3;
        private const int DRAW_START_FRAME_UNMOUNT = 0;
        private const int DRAW_END_FRAME_UNMOUNT = 6;
        private const int HOLD_FRAME_UNMOUNT = 4;

        // Track registered callbacks for cleanup
        private readonly List<Action> _registeredCallbacks = new List<Action>();

        // Track which frames have been registered to prevent duplicates
        private readonly HashSet<int> _registeredFrames = new HashSet<int>();

        // Store callback instances to check for duplicates
        private System.Action _drawSoundCallback;
        private System.Action _shotSoundCallback;
        private System.Action _releaseSoundCallback;

        // Track playing sounds for manual stopping
        private IO.Audio.Sound _drawSound;

        public override string OperationId => "ChargedShot";
        public override SpecialCombatOperationInfo Info => _info;

        public ChargedShotOperation(World world) : base(world)
        {
            // Log.Trace("[ChargedShotOperation] Instance created");
        }

        protected override void OnStateEnter(SpecialCombatOperationState state)
        {
            // Log.Trace($"[ChargedShotOperation] Entered state: {state}");

            switch (state)
            {
                case SpecialCombatOperationState.Preparing:
                    // Clear any old frame events from previous operation cycle
                    // This is critical when transitioning from Active/Canceled -> Preparing (rapid re-attack)
                    ClearAllFrameEvents();

                    // Set client-side UI start time for smooth progress bar (always starts at 0%)
                    // This is separate from server's StartTime which is used for synchronization
                    _info.ClientUIStartTime = DateTime.Now;

                    // Fallback: Set server StartTime if not provided in metadata
                    if (_info.StartTime == DateTime.MinValue || _info.StartTime == default(DateTime))
                    {
                        _info.StartTime = DateTime.Now;
                        Log.Warn("[ChargedShotOperation] Server did not provide startTime, using client fallback");
                    }

                    ShowUI();
                    // Register frame events for draw animation
                    RegisterDrawAnimationEvents();
                    break;

                case SpecialCombatOperationState.Ready:
                    // Fully charged - ready to fire
                    // UI continues to show (progress at 100%)
                    // Server is waiting for release input
                    Log.Trace("[ChargedShotOperation] Bow fully charged and ready to fire");
                    break;

                case SpecialCombatOperationState.Active:
                    // Actually firing (after release with target)
                    Log.Trace("[ChargedShotOperation] Firing charged shot");
                    break;

                case SpecialCombatOperationState.Completing:
                    // Stop draw sound when starting release animation
                    StopDrawSound();
                    // Register frame events for release animation
                    RegisterReleaseAnimationEvents();
                    break;

                case SpecialCombatOperationState.Completed:
                case SpecialCombatOperationState.Canceled:
                case SpecialCombatOperationState.Interrupted:
                    ClearAllFrameEvents();
                    HideUI();
                    break;
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            // Local UI updates (progress calculation handled by indicator)
            // No game logic here - server drives state

            // CRITICAL: Register frame events proactively when animation is detected
            // Animation packets can arrive BEFORE state updates, causing frame events to be missed
            // Check if player is playing action 27 (bow draw) animation and register events immediately
            if (_world?.Player != null)
            {
                AnimationSystem.AnimationState animState = AnimationSystem.Instance.GetState(_world.Player.Serial);
                if (animState != null && animState.Action == 27 && animState.IsActive)
                {
                    // Animation is playing - ensure events are registered
                    if (CurrentState == SpecialCombatOperationState.Preparing ||
                        CurrentState == SpecialCombatOperationState.Ready ||
                        CurrentState == SpecialCombatOperationState.None)
                    {
                        // Register draw events if not already registered
                        bool wasRegistered = _registeredFrames.Contains(0);
                        RegisterDrawAnimationEvents();

                        // If we're already past frame 1 and sound hasn't been played, trigger it retroactively
                        // This handles the race condition where animation starts before state update
                        if (!wasRegistered && animState.CurrentFrame >= 1 && _drawSound == null)
                        {
                            Log.Trace($"[ChargedShotOperation] Animation already at frame {animState.CurrentFrame}, triggering draw sound retroactively");
                            PlayDrawSound();
                            _registeredFrames.Add(0); // Mark frame 0 as processed
                        }
                    }
                    else if (CurrentState == SpecialCombatOperationState.Completing)
                    {
                        RegisterReleaseAnimationEvents();
                    }
                }
            }

            // Fallback: Ensure frame events are registered based on state
            // (This handles cases where state update arrives before animation)
            if (CurrentState == SpecialCombatOperationState.Preparing ||
                CurrentState == SpecialCombatOperationState.Ready)
            {
                RegisterDrawAnimationEvents();
            }
            else if (CurrentState == SpecialCombatOperationState.Completing)
            {
                RegisterReleaseAnimationEvents();
            }
        }

        protected override IDisposable CreateUIIndicator()
        {
            var indicator = new ChargedShotIndicator(_world, this);
            Game.Managers.UIManager.Add(indicator);
            // Log.Trace("[ChargedShotOperation] UI indicator created and added");
            return indicator;
        }

        /// <summary>
        /// Register frame events for draw animation.
        /// Draw sound plays at frame 0 (start), ready sound plays at hold frame.
        /// </summary>
        private void RegisterDrawAnimationEvents()
        {
            if (_world?.Player == null)
                return;

            uint playerSerial = _world.Player.Serial;
            bool isMounted = _world.Player.IsMounted;

            // Determine frames based on mount status
            int startFrame = isMounted ? DRAW_START_FRAME_MOUNT : DRAW_START_FRAME_UNMOUNT;
            int holdFrame = isMounted ? HOLD_FRAME_MOUNT : HOLD_FRAME_UNMOUNT;
            int drawSoundFrame = startFrame;
            int shotSoundFrame = holdFrame + 1;

            // Create callbacks only once (reuse if already created)
            if (_drawSoundCallback == null)
                _drawSoundCallback = () => PlayDrawSound();
            if (_shotSoundCallback == null)
                _shotSoundCallback = () => PlayShotSound();

            // Register frame events only if not already registered
            // Frame 1: Draw sound (bow string pull starts)
            if (!_registeredFrames.Contains(drawSoundFrame))
            {
                if (AnimationSystem.Instance.RegisterFrameEvent(playerSerial, drawSoundFrame + 1, _drawSoundCallback))
                {
                    _registeredCallbacks.Add(_drawSoundCallback);
                    _registeredFrames.Add(drawSoundFrame);
                    Log.Trace($"[ChargedShotOperation] Registered draw sound event for frame {drawSoundFrame}");
                }
            }

            // Hold frame: Ready sound (bow fully drawn)
            if (!_registeredFrames.Contains(shotSoundFrame))
            {
                if (AnimationSystem.Instance.RegisterFrameEvent(playerSerial, shotSoundFrame, _shotSoundCallback))
                {
                    _registeredCallbacks.Add(_shotSoundCallback);
                    _registeredFrames.Add(shotSoundFrame);
                    Log.Trace($"[ChargedShotOperation] Registered shot sound event for frame {shotSoundFrame}");
                }
            }
        }

        /// <summary>
        /// Register frame events for release animation.
        /// Release sound plays at the start of release animation.
        /// </summary>
        private void RegisterReleaseAnimationEvents()
        {
            if (_world?.Player == null)
                return;

            uint playerSerial = _world.Player.Serial;
            bool isMounted = _world.Player.IsMounted;

            // Release animation starts from hold frame
            int releaseStartFrame = isMounted ? HOLD_FRAME_MOUNT : HOLD_FRAME_UNMOUNT;

            // Create callback only once (reuse if already created)
            if (_releaseSoundCallback == null)
                _releaseSoundCallback = () => PlayReleaseSound();

            // Register frame event for release sound only if not already registered
            // Sound plays when release animation starts (at hold frame transitioning to release)
            if (!_registeredFrames.Contains(releaseStartFrame))
            {
                if (AnimationSystem.Instance.RegisterFrameEvent(playerSerial, releaseStartFrame, _releaseSoundCallback))
                {
                    _registeredCallbacks.Add(_releaseSoundCallback);
                    _registeredFrames.Add(releaseStartFrame);
                    Log.Trace($"[ChargedShotOperation] Registered release sound event for frame {releaseStartFrame}");
                }
            }
        }

        /// <summary>
        /// Clear all registered frame events for cleanup
        /// </summary>
        private void ClearAllFrameEvents()
        {
            if (_world?.Player == null)
                return;

            uint playerSerial = _world.Player.Serial;

            // Clear events for all registered frames
            foreach (int frame in _registeredFrames)
            {
                AnimationSystem.Instance.ClearFrameEvents(playerSerial, frame);
            }

            _registeredCallbacks.Clear();
            _registeredFrames.Clear();

            // Reset callbacks so they can be recreated if needed
            _drawSoundCallback = null;
            _shotSoundCallback = null;
            _releaseSoundCallback = null;

            // Stop any playing draw sound
            StopDrawSound();
        }

        /// <summary>
        /// Stops the currently playing draw sound if any.
        /// </summary>
        private void StopDrawSound()
        {
            if (_drawSound != null)
            {
                try
                {
                    Client.Game.Audio.StopSound(_drawSound);
                }
                catch (Exception ex)
                {
                    Log.Warn($"[ChargedShotOperation] Failed to stop draw sound: {ex.Message}");
                }
                finally
                {
                    _drawSound = null;
                }
            }
        }

        private void PlayDrawSound()
        {
            try
            {
                // Stop any previously playing draw sound
                if (_drawSound != null)
                {
                    Client.Game.Audio.StopSound(_drawSound);
                    _drawSound = null;
                }

                // Play and track the draw sound
                int index = RandomHelper.GetValue(1, 3);
                _drawSound = Client.Game.Audio.PlaySoundFromFile($"bow_draw_0{index}.wav");
                Log.Trace("[ChargedShotOperation] Custom draw sound played from audioassets");
            }
            catch (Exception ex)
            {
                Log.Error($"[ChargedShotOperation] Failed to play draw sound: {ex.Message}");
                // Fallback to default sound on error
                try
                {
                    Client.Game.Audio.PlaySound(0x0233);
                }
                catch
                {
                    // Ignore fallback errors
                }
            }
        }

        private void PlayShotSound()
        {
            try
            {
                Client.Game.Audio.PlaySound(0x02B1); // Shot sound
                Log.Trace("[ChargedShotOperation] Shot sound played at hold frame");
            }
            catch (Exception ex)
            {
                Log.Error($"[ChargedShotOperation] Failed to play shot sound: {ex.Message}");
            }
        }

        private void PlayReleaseSound()
        {
            try
            {
                Client.Game.Audio.PlaySound(0x0234); // Release sound
                Log.Trace("[ChargedShotOperation] Release sound played at release frame");
            }
            catch (Exception ex)
            {
                Log.Error($"[ChargedShotOperation] Failed to play release sound: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            ClearAllFrameEvents();
            base.Dispose();
            // Log.Trace("[ChargedShotOperation] Disposed");
        }
    }
}

