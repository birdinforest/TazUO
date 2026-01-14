// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Text.Json;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Combat
{
    /// <summary>
    /// Special combat operation state enum.
    /// Shared between client and server (unified naming).
    /// </summary>
    public enum SpecialCombatOperationState : byte
    {
        None = 0,        // Not started
        Preparing = 1,   // Initializing (e.g., drawing bow)
        Active = 2,      // Active and providing bonuses (e.g., fully charged)
        Completing = 3,  // Finishing (e.g., waiting for attack result)
        Completed = 4,   // Successfully completed
        Canceled = 5,    // Canceled by user
        Interrupted = 6  // Interrupted by external event
    }

    /// <summary>
    /// Client-side operation info.
    /// Purpose: Store presentation state (UI, timers) only.
    /// Server handles game logic.
    /// </summary>
    public abstract class SpecialCombatOperationInfo
    {
        public SpecialCombatOperationState CurrentState { get; set; }
        public DateTime StartTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Update info from server state update packet
        /// </summary>
        public virtual void UpdateFromServer(SpecialCombatOperationState state, string metadataJson)
        {
            CurrentState = state;
            if (!string.IsNullOrEmpty(metadataJson) && metadataJson != "{}")
            {
                try
                {
                    Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                }
                catch (Exception ex)
                {
                    Log.Error($"[SpecialCombatOperationInfo] Failed to parse metadata: {ex.Message}");
                    Metadata = new Dictionary<string, object>();
                }
            }
        }

        public virtual void Clear()
        {
            CurrentState = SpecialCombatOperationState.None;
            StartTime = DateTime.MinValue;
            Metadata?.Clear();
        }
    }

    /// <summary>
    /// Client-side operation.
    /// Purpose: Manage UI and presentation only. NO game logic.
    /// Server drives state changes.
    /// </summary>
    public abstract class SpecialCombatOperation : IDisposable
    {
        protected readonly World _world;
        protected IDisposable _uiIndicator;

        public abstract string OperationId { get; }
        public abstract SpecialCombatOperationInfo Info { get; }
        public SpecialCombatOperationState CurrentState => Info.CurrentState;

        protected SpecialCombatOperation(World world)
        {
            _world = world;
        }

        /// <summary>
        /// Called when server informs client of state change.
        /// This is the PRIMARY way states change on client.
        /// </summary>
        public virtual void OnServerStateUpdate(SpecialCombatOperationState newState, string metadataJson)
        {
            SpecialCombatOperationState oldState = CurrentState;

            // Update info from server
            Info.UpdateFromServer(newState, metadataJson);

            // Handle state transition
            if (oldState != newState)
            {
                OnStateExit(oldState);
                OnStateEnter(newState);

                Log.Trace($"[{OperationId}] State: {oldState} -> {newState}");
            }
        }

        /// <summary>
        /// Called every frame for local updates (animations, UI)
        /// </summary>
        public virtual void OnUpdate(float deltaTime) { }

        /// <summary>
        /// State transition hooks for UI management
        /// </summary>
        protected virtual void OnStateEnter(SpecialCombatOperationState state) { }
        protected virtual void OnStateExit(SpecialCombatOperationState state) { }

        // UI management (operations control their own UI)
        protected abstract IDisposable CreateUIIndicator();

        protected virtual void ShowUI()
        {
            if (_uiIndicator == null)
            {
                _uiIndicator = CreateUIIndicator();
                Log.Trace($"[{OperationId}] UI shown");
            }
        }

        protected virtual void HideUI()
        {
            if (_uiIndicator != null)
            {
                _uiIndicator?.Dispose();
                _uiIndicator = null;
                Log.Trace($"[{OperationId}] UI hidden");
            }
        }

        public virtual void Dispose()
        {
            HideUI();
            Info.Clear();
            Log.Trace($"[{OperationId}] Disposed");
        }
    }
}

