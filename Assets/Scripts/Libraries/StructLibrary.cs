using Enemy;
using Scriptables.Enemies;
using Scriptables.Turrets;
using System.Collections.Generic;
using UnityEngine;

namespace Player.Inventory
{
    /// <summary>
    /// Data object describing a placement preview result broadcast to the UI.
    /// </summary>
    public readonly struct BuildPreviewData
    {
        #region Variables And Properties
        #region Serialized-Like Fields
        public TurretClassDefinition Definition { get; }
        public Vector3 WorldPosition { get; }
        public Vector2Int Cell { get; }
        public bool HasValidCell { get; }
        #endregion
        #endregion

        #region Methods
        /// <summary>
        /// Builds a new immutable preview description.
        /// </summary>
        public BuildPreviewData(TurretClassDefinition definition, Vector3 worldPosition, Vector2Int cell, bool hasValidCell)
        {
            Definition = definition;
            WorldPosition = worldPosition;
            Cell = cell;
            HasValidCell = hasValidCell;
        }
        #endregion
    }

    /// <summary>
    /// Result payload emitted whenever a placement attempt finishes.
    /// </summary>
    public readonly struct BuildPlacementResult
    {
        #region Variables And Properties
        #region Serialized-Like Fields
        public TurretClassDefinition Definition { get; }
        public bool Success { get; }
        public string FailureReason { get; }
        public Vector3 WorldPosition { get; }
        public Vector2Int Cell { get; }
        #endregion
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new immutable placement result.
        /// </summary>
        public BuildPlacementResult(TurretClassDefinition definition, bool success, string failureReason, Vector3 worldPosition, Vector2Int cell)
        {
            Definition = definition;
            Success = success;
            FailureReason = failureReason;
            WorldPosition = worldPosition;
            Cell = cell;
        }
        #endregion
    }
}
[System.Serializable]
public struct DijkstraInfo
{
    public int[] distances;
    //public Dictionary<int, int> pathIndexesDict;
    public int[] pathIndexes;
    public DijkstraInfo(in int[] distances, /*in Dictionary<int, int> pathIndexesDict,*/ in int[] pathIndexes)
    {
        this.distances = distances;
        //this.pathIndexesDict = pathIndexesDict;
        this.pathIndexes = pathIndexes;
    }
}

/// <summary>
/// Groups multiple waves executed during a single defence phase.
/// </summary>
[System.Serializable]
public struct HordeDefinition
{
    [Tooltip("Identifier used in debug panels or logs.")]
    [SerializeField] private string key;

    [Tooltip("Waves executed sequentially during this horde.")]
    [SerializeField] private List<HordeWave> waves;

    public string Key { get { return key; } }
    public IReadOnlyList<HordeWave> Waves { get { return waves != null ? waves : System.Array.Empty<HordeWave>(); } }
}

/// <summary>
/// Configures a single wave with enemy type, spawn cadence, and start mode.
/// </summary>
[System.Serializable]
public struct HordeWave
{
    [Tooltip("Enemy archetype spawned in this wave.")]
    [SerializeField] private EnemyClassDefinition enemyDefinition;

    [Tooltip("Runtime modifiers applied on spawn to speed or reward enemies.")]
    [SerializeField] private EnemyRuntimeModifiers runtimeModifiers;

    [Tooltip("Total number of enemies spawned in this wave.")]
    [SerializeField] private int enemyCount;

    [Tooltip("Seconds between spawns for this wave.")]
    [SerializeField] private float spawnCadenceSeconds;

    [Tooltip("Offset applied to the resolved spawn position for this wave.")]
    [SerializeField] private Vector3 spawnOffset;

    [Tooltip("Spawn nodes used for this wave. Nodes must be marked as enemy spawns in the grid.")]
    [SerializeField] private List<Vector2Int> spawnNodes;

    [Tooltip("Mode controlling when the next wave begins.")]
    [SerializeField] private WaveAdvanceMode advanceMode;

    [Tooltip("Delay applied before the next wave starts. Applied after the last enemy spawn or after full clear based on the advance mode.")]
    [SerializeField] private float advanceDelaySeconds;

    public EnemyClassDefinition EnemyDefinition { get { return enemyDefinition; } }
    public EnemyRuntimeModifiers RuntimeModifiers { get { return runtimeModifiers; } }
    public int EnemyCount { get { return enemyCount; } }
    public float SpawnCadenceSeconds { get { return spawnCadenceSeconds; } }
    public Vector3 SpawnOffset { get { return spawnOffset; } }
    public IReadOnlyList<Vector2Int> SpawnNodes { get { return spawnNodes != null ? spawnNodes : System.Array.Empty<Vector2Int>(); } }
    public WaveAdvanceMode AdvanceMode { get { return advanceMode; } }
    public float AdvanceDelaySeconds { get { return advanceDelaySeconds; } }
}