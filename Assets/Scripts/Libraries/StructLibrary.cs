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

/// <summary>
/// Holds distance and predecessor information computed by Dijkstra traversal.
/// </summary>
[System.Serializable]
public readonly struct DijkstraInfo
{
    public int[] Distances { get; }
    public int[] Previous { get; }

    public DijkstraInfo(in int[] distances, in int[] previous)
    {
        Distances = distances;
        Previous = previous;
    }
}

/// <summary>
/// Describes an enemy archetype used inside a wave and its specific parameters.
/// </summary>
[System.Serializable]
public struct WaveEnemyType
{
    [Tooltip("Optional friendly name shown in inspectors.")]
    [SerializeField] private string label;

    [Tooltip("Enemy archetype spawned for this entry.")]
    [SerializeField] private EnemyClassDefinition enemyDefinition;

    [Tooltip("Runtime modifiers applied to this enemy type on spawn.")]
    [SerializeField] private EnemyRuntimeModifiers runtimeModifiers;

    [Tooltip("Total number of enemies spawned for this type.")]
    [SerializeField] private int enemyCount;

    [Tooltip("Offset applied to the resolved spawn position for this enemy type.")]
    [SerializeField] private Vector3 spawnOffset;

    public string Label { get { return string.IsNullOrWhiteSpace(label) ? (enemyDefinition != null ? enemyDefinition.name : "Enemy") : label; } }
    public EnemyClassDefinition EnemyDefinition { get { return enemyDefinition; } }
    public EnemyRuntimeModifiers RuntimeModifiers { get { return runtimeModifiers; } }
    public int EnemyCount { get { return enemyCount; } }
    public Vector3 SpawnOffset { get { return spawnOffset; } }

    public WaveEnemyType(EnemyClassDefinition definition, EnemyRuntimeModifiers runtimeModifiers, int enemyCount, Vector3 spawnOffset)
    {
        label = definition != null ? definition.name : string.Empty;
        enemyDefinition = definition;
        this.runtimeModifiers = runtimeModifiers;
        this.enemyCount = enemyCount;
        this.spawnOffset = spawnOffset;
    }
}

/// <summary>
/// Maps a spawn node to the list of enemy type indices that can use it.
/// </summary>
[System.Serializable]
public struct WaveSpawnAssignment
{
    [Tooltip("Grid coordinate for this spawn lane.")]
    [SerializeField] private Vector2Int spawnNode;

    [Tooltip("Indices into the wave's enemy type list that this spawner may emit.")]
    [SerializeField] private List<int> allowedEnemyTypeIndices;

    public Vector2Int SpawnNode { get { return spawnNode; } }
    public IReadOnlyList<int> AllowedEnemyTypeIndices { get { return allowedEnemyTypeIndices != null ? allowedEnemyTypeIndices : System.Array.Empty<int>(); } }

