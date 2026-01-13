// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Game.GameObjects;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Animation command types for advanced animation control
    /// </summary>
    public enum AnimationCommand : byte
    {
        Play = 0x01,        // Play animation from startFrame to endFrame
        Hold = 0x02,        // Hold at specific frame
        Continue = 0x03,    // Continue from current frame
        Stop = 0x04,        // Stop current animation
        Repeat = 0x05       // Repeat specific frame
    }

    /// <summary>
    /// Modern animation system that provides frame-level control over mobile animations
    /// </summary>
    public class AnimationSystem
    {
        private static AnimationSystem _instance;
        private readonly Dictionary<uint, AnimationState> _activeAnimations = new Dictionary<uint, AnimationState>();

        public static AnimationSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AnimationSystem();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Animation state for a mobile
        /// </summary>
        public class AnimationState
        {
            public uint MobileSerial { get; set; }
            public ushort Action { get; set; }
            public AnimationCommand Command { get; set; }
            public byte StartFrame { get; set; }
            public byte EndFrame { get; set; }
            public byte HoldFrame { get; set; }
            public bool Forward { get; set; }
            public byte Delay { get; set; }
            public byte RepeatCount { get; set; }
            public byte CurrentFrame { get; set; }
            public long LastFrameTime { get; set; }
            public byte RemainingRepeats { get; set; }
            public bool IsActive { get; set; }

            public AnimationState()
            {
                IsActive = true;
                LastFrameTime = Time.Ticks;
            }

            public void Reset()
            {
                IsActive = false;
                Command = AnimationCommand.Stop;
            }
        }

        /// <summary>
        /// Process advanced animation packet
        /// </summary>
        /// <param name="holdFrame">Frame to hold at. Value 255 indicates "no hold frame" (sentinel when server sends null).
        /// Only used for Hold and Repeat commands. Ignored for Play/Continue/Stop commands.</param>
        public void ProcessAdvancedAnimation(
            World world,
            uint mobileSerial,
            ushort action,
            AnimationCommand command,
            byte startFrame,
            byte endFrame,
            byte holdFrame, // 255 = no hold frame (sentinel value)
            bool forward,
            byte delay,
            byte repeatCount
        )
        {
            Mobile mobile = world.Mobiles.Get(mobileSerial);
            if (mobile == null)
            {
                Log.Warn($"[AnimationSystem] Mobile {mobileSerial} not found");
                return;
            }

            // Process command
            // Note: holdFrame=255 (sentinel for "no hold frame") is only validated for Hold/Repeat commands
            // For Play/Continue/Stop commands, holdFrame is ignored
            switch (command)
            {
                case AnimationCommand.Play:
                    // holdFrame is passed to PlayAnimation for play-then-hold behavior
                    PlayAnimation(mobile, action, startFrame, endFrame, forward, delay, holdFrame);
                    break;

                case AnimationCommand.Hold:
                    // holdFrame=255 is invalid for Hold command (will be validated in HoldFrame method)
                    // HoldFrame(mobile, action, holdFrame);
                    break;

                case AnimationCommand.Continue:
                    // holdFrame is ignored for Continue command (255 sentinel is normal)
                    ContinueAnimation(mobile, action, endFrame, forward, delay);
                    break;

                case AnimationCommand.Stop:
                    // holdFrame is ignored for Stop command (255 sentinel is normal)
                    StopAnimation(mobile);
                    break;

                case AnimationCommand.Repeat:
                    // holdFrame=255 is invalid for Repeat command (will be validated in RepeatFrame method)
                    RepeatFrame(mobile, action, holdFrame, repeatCount, delay);
                    break;

                default:
                    Log.Warn($"[AnimationSystem] Unknown animation command: {command}");
                    break;
            }
        }

        /// <summary>
        /// Update all active animations (called from game loop)
        /// </summary>
        public void Update(World world)
        {
            // Update frame progression for active animations
            List<uint> toRemove = null;

            foreach (KeyValuePair<uint, AnimationState> kvp in _activeAnimations)
            {
                AnimationState state = kvp.Value;
                if (!state.IsActive)
                {
                    if (toRemove == null)
                        toRemove = new List<uint>();
                    toRemove.Add(kvp.Key);
                    continue;
                }

                Mobile mobile = world.Mobiles.Get(kvp.Key);
                if (mobile == null || mobile.IsDestroyed)
                {
                    state.Reset();
                    if (toRemove == null)
                        toRemove = new List<uint>();
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Update frame progression based on command
                UpdateAnimationState(mobile, state);
            }

            // Clean up inactive animations
            if (toRemove != null)
            {
                foreach (uint serial in toRemove)
                {
                    _activeAnimations.Remove(serial);
                }
            }
        }

        /// <summary>
        /// Update animation state for a mobile
        /// NOTE: This system takes exclusive control by setting ExecuteAnimation = false
        /// to prevent Mobile.ProcessAnimation from interfering with frame progression
        /// </summary>
        private void UpdateAnimationState(Mobile mobile, AnimationState state)
        {
            // Calculate frame timing (default 100ms per frame if delay is 0)
            int frameDelay = state.Delay > 0 ? state.Delay : 100;
            long currentTime = Time.Ticks;
            long elapsed = currentTime - state.LastFrameTime;

            // if(mobile.Name == "BirdinForest")
            // {
            //     Console.WriteLine($"AnimationSystem.UpdateAnimationState: currentTime={currentTime} lastFrameTime={state.LastFrameTime} elapsed={elapsed} frameDelay={frameDelay}");
            // }

            if (elapsed < frameDelay)
                return;

            state.LastFrameTime = currentTime;

            if(mobile.Name == "BirdinForest")
            {
                Console.WriteLine($"AnimationSystem.UpdateAnimationState: Mobile={mobile.Serial}, Action={state.Action}, Command={state.Command}, StartFrame={state.StartFrame}, EndFrame={state.EndFrame}, Forward={state.Forward}, Delay={state.Delay}, RepeatCount={state.RepeatCount}, CurrentFrame={state.CurrentFrame}, RemainingRepeats={state.RemainingRepeats}, IsActive={state.IsActive}");
            }

            switch (state.Command)
            {
                case AnimationCommand.Play:
                    UpdatePlayAnimation(mobile, state);
                    break;

                case AnimationCommand.Repeat:
                    UpdateRepeatAnimation(mobile, state);
                    break;

                case AnimationCommand.Hold:
                    // Hold doesn't need frame updates
                    break;

                case AnimationCommand.Continue:
                    UpdateContinueAnimation(mobile, state);
                    break;
            }
        }

        /// <summary>
        /// Update play animation frame progression
        /// </summary>
        private void UpdatePlayAnimation(Mobile mobile, AnimationState state)
        {
            // if (!mobile.ExecuteAnimation)
            //     return;

            int targetFrame = state.EndFrame == 255 ? mobile.AnimationFrameCount : state.EndFrame;

            if (state.Forward)
            {
                state.CurrentFrame++;

                // Check if we've reached holdFrame (play-then-hold behavior)
                if (state.HoldFrame != 255 && state.CurrentFrame >= state.HoldFrame)
                {
                    // Reached hold frame - stop advancing but keep state active
                    mobile.AnimIndex = state.HoldFrame;
                    state.CurrentFrame = state.HoldFrame;
                    state.Command = AnimationCommand.Hold;  // Switch to Hold mode
                    Log.Trace($"[AnimationSystem] UpdatePlayAnimation: Reached hold frame {state.HoldFrame} for Mobile={mobile.Serial}, switching to Hold mode");
                    return;
                }

                if (state.CurrentFrame > targetFrame)
                {
                    // Animation complete - re-enable legacy system
                    Log.Trace($"[AnimationSystem] UpdatePlayAnimation: Animation complete for Mobile={mobile.Serial}, Action={state.Action}, reached frame {state.CurrentFrame} (target was {targetFrame})");
                    byte finalFrame = (byte)targetFrame;
                    mobile.AnimIndex = finalFrame; // Ensure AnimIndex is set to final frame before stopping
                    state.Reset();

                    // Reset animation group to force legacy system to recalculate correct animation
                    // This works for both war mode and peace mode
                    mobile.ExecuteAnimation = true;  // Re-enable legacy animation system
                    mobile.ResetAnimationGroup(); // Reset animation group to 0xFF (unset)

                    _activeAnimations.Remove(mobile.Serial);  // Clean up state
                    Log.Trace($"[AnimationSystem] UpdatePlayAnimation: After completion - ExecuteAnimation={mobile.ExecuteAnimation}, ResetAnimationGroup called, legacy system re-enabled");
                    return;
                }
            }
            else
            {
                state.CurrentFrame--;

                // Check if we've reached holdFrame (play-then-hold behavior for backward animation)
                if (state.HoldFrame != 255 && state.CurrentFrame <= state.HoldFrame)
                {
                    // Reached hold frame - stop advancing but keep state active
                    mobile.AnimIndex = state.HoldFrame;
                    state.CurrentFrame = state.HoldFrame;
                    state.Command = AnimationCommand.Hold;  // Switch to Hold mode
                    Log.Trace($"[AnimationSystem] UpdatePlayAnimation: Reached hold frame {state.HoldFrame} for Mobile={mobile.Serial} (backward), switching to Hold mode");
                    return;
                }

                if (state.CurrentFrame < targetFrame)
                {
                    // Animation complete - re-enable legacy system
                    Log.Trace($"[AnimationSystem] UpdatePlayAnimation: Animation complete for Mobile={mobile.Serial}, reached frame {state.CurrentFrame} (target was {targetFrame})");
                    state.Reset();

                    // Reset animation group to force recalculation
                    mobile.ExecuteAnimation = true;  // Re-enable legacy animation system
                    mobile.ResetAnimationGroup(); // Reset animation group to 0xFF (unset)

                    _activeAnimations.Remove(mobile.Serial);  // Clean up state
                    return;
                }
            }

            mobile.AnimIndex = state.CurrentFrame;

            if(mobile.Name == "BirdinForest")
            {
                Console.WriteLine($"AnimationSystem.UpdatePlayAnimation: state.CurrentFrame={state.CurrentFrame} targetFrame={targetFrame} mobile.animIndex={mobile.AnimIndex}");
            }
        }

        /// <summary>
        /// Update continue animation frame progression
        /// </summary>
        private void UpdateContinueAnimation(Mobile mobile, AnimationState state) => UpdatePlayAnimation(mobile, state);

        /// <summary>
        /// Update repeat animation
        /// </summary>
        private void UpdateRepeatAnimation(Mobile mobile, AnimationState state)
        {
            if (state.RemainingRepeats > 0)
            {
                state.RemainingRepeats--;
                mobile.AnimIndex = state.HoldFrame;

                if (state.RemainingRepeats == 0)
                {
                    state.Reset();
                }
            }
        }

        /// <summary>
        /// Play animation from startFrame to endFrame
        /// </summary>
        /// <param name="holdFrame">Optional frame to hold at. If not 255, animation will play to this frame and hold. Use Continue command to resume.</param>
        private void PlayAnimation(Mobile mobile, ushort action, byte startFrame, byte endFrame, bool forward, byte delay, byte holdFrame = 255)
        {
            // Remove any existing animation state
            bool hadExisting = _activeAnimations.ContainsKey(mobile.Serial);
            if (hadExisting)
            {
                Log.Trace($"[AnimationSystem] PlayAnimation: Stopping existing animation for Mobile={mobile.Serial} before starting new one");
            }
            StopAnimation(mobile);

            // Set animation
            mobile.SetAnimation((byte)action);
            mobile.AnimIndex = startFrame;
            // Set ExecuteAnimation = false to prevent built-in ProcessAnimation from interfering
            // The custom AnimationSystem will handle frame progression manually
            mobile.ExecuteAnimation = false;

            // Create animation state
            var state = new AnimationState
            {
                MobileSerial = mobile.Serial,
                Action = action,
                Command = AnimationCommand.Play,
                StartFrame = startFrame,
                EndFrame = endFrame,
                HoldFrame = holdFrame,  // Store holdFrame for play-then-hold behavior
                Forward = forward,
                Delay = delay,
                CurrentFrame = startFrame,
                IsActive = true,
                LastFrameTime = Time.Ticks
            };

            _activeAnimations[mobile.Serial] = state;

            Log.Trace($"[AnimationSystem] PlayAnimation: Mobile={mobile.Serial}, Action={action}, Start={state.StartFrame}, End={state.EndFrame}, Hold={holdFrame} (255=no hold), Delay={state.Delay}ms, LastFrameTime={state.LastFrameTime}, HadExisting={hadExisting}, TotalActive={_activeAnimations.Count}");
        }

        /// <summary>
        /// Hold animation at specific frame
        /// </summary>
        private void HoldFrame(Mobile mobile, ushort action, byte holdFrame)
        {
            if (holdFrame == 255)
            {
                Log.Warn($"[AnimationSystem] Invalid hold frame: 255");
                return;
            }

            // Remove any existing animation state
            StopAnimation(mobile);

            // Set animation and hold at frame
            mobile.SetAnimation((byte)action);
            mobile.AnimIndex = holdFrame;
            mobile.ExecuteAnimation = false; // Stop automatic progression

            // Create animation state
            var state = new AnimationState
            {
                MobileSerial = mobile.Serial,
                Action = action,
                Command = AnimationCommand.Hold,
                HoldFrame = holdFrame,
                CurrentFrame = holdFrame,
                IsActive = true,
                LastFrameTime = Time.Ticks
            };

            _activeAnimations[mobile.Serial] = state;

            Log.Trace($"[AnimationSystem] Hold frame: Mobile={mobile.Serial}, Action={action}, Frame={holdFrame}");
        }

        /// <summary>
        /// Continue animation from current frame
        /// </summary>
        private void ContinueAnimation(Mobile mobile, ushort action, byte endFrame, bool forward, byte delay)
        {
            byte currentFrame = mobile.AnimIndex;

            // Remove existing state
            StopAnimation(mobile);

            // Set animation
            mobile.SetAnimation((byte)action);
            mobile.AnimIndex = currentFrame;
            // Set ExecuteAnimation = false to prevent built-in ProcessAnimation from interfering
            // The custom AnimationSystem will handle frame progression manually
            mobile.ExecuteAnimation = false;

            // Create animation state
            var state = new AnimationState
            {
                MobileSerial = mobile.Serial,
                Action = action,
                Command = AnimationCommand.Continue,
                StartFrame = currentFrame,
                EndFrame = endFrame,
                Forward = forward,
                Delay = delay,
                CurrentFrame = currentFrame,
                IsActive = true,
                LastFrameTime = Time.Ticks
            };

            _activeAnimations[mobile.Serial] = state;

            Log.Trace($"[AnimationSystem] Continue animation: Mobile={mobile.Serial}, Action={action}, From={currentFrame}, End={endFrame}");
        }

        /// <summary>
        /// Stop animation
        /// </summary>
        private void StopAnimation(Mobile mobile)
        {
            if (_activeAnimations.TryGetValue(mobile.Serial, out AnimationState state))
            {
                state.Reset();
                _activeAnimations.Remove(mobile.Serial);

                // Reset animation group to force recalculation
                mobile.ExecuteAnimation = true;  // Re-enable legacy animation system
                mobile.ResetAnimationGroup(); // Reset animation group to 0xFF (unset)

                Log.Trace($"[AnimationSystem] Stop animation: Mobile={mobile.Serial}, ResetAnimationGroup called, legacy system re-enabled");
            }
        }

        /// <summary>
        /// Repeat a specific frame multiple times
        /// </summary>
        private void RepeatFrame(Mobile mobile, ushort action, byte frame, byte repeatCount, byte delay)
        {
            if (frame == 255)
            {
                Log.Warn($"[AnimationSystem] Invalid repeat frame: 255");
                return;
            }

            // Remove any existing animation state
            StopAnimation(mobile);

            // Set animation
            mobile.SetAnimation((byte)action);
            mobile.AnimIndex = frame;
            mobile.ExecuteAnimation = false;

            // Create animation state
            var state = new AnimationState
            {
                MobileSerial = mobile.Serial,
                Action = action,
                Command = AnimationCommand.Repeat,
                HoldFrame = frame,
                RepeatCount = repeatCount,
                RemainingRepeats = repeatCount,
                Delay = delay,
                CurrentFrame = frame,
                IsActive = true,
                LastFrameTime = Time.Ticks
            };

            _activeAnimations[mobile.Serial] = state;

            Log.Trace($"[AnimationSystem] Repeat frame: Mobile={mobile.Serial}, Action={action}, Frame={frame}, Count={repeatCount}");
        }

        /// <summary>
        /// Get active animation state for a mobile
        /// </summary>
        public AnimationState GetState(uint mobileSerial)
        {
            _activeAnimations.TryGetValue(mobileSerial, out AnimationState state);
            return state;
        }

        /// <summary>
        /// Clear all animation states
        /// </summary>
        public void Clear() => _activeAnimations.Clear();

        /// <summary>
        /// Remove animation state for a mobile (called when mobile is destroyed)
        /// </summary>
        public void RemoveState(uint mobileSerial)
        {
            if (_activeAnimations.TryGetValue(mobileSerial, out AnimationState state))
            {
                state.Reset();
                _activeAnimations.Remove(mobileSerial);
            }
        }
    }
}

