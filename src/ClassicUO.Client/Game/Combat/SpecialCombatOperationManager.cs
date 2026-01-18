// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Network;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Combat
{
    /// <summary>
    /// Manages client-side special combat operations.
    /// Reactive: Responds to server state updates.
    /// Purpose: Coordinate UI/presentation for operations.
    /// </summary>
    public class SpecialCombatOperationManager
    {
        private static SpecialCombatOperationManager _instance;
        public static SpecialCombatOperationManager Instance => _instance ??= new SpecialCombatOperationManager();

        private readonly Dictionary<string, SpecialCombatOperation> _activeOperations;
        private readonly Dictionary<string, Func<World, SpecialCombatOperation>> _operationFactories;
        private World _world;

        // Threshold-based direction tracking for ManualArm mode
        // Only sends updates when UO direction (8-way) changes, not every mouse move
        private bool _isTrackingDirection = false;
        private Direction _lastSentDirection = Direction.NONE;

        private SpecialCombatOperationManager()
        {
            _activeOperations = new Dictionary<string, SpecialCombatOperation>();
            _operationFactories = new Dictionary<string, Func<World, SpecialCombatOperation>>();
        }

        private void RegisterDefaultOperations()
        {
            // Register default operations explicitly
            // This is more reliable than relying on static constructors
            RegisterOperationFactory("ChargedShot", world => new ChargedShotOperation(world));

            // Others ...
        }

        /// <summary>
        /// Register an operation factory
        /// </summary>
        public void RegisterOperationFactory(string operationId, Func<World, SpecialCombatOperation> factory)
        {
            if (string.IsNullOrEmpty(operationId) || factory == null)
                return;

            _operationFactories[operationId] = factory;
        }

        public void Initialize(World world)
        {
            _world = world;
            RegisterDefaultOperations();
        }

        /// <summary>
        /// Called when server sends operation state update.
        /// This is the PRIMARY entry point for operation lifecycle.
        /// </summary>
        public void OnServerStateUpdate(string operationId, SpecialCombatOperationState state, string metadataJson)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                Log.Error("[SpecialCombatManager] Received state update with null/empty operationId");
                return;
            }

            // Get or create operation
            if (!_activeOperations.TryGetValue(operationId, out SpecialCombatOperation operation))
            {
                // Server started a new operation - create it
                if (_operationFactories.TryGetValue(operationId, out Func<World, SpecialCombatOperation> factory))
                {
                    operation = factory(_world);
                    _activeOperations[operationId] = operation;
                }
                else
                {
                    Log.Warn($"[SpecialCombatManager] Unknown operation: {operationId} (factory not registered)");
                    return;
                }
            }

            // Update operation state from server
            operation.OnServerStateUpdate(state, metadataJson);

            // Remove if completed/canceled/interrupted
            if (state == SpecialCombatOperationState.Completed ||
                state == SpecialCombatOperationState.Canceled ||
                state == SpecialCombatOperationState.Interrupted)
            {
                _activeOperations.Remove(operationId);
                operation.Dispose();
            }
        }

        /// <summary>
        /// Update all active operations (for animations, UI)
        /// Called every frame from GameScene.Update()
        /// </summary>
        public void Update(float deltaTime)
        {
            foreach (SpecialCombatOperation operation in _activeOperations.Values.ToList())
            {
                try
                {
                    operation.OnUpdate(deltaTime);
                }
                catch (Exception ex)
                {
                    Log.Error($"[SpecialCombatManager] Error updating operation {operation.OperationId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Clear all operations (on death, disconnect)
        /// </summary>
        public void ClearAllOperations()
        {
            foreach (SpecialCombatOperation operation in _activeOperations.Values)
            {
                try
                {
                    operation.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error($"[SpecialCombatManager] Error disposing operation {operation.OperationId}: {ex.Message}");
                }
            }
            _activeOperations.Clear();
        }

        // Query methods
        public bool HasActiveOperation(string operationId) => _activeOperations.ContainsKey(operationId);
        public bool HasAnyActiveOperation() => _activeOperations.Count > 0;
        public SpecialCombatOperation GetOperation(string operationId) => _activeOperations.GetValueOrDefault(operationId);
        public IReadOnlyDictionary<string, SpecialCombatOperation> GetAllActiveOperations() => _activeOperations;

        // Direction tracking for ManualArm mode
        public bool IsTrackingDirection => _isTrackingDirection;

        /// <summary>
        /// Start tracking mouse direction for ManualArm mode.
        /// Called when a ManualArm operation begins (e.g., ChargedShot with manualArm enabled).
        /// </summary>
        public void StartDirectionTracking()
        {
            _isTrackingDirection = true;
            _lastSentDirection = Direction.NONE;
            Log.Trace("[SpecialCombatManager] Direction tracking started");
        }

        /// <summary>
        /// Stop tracking mouse direction.
        /// Called when operation ends or is canceled.
        /// </summary>
        public void StopDirectionTracking()
        {
            _isTrackingDirection = false;
            _lastSentDirection = Direction.NONE;
            Log.Trace("[SpecialCombatManager] Direction tracking stopped");
        }

        /// <summary>
        /// Called on mouse move to check for direction changes.
        /// Only sends packet when UO direction (8-way) changes from last sent direction.
        /// This implements threshold-based updates: max 7 packets per draw instead of continuous.
        /// </summary>
        /// <param name="cursorWorldX">World X coordinate of cursor</param>
        /// <param name="cursorWorldY">World Y coordinate of cursor</param>
        public void OnMouseMove(int cursorWorldX, int cursorWorldY)
        {
            if (!_isTrackingDirection || _world?.Player == null)
                return;

            // Calculate direction from player to cursor using UO's 8-way direction system
            int playerX = _world.Player.X;
            int playerY = _world.Player.Y;
            Direction newDirection = DirectionHelper.CalculateDirection(playerX, playerY, cursorWorldX, cursorWorldY);

            // Only send update if direction actually changed (threshold-based)
            if (newDirection != _lastSentDirection && newDirection != Direction.NONE)
            {
                _lastSentDirection = newDirection;

                // Send direction update to server
                AsyncNetClient.Socket.Send_SpecialCombatInput(
                    NetClientExt.SpecialCombatInputType.DirectionUpdate,
                    NetClientExt.MouseButton.Left,
                    new Point3D(cursorWorldX, cursorWorldY, 0));

                Log.Trace($"[SpecialCombatManager] Direction update sent: {newDirection} (cursor: {cursorWorldX},{cursorWorldY})");
            }
        }
    }
}