    public WaveSpawnAssignment(Vector2Int spawnNode, List<int> allowedEnemyTypeIndices)
    {
        this.spawnNode = spawnNode;
        this.allowedEnemyTypeIndices = allowedEnemyTypeIndices;
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
/// Defines a macro wave that may contain multiple timed or chained sub-waves.
/// </summary>
[System.Serializable]
public struct HordeWave
{
    [Tooltip("Identifier used for inspectors and debug panels.")]
    [SerializeField] private string label;

    [Tooltip("Ordered sub-waves emitted during this macro wave.")]
    [SerializeField] private List<HordeSubWave> subWaves;

    [Tooltip("Legacy enemy archetypes used when no sub-wave list is populated.")]
    [SerializeField, HideInInspector] private List<WaveEnemyType> enemyTypes;

    [Tooltip("Legacy cadence used to build a fallback sub-wave when migrating scenes.")]
    [SerializeField, HideInInspector] private float spawnCadenceSeconds;

    [Tooltip("Legacy spawn nodes used to build a fallback sub-wave when migrating scenes.")]
    [SerializeField, HideInInspector] private List<Vector2Int> spawnNodes;

    [Tooltip("Legacy per-spawner restrictions used to build a fallback sub-wave when migrating scenes.")]
    [SerializeField, HideInInspector] private List<WaveSpawnAssignment> spawnAssignments;

    [Tooltip("Legacy advance mode preserved for backwards compatibility when synthesizing a sub-wave.")]
    [SerializeField, HideInInspector] private WaveAdvanceMode advanceMode;

    [Tooltip("Legacy delay preserved for backwards compatibility when synthesizing a sub-wave.")]
    [SerializeField, HideInInspector] private float advanceDelaySeconds;

    [Tooltip("Legacy single-enemy definition used for scenes created before multi-type waves existed.")]
    [SerializeField, HideInInspector] private EnemyClassDefinition enemyDefinition;

    [SerializeField, HideInInspector] private EnemyRuntimeModifiers runtimeModifiers;
    [SerializeField, HideInInspector] private int enemyCount;
    [SerializeField, HideInInspector] private Vector3 spawnOffset;

    public string Label
    {
        get { return string.IsNullOrWhiteSpace(label) ? "Wave" : label; }
    }

    public IReadOnlyList<HordeSubWave> SubWaves
    {
        get
        {
            if (subWaves != null && subWaves.Count > 0)
                return subWaves;

            if (HasLegacyContent)
            {
                HordeSubWave legacy = BuildLegacySubWave();
                if (legacy.HasContent)
                    return new[] { legacy };
            }

            return System.Array.Empty<HordeSubWave>();
        }
    }

    public bool HasLegacyEnemy
    {
        get { return enemyDefinition != null; }
    }

    public EnemyClassDefinition LegacyEnemyDefinition
    {
        get { return enemyDefinition; }
    }

    public EnemyRuntimeModifiers LegacyRuntimeModifiers
    {
        get { return runtimeModifiers; }
    }

    public int LegacyEnemyCount
    {
        get { return enemyCount; }
    }

    public Vector3 LegacySpawnOffset
    {
        get { return spawnOffset; }
    }

    private bool HasLegacyContent
    {
        get
        {
            bool hasTypes = enemyTypes != null && enemyTypes.Count > 0;
            bool hasLegacyEnemy = enemyDefinition != null && enemyCount > 0;
            bool hasAssignments = spawnAssignments != null && spawnAssignments.Count > 0;
            bool hasNodes = spawnNodes != null && spawnNodes.Count > 0;
            return hasTypes || hasLegacyEnemy || hasAssignments || hasNodes;
        }
    }

    private HordeSubWave BuildLegacySubWave()
    {
        List<WaveEnemyType> legacyTypes = enemyTypes != null ? new List<WaveEnemyType>(enemyTypes) : new List<WaveEnemyType>();
        List<Vector2Int> legacyNodes = spawnNodes != null ? new List<Vector2Int>(spawnNodes) : new List<Vector2Int>();
        List<WaveSpawnAssignment> legacyAssignments = spawnAssignments != null ? new List<WaveSpawnAssignment>(spawnAssignments) : new List<WaveSpawnAssignment>();
        HordeSubWave legacy = new HordeSubWave(label, legacyTypes, legacyNodes, legacyAssignments, spawnCadenceSeconds, ConvertLegacyStartMode(advanceMode), advanceDelaySeconds, enemyDefinition, runtimeModifiers, enemyCount, spawnOffset);
        return legacy;
    }

    private SubWaveStartMode ConvertLegacyStartMode(WaveAdvanceMode legacyMode)
    {
        if (legacyMode == WaveAdvanceMode.FixedInterval)
            return SubWaveStartMode.DelayFromWaveStart;

        return SubWaveStartMode.AfterPreviousClear;
    }
}

/// <summary>
/// Configures a sub-wave inside a macro wave with independent cadence and start conditions.
/// </summary>
[System.Serializable]
public struct HordeSubWave
{
    [Tooltip("Identifier used for debug tooling and inspectors.")]
    [SerializeField] private string label;

    [Tooltip("Enemy archetypes spawned in this sub-wave.")]
    [SerializeField] private List<WaveEnemyType> enemyTypes;

    [Tooltip("Seconds between spawns for this sub-wave.")]
    [SerializeField] private float spawnCadenceSeconds;

    [Tooltip("Spawn nodes used for this sub-wave. Nodes must be marked as enemy spawns in the grid.")]
    [SerializeField] private List<Vector2Int> spawnNodes;

    [Tooltip("Optional per-spawner restrictions to dictate which enemy types each node can emit. When empty, all enemy types are allowed on every spawn node defined above.")]
    [SerializeField] private List<WaveSpawnAssignment> spawnAssignments;

    [Tooltip("Start condition controlling when this sub-wave begins emitting enemies.")]
    [SerializeField] private SubWaveStartMode startMode;

    [Tooltip("Delay before this sub-wave starts. Interpreted from macro wave start or from previous sub-wave clear depending on the start mode.")]
    [SerializeField] private float startDelaySeconds;

    [Tooltip("Legacy single-enemy definition used for scenes created before multi-type waves existed.")]
    [SerializeField, HideInInspector] private EnemyClassDefinition enemyDefinition;

    [SerializeField, HideInInspector] private EnemyRuntimeModifiers runtimeModifiers;
    [SerializeField, HideInInspector] private int enemyCount;
    [SerializeField, HideInInspector] private Vector3 spawnOffset;

    public string Label
    {
        get { return string.IsNullOrWhiteSpace(label) ? "Sub-Wave" : label; }
    }

    public IReadOnlyList<WaveEnemyType> EnemyTypes
    {
        get { return enemyTypes != null ? enemyTypes : System.Array.Empty<WaveEnemyType>(); }
    }

    public IReadOnlyList<WaveSpawnAssignment> SpawnAssignments
    {
        get { return spawnAssignments != null ? spawnAssignments : System.Array.Empty<WaveSpawnAssignment>(); }
    }

    public IReadOnlyList<Vector2Int> SpawnNodes
    {
        get { return spawnNodes != null ? spawnNodes : System.Array.Empty<Vector2Int>(); }
    }

    public float SpawnCadenceSeconds
    {
        get { return spawnCadenceSeconds; }
    }

    public SubWaveStartMode StartMode
    {
        get { return startMode; }
    }

    public float StartDelaySeconds
    {
        get { return startDelaySeconds; }
    }

    public bool HasLegacyEnemy
    {
        get { return enemyDefinition != null; }
    }

    public EnemyClassDefinition LegacyEnemyDefinition
    {
        get { return enemyDefinition; }
    }

    public EnemyRuntimeModifiers LegacyRuntimeModifiers
    {
        get { return runtimeModifiers; }
    }

    public int LegacyEnemyCount
    {
        get { return enemyCount; }
    }

    public Vector3 LegacySpawnOffset
    {
        get { return spawnOffset; }
    }

    public bool HasContent
    {
        get
        {
            bool hasTypes = enemyTypes != null && enemyTypes.Count > 0;
            bool hasLegacy = enemyDefinition != null && enemyCount > 0;
            bool hasNodes = spawnNodes != null && spawnNodes.Count > 0;
            return hasTypes || hasLegacy || hasNodes;
        }
    }

    public HordeSubWave(string label, List<WaveEnemyType> enemyTypes, List<Vector2Int> spawnNodes, List<WaveSpawnAssignment> spawnAssignments, float spawnCadenceSeconds, SubWaveStartMode startMode, float startDelaySeconds, EnemyClassDefinition legacyDefinition, EnemyRuntimeModifiers legacyModifiers, int legacyCount, Vector3 legacyOffset)
    {
        this.label = label;
        this.enemyTypes = enemyTypes;
        this.spawnNodes = spawnNodes;
        this.spawnAssignments = spawnAssignments;
        this.spawnCadenceSeconds = spawnCadenceSeconds;
        this.startMode = startMode;
        this.startDelaySeconds = startDelaySeconds;
        enemyDefinition = legacyDefinition;
        runtimeModifiers = legacyModifiers;
        enemyCount = legacyCount;
        spawnOffset = legacyOffset;
    }
}
