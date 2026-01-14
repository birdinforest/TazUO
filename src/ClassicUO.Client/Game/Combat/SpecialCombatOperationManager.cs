// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
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

        private SpecialCombatOperationManager()
        {
            _activeOperations = new Dictionary<string, SpecialCombatOperation>();
            _operationFactories = new Dictionary<string, Func<World, SpecialCombatOperation>>();
            RegisterDefaultOperations();
        }

        private void RegisterDefaultOperations() => Log.Trace(
            "[SpecialCombatManager] Initialized (operations will be registered by their classes)"
        );

        /// <summary>
        /// Register an operation factory
        /// </summary>
        public void RegisterOperationFactory(string operationId, Func<World, SpecialCombatOperation> factory)
        {
            if (string.IsNullOrEmpty(operationId) || factory == null)
                return;

            _operationFactories[operationId] = factory;
            Log.Trace($"[SpecialCombatManager] Registered operation factory: {operationId}");
        }

        public void Initialize(World world)
        {
            _world = world;
            Log.Trace("[SpecialCombatManager] Initialized with world");
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
                    Log.Trace($"[SpecialCombatManager] Created operation: {operationId}");
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
                Log.Trace($"[SpecialCombatManager] Removed operation: {operationId}");
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
            Log.Trace("[SpecialCombatManager] Cleared all operations");
        }

        // Query methods
        public bool HasActiveOperation(string operationId) => _activeOperations.ContainsKey(operationId);
        public bool HasAnyActiveOperation() => _activeOperations.Count > 0;
        public SpecialCombatOperation GetOperation(string operationId) => _activeOperations.GetValueOrDefault(operationId);
        public IReadOnlyDictionary<string, SpecialCombatOperation> GetAllActiveOperations() => _activeOperations;
    }
}

