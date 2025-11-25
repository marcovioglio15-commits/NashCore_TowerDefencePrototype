using System;
using System.Collections.Generic;
using Player.Inventory;
using Scriptables.Turrets;
using UnityEngine;
using Utils.Combat;

public static class EventsManager
{
    #region Actions
    #region Inputs
    public static Action<Vector2> Drag;
    public static Action<Vector2> Swipe;
    public static Action<Vector2> HoldBegan;
    public static Action<Vector2> HoldEnded;
    public static Action<Vector2> Tap;
    public static Action<Vector2> PinchIn;
    public static Action<Vector2> PinchOut;
    #endregion

    #region Buildables
    public static Action<IReadOnlyList<TurretClassDefinition>> BuildablesCatalogChanged;
    public static Action<TurretClassDefinition, Vector2> BuildableDragBegan;
    public static Action<Vector2> BuildableDragUpdated;
    public static Action<Vector2> BuildableDragEnded;
    public static Action<BuildPreviewData> BuildablePreviewUpdated;
    public static Action<BuildPlacementResult> BuildablePlacementResolved;
    public static Action<TurretClassDefinition> BuildableRelocationBegan;
    #endregion

    #region Turret Possession
    public static Action<PooledTurret> TurretPerspectiveRequested;
    public static Action<PooledTurret> TurretFreeAimStarted;
    public static Action<PooledTurret> TurretFreeAimEnded;
    public static Action TurretFreeAimExitRequested;
    #endregion

    #region Game Phases
    public static Action GamePhaseAdvanceRequested;
    public static Action<GamePhase> GamePhaseChanged;
    public static Action<int> PlayerGoldChanged;
    public static Action<int> PlayerGoldEarned;
    public static Action<int, int> PlayerGoldInsufficient;
    #endregion

    #region Player
    public static Action<float, float> PlayerHealthChanged;
    public static Action<IDamage, Vector3, float> PlayerDamaged;
    public static Action PlayerDeath;
    #endregion

    #region Combat Resolution
    public static Action GameVictoryAchieved;
    public static Action GameDefeatTriggered;
    public static Action IncreaseCompletedHordesCounter;
    #endregion
    #endregion

    #region Invokes
    #region Inputs
    public static void InvokeDrag(Vector2 delta) => Drag?.Invoke(delta);
    public static void InvokeSwipe(Vector2 delta) => Swipe?.Invoke(delta);
    public static void InvokeHoldBegan(Vector2 screenPosition) => HoldBegan?.Invoke(screenPosition);
    public static void InvokeHoldEnded(Vector2 screenPosition) => HoldEnded?.Invoke(screenPosition);
    public static void InvokeTap(Vector2 screenPosition) => Tap?.Invoke(screenPosition);
    public static void InvokePinchIn(Vector2 delta)=> PinchIn?.Invoke(delta);
    public static void InvokePinchOut(Vector2 delta)=> PinchOut?.Invoke(delta);
    #endregion

    #region Buildables
    public static void InvokeBuildablesCatalogChanged(IReadOnlyList<TurretClassDefinition> catalog)=> BuildablesCatalogChanged?.Invoke(catalog);
    public static void InvokeBuildableDragBegan(TurretClassDefinition definition, Vector2 screenPosition)=> BuildableDragBegan?.Invoke(definition, screenPosition);
    public static void InvokeBuildableDragUpdated(Vector2 screenPosition)=> BuildableDragUpdated?.Invoke(screenPosition);
    public static void InvokeBuildableDragEnded(Vector2 screenPosition)=> BuildableDragEnded?.Invoke(screenPosition);
    public static void InvokeBuildablePreviewUpdated(BuildPreviewData preview)=> BuildablePreviewUpdated?.Invoke(preview);
    public static void InvokeBuildablePlacementResolved(BuildPlacementResult result)=> BuildablePlacementResolved?.Invoke(result);
    public static void InvokeBuildableRelocationBegan(TurretClassDefinition definition)=> BuildableRelocationBegan?.Invoke(definition);
    #endregion

    #region Turret Possession
    public static void InvokeTurretPerspectiveRequested(PooledTurret turret)=> TurretPerspectiveRequested?.Invoke(turret);
    public static void InvokeTurretFreeAimStarted(PooledTurret turret)=> TurretFreeAimStarted?.Invoke(turret);
    public static void InvokeTurretFreeAimEnded(PooledTurret turret)=> TurretFreeAimEnded?.Invoke(turret);
    public static void InvokeTurretFreeAimExitRequested()=> TurretFreeAimExitRequested?.Invoke();
    #endregion

    #region Game Phases
    public static void InvokeGamePhaseAdvanceRequested()=> GamePhaseAdvanceRequested?.Invoke();
    public static void InvokeGamePhaseChanged(GamePhase phase)=> GamePhaseChanged?.Invoke(phase);
    public static void InvokePlayerGoldChanged(int gold)=> PlayerGoldChanged?.Invoke(gold);
    public static void InvokePlayerGoldEarned(int amount)=> PlayerGoldEarned?.Invoke(amount);
    public static void InvokePlayerGoldInsufficient(int currentGold, int requiredGold)=> PlayerGoldInsufficient?.Invoke(currentGold, requiredGold);
    #endregion

    #region Player
    public static void InvokePlayerHealthChanged(float currentHealth, float maxHealth)=> PlayerHealthChanged?.Invoke(currentHealth, maxHealth);
    public static void InvokePlayerDamaged(IDamage damage, Vector3 hitPoint, float currentHealth)=> PlayerDamaged?.Invoke(damage, hitPoint, currentHealth);
    public static void InvokePlayerDeath()=> PlayerDeath?.Invoke();
    #endregion

    #region Combat Resolution
    public static void InvokeGameVictoryAchieved()=> GameVictoryAchieved?.Invoke();
    public static void InvokeGameDefeatTriggered()=> GameDefeatTriggered?.Invoke();
    public static void InvokeIncreaseCompletedHordesCounter()=> IncreaseCompletedHordesCounter?.Invoke();
    #endregion
    #endregion

}
