// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Combat
{
    /// <summary>
    /// Charged shot specific info class
    /// </summary>
    public class ChargedShotInfo : SpecialCombatOperationInfo
    {
        public float ChargeTime { get; set; } = 3.0f;
        public bool FullyCharged { get; set; } = false;

        public override void UpdateFromServer(SpecialCombatOperationState state, string metadataJson)
        {
            base.UpdateFromServer(state, metadataJson);

            // Parse charged shot specific metadata
            if (Metadata != null)
            {
                if (Metadata.TryGetValue("chargeTime", out var chargeTime))
                {
                    try
                    {
                        ChargeTime = Convert.ToSingle(chargeTime);
                    }
                    catch
                    {
                        ChargeTime = 3.0f;
                    }
                }

                if (Metadata.TryGetValue("fullyCharged", out var fullyCharged))
                {
                    try
                    {
                        FullyCharged = Convert.ToBoolean(fullyCharged);
                    }
                    catch
                    {
                        FullyCharged = false;
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

        public override string OperationId => "ChargedShot";
        public override SpecialCombatOperationInfo Info => _info;

        // Static constructor to register with manager
        static ChargedShotOperation()
        {
            // Register factory with manager
            SpecialCombatOperationManager.Instance.RegisterOperationFactory(
                "ChargedShot",
                world => new ChargedShotOperation(world)
            );
            Log.Trace("[ChargedShotOperation] Factory registered");
        }

        public ChargedShotOperation(World world) : base(world)
        {
            Log.Trace("[ChargedShotOperation] Instance created");
        }

        protected override void OnStateEnter(SpecialCombatOperationState state)
        {
            Log.Trace($"[ChargedShotOperation] Entered state: {state}");

            switch (state)
            {
                case SpecialCombatOperationState.Preparing:
                    // Server confirmed charge started - show UI
                    _info.StartTime = DateTime.Now;
                    ShowUI();
                    PlayDrawSound();
                    break;

                case SpecialCombatOperationState.Active:
                    // Fully charged (server transitioned to Active)
                    PlayReadySound();
                    // UI will update automatically via OnUpdate()
                    break;

                case SpecialCombatOperationState.Completing:
                    // Shot fired
                    PlayReleaseSound();
                    break;

                case SpecialCombatOperationState.Completed:
                case SpecialCombatOperationState.Canceled:
                case SpecialCombatOperationState.Interrupted:
                    HideUI();
                    break;
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            // Local UI updates (progress calculation handled by indicator)
            // No game logic here - server drives state
        }

        protected override IDisposable CreateUIIndicator()
        {
            var indicator = new ChargedShotIndicator(_world, this);
            Game.Managers.UIManager.Add(indicator);
            Log.Trace("[ChargedShotOperation] UI indicator created and added");
            return indicator;
        }

        private void PlayDrawSound()
        {
            try
            {
                Client.Game.Audio.PlaySound(0x0233); // Bow draw sound
                Log.Trace("[ChargedShotOperation] Draw sound played");
            }
            catch (Exception ex)
            {
                Log.Error($"[ChargedShotOperation] Failed to play draw sound: {ex.Message}");
            }
        }

        private void PlayReadySound()
        {
            try
            {
                Client.Game.Audio.PlaySound(0x0235); // Ready sound
                Log.Trace("[ChargedShotOperation] Ready sound played");
            }
            catch (Exception ex)
            {
                Log.Error($"[ChargedShotOperation] Failed to play ready sound: {ex.Message}");
            }
        }

        private void PlayReleaseSound()
        {
            try
            {
                Client.Game.Audio.PlaySound(0x0234); // Release sound
                Log.Trace("[ChargedShotOperation] Release sound played");
            }
            catch (Exception ex)
            {
                Log.Error($"[ChargedShotOperation] Failed to play release sound: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            Log.Trace("[ChargedShotOperation] Disposed");
        }
    }
}

