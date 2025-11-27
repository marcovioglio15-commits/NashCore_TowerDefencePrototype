using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Grid;
using Scriptables.Enemies;
using Enemy;
using Player;

/// <summary>
/// Coordinates ordered hordes of waves, spawns enemies from grid-defined spawn nodes, and drives phase transitions.
/// </summary>
public class HordesManager : Singleton<HordesManager>
{
    #region Variables And Properties
    #region Serialized Fields
    [Header("Dependencies")]
    [Tooltip("Grid used to resolve spawn nodes and optional spawn point bindings.")]
    [SerializeField] private Grid3D grid;
    [Tooltip("Game manager controlling phase flow.")]
    [SerializeField] private GameManager gameManager;

    [Header("Hordes")]
    [Tooltip("Ordered list of hordes. Each entry is executed when entering the defence phase.")]
    [SerializeField] private List<HordeDefinition> hordes = new List<HordeDefinition>();

    [Header("Timing")]
    [Tooltip("Seconds waited after entering defence before the first wave of a horde begins.")]
    [SerializeField] private float defenceStartDelay = 0.5f;

    [Header("Player")]
    [Tooltip("Reference to PlayerHealth component")]
    [SerializeField] private PlayerHealth cachedPlayerHealth;
    #endregion

    #region Runtime
    private int currentHordeIndex = -1;
    private int activeEnemies;
    private Coroutine hordeRoutine;
    private bool hordeActive;
    private readonly List<SpawnPointDoor> spawnDoors = new List<SpawnPointDoor>();
    private readonly HashSet<SpawnPointDoor> previewDoorBuffer = new HashSet<SpawnPointDoor>();
    private readonly List<WaveEnemyType> enemyTypesBuffer = new List<WaveEnemyType>();
    private readonly List<WaveSpawnAssignment> spawnAssignmentBuffer = new List<WaveSpawnAssignment>();
    private readonly List<Vector2Int> previewNodesBuffer = new List<Vector2Int>();
    private readonly List<WaveEnemyTypeState> enemyTypeStatesBuffer = new List<WaveEnemyTypeState>();
    private readonly Dictionary<int, SubWaveRuntimeState> subWaveStates = new Dictionary<int, SubWaveRuntimeState>();
    private readonly List<Coroutine> scheduledSubWaveRoutines = new List<Coroutine>();
    private int nextSubWaveId;
    #endregion

    #region Properties
    public bool HasPendingHordes
    {
        get { return currentHordeIndex + 1 < hordes.Count; }
    }
    #endregion
    #endregion

    #region Methods
    #region Unity
    /// <summary>
    /// Subscribes to phase changes to automatically start hordes.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        EventsManager.GamePhaseChanged += HandlePhaseChanged;
        CacheSpawnDoors();
    }

    /// <summary>
    /// Removes subscriptions when disabled.
    /// </summary>
    private void OnDisable()
    {
        EventsManager.GamePhaseChanged -= HandlePhaseChanged;
        spawnDoors.Clear();
        previewDoorBuffer.Clear();
    }
    #endregion

    #region Public
    /// <summary>
    /// Signals the manager that an enemy has spawned.
    /// </summary>
    public void NotifyEnemySpawned(PooledEnemy enemy)
    {
        if (!hordeActive)
            return;

        activeEnemies++;

        if (enemy == null)
            return;

        EnemySpawnContext context = enemy.LastContext;
        int subWaveId = context.SubWaveId;
        if (subWaveId < 0)
            return;

        if (subWaveStates.TryGetValue(subWaveId, out SubWaveRuntimeState state))
        {
            state.AliveCount++;
            subWaveStates[subWaveId] = state;
        }
    }

    /// <summary>
    /// Signals the manager that an enemy has despawned.
    /// </summary>
    public void NotifyEnemyDespawned(PooledEnemy enemy)
    {
        if (!hordeActive)
            return;

        if (activeEnemies > 0)
            activeEnemies--;

        if (enemy == null)
            return;

        EnemySpawnContext context = enemy.LastContext;
        int subWaveId = context.SubWaveId;
        if (subWaveId < 0)
            return;

        if (subWaveStates.TryGetValue(subWaveId, out SubWaveRuntimeState state))
        {
            if (state.AliveCount > 0)
                state.AliveCount--;

            subWaveStates[subWaveId] = state;
        }
    }
    #endregion

    #region Internal
    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Defence)
        {
            BeginNextHorde();
            return;
        }

        if (phase == GamePhase.Building)
        {
            CloseAllSpawnDoors();
            PreviewNextWaveDoors();
        }
    }

    /// <summary>
    /// Starts the next configured horde when defence begins.
    /// </summary>
    private void BeginNextHorde()
    {
        if (hordeActive)
            return;

        if (!HasPendingHordes)
        {
            EventsManager.InvokeGameVictoryAchieved();
            return;
        }

        if (hordeRoutine != null)
            StopCoroutine(hordeRoutine);

        currentHordeIndex++;
        HordeDefinition definition = hordes[currentHordeIndex];
        hordeRoutine = StartCoroutine(RunHorde(definition));
    }

    /// <summary>
    /// Opens doors for spawn points used by the next scheduled wave and closes the rest.
    /// </summary>
    private void PreviewNextWaveDoors()
    {
        if (grid == null)
            return;

        if (spawnDoors.Count == 0)
            CacheSpawnDoors();

        if (spawnDoors.Count == 0)
            return;

        IReadOnlyList<Vector2Int> previewNodes = ResolveNextWaveSpawnNodes();
        ApplyDoorPreview(previewNodes);
    }

    /// <summary>
    /// Closes all known spawn doors, used before defence begins or when no preview is available.
    /// </summary>
    private void CloseAllSpawnDoors()
    {
        if (spawnDoors.Count == 0)
            CacheSpawnDoors();

        int doorCount = spawnDoors.Count;
        for (int i = 0; i < doorCount; i++)
        {
            SpawnPointDoor door = spawnDoors[i];
            if (door != null)
                door.CloseDoor();
        }
    }

    /// <summary>
    /// Opens only the doors mapped to the provided nodes and closes the rest.
    /// </summary>
    private void ApplyDoorPreview(IReadOnlyList<Vector2Int> previewNodes)
    {
        if (grid == null)
            return;

        if (spawnDoors.Count == 0)
            CacheSpawnDoors();

        if (spawnDoors.Count == 0)
            return;

        if (previewNodes == null || previewNodes.Count == 0)
        {
            CloseAllSpawnDoors();
            return;
        }

        previewDoorBuffer.Clear();
        int previewCount = previewNodes.Count;
        for (int i = 0; i < previewCount; i++)
        {
            SpawnPointDoor door = grid.GetSpawnDoor(previewNodes[i]);
            if (door != null)
                previewDoorBuffer.Add(door);
        }

        int trackedCount = spawnDoors.Count;
        for (int i = 0; i < trackedCount; i++)
        {
            SpawnPointDoor door = spawnDoors[i];
            if (door == null)
                continue;

            if (previewDoorBuffer.Contains(door))
                door.OpenDoor();
            else
                door.CloseDoor();
        }

        previewDoorBuffer.Clear();
    }

    /// <summary>
    /// Closes all doors after a macro wave and prepares the correct lanes for the upcoming one when present.
    /// </summary>
    private void PrepareDoorsForUpcomingWave(IReadOnlyList<HordeWave> waves, int completedIndex)
    {
        CloseAllSpawnDoors();
        if (waves == null)
            return;

        int nextIndex = completedIndex + 1;
        if (nextIndex < 0 || nextIndex >= waves.Count)
            return;

        IReadOnlyList<Vector2Int> previewNodes = ResolveSpawnNodesForWave(waves[nextIndex]);
        ApplyDoorPreview(previewNodes);
    }

    /// <summary>
    /// Builds the cached list of doors using grid bindings to avoid repeated lookups.
    /// </summary>
    private void CacheSpawnDoors()
    {
        spawnDoors.Clear();

        if (grid == null)
            return;

        Vector2Int[] spawnCoords = grid.GetEnemySpawnCoords();
        if (spawnCoords == null || spawnCoords.Length == 0)
            return;

        int coordCount = spawnCoords.Length;
        for (int i = 0; i < coordCount; i++)
        {
            SpawnPointDoor door = grid.GetSpawnDoor(spawnCoords[i]);
            if (door != null && !spawnDoors.Contains(door))
                spawnDoors.Add(door);
        }
    }

    /// <summary>
    /// Returns the spawn nodes of the next upcoming wave when available.
    /// </summary>
    private IReadOnlyList<Vector2Int> ResolveNextWaveSpawnNodes()
    {
        previewNodesBuffer.Clear();

        if (hordes == null || hordes.Count == 0)
            return null;

        int nextHordeIndex = currentHordeIndex + 1;
        if (nextHordeIndex < 0 || nextHordeIndex >= hordes.Count)
            return null;

        HordeDefinition horde = hordes[nextHordeIndex];
        IReadOnlyList<HordeWave> waves = horde.Waves;
        if (waves == null || waves.Count == 0)
            return null;

        int waveCount = waves.Count;
        for (int i = 0; i < waveCount; i++)
        {
            IReadOnlyList<Vector2Int> nodes = ResolveSpawnNodesForWave(waves[i]);
            if (nodes != null && nodes.Count > 0)
                return nodes;
        }

        return null;
    }

    /// <summary>
    /// Resolves spawn nodes for the provided macro wave by inspecting its sub-waves.
    /// </summary>
    private IReadOnlyList<Vector2Int> ResolveSpawnNodesForWave(HordeWave wave)
    {
        previewNodesBuffer.Clear();

        IReadOnlyList<HordeSubWave> subWaves = wave.SubWaves;
        if (subWaves == null || subWaves.Count == 0)
            return null;

        int subWaveCount = subWaves.Count;
        for (int i = 0; i < subWaveCount; i++)
        {
            HordeSubWave subWave = subWaves[i];
            IReadOnlyList<WaveSpawnAssignment> assignments = subWave.SpawnAssignments;
            if (assignments != null && assignments.Count > 0)
            {
                previewNodesBuffer.Clear();
                int assignmentCount = assignments.Count;
                for (int a = 0; a < assignmentCount; a++)
                {
                    Vector2Int node = assignments[a].SpawnNode;
                    if (!previewNodesBuffer.Contains(node))
                        previewNodesBuffer.Add(node);
                }

                if (previewNodesBuffer.Count > 0)
                    return previewNodesBuffer;
            }

            IReadOnlyList<Vector2Int> nodes = subWave.SpawnNodes;
            if (nodes != null && nodes.Count > 0)
                return nodes;
        }

        return null;
    }

    /// <summary>
    /// Sequentially executes all waves in the provided horde.
    /// </summary>
    private IEnumerator RunHorde(HordeDefinition definition)
    {
        hordeActive = true;
        activeEnemies = 0;
        nextSubWaveId = 0;
        subWaveStates.Clear();
        scheduledSubWaveRoutines.Clear();
        if (defenceStartDelay > 0f)
            yield return new WaitForSeconds(defenceStartDelay);

        IReadOnlyList<HordeWave> waves = definition.Waves;
        int waveCount = waves.Count;
        for (int i = 0; i < waveCount; i++)
        {
            HordeWave wave = waves[i];
            yield return StartCoroutine(RunMacroWave(wave));
            PrepareDoorsForUpcomingWave(waves, i);
        }

        yield return new WaitUntil(() => activeEnemies == 0);
        FinalizeHordeCompletion();
        hordeActive = false;
        hordeRoutine = null;
    }

    /// <summary>
    /// Executes all configured sub-waves for a macro wave honoring start modes and dependencies.
    /// </summary>
    private IEnumerator RunMacroWave(HordeWave wave)
    {
        IReadOnlyList<HordeSubWave> subWaves = wave.SubWaves;
        if (subWaves == null || subWaves.Count == 0)
            yield break;

        subWaveStates.Clear();
        scheduledSubWaveRoutines.Clear();
        int lastSequentialId = -1;
        int subWaveCount = subWaves.Count;
        float macroStartTime = Time.time;

        for (int i = 0; i < subWaveCount; i++)
        {
            HordeSubWave subWave = subWaves[i];
            int subWaveId = GetNextSubWaveId();
            EnsureSubWaveState(subWaveId, subWave.Label);

            if (!subWave.HasContent)
            {
                MarkSubWaveSpawningComplete(subWaveId);
                continue;
            }

            if (subWave.StartMode == SubWaveStartMode.DelayFromWaveStart)
            {
                Coroutine routine = StartCoroutine(RunSubWaveAfterDelay(subWave, subWaveId, macroStartTime));
                if (routine != null)
                    scheduledSubWaveRoutines.Add(routine);

                continue;
            }

            Coroutine sequentialRoutine = StartCoroutine(RunSubWaveSequential(subWave, subWaveId, lastSequentialId));
            lastSequentialId = subWaveId;
            if (sequentialRoutine != null)
                yield return sequentialRoutine;
        }

        int scheduledCount = scheduledSubWaveRoutines.Count;
        for (int i = 0; i < scheduledCount; i++)
        {
            Coroutine routine = scheduledSubWaveRoutines[i];
            if (routine != null)
                yield return routine;
        }

        yield return new WaitUntil(() => AreAllSubWavesCleared());
    }

    /// <summary>
    /// Starts a sub-wave after a fixed offset from macro wave start.
    /// </summary>
    private IEnumerator RunSubWaveAfterDelay(HordeSubWave subWave, int subWaveId, float macroStartTime)
    {
        float targetTime = macroStartTime + Mathf.Max(0f, subWave.StartDelaySeconds);
        float remaining = targetTime - Time.time;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        yield return StartCoroutine(RunSubWaveSpawns(subWave, subWaveId));
        yield return new WaitUntil(() => IsSubWaveCleared(subWaveId));
    }

    /// <summary>
    /// Starts a sub-wave once the previous sequential sub-wave is cleared.
    /// </summary>
    private IEnumerator RunSubWaveSequential(HordeSubWave subWave, int subWaveId, int dependencyId)
    {
        if (dependencyId >= 0)
            yield return new WaitUntil(() => IsSubWaveCleared(dependencyId));

        float delay = Mathf.Max(0f, subWave.StartDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        yield return StartCoroutine(RunSubWaveSpawns(subWave, subWaveId));
        yield return new WaitUntil(() => IsSubWaveCleared(subWaveId));
    }

    /// <summary>
    /// Spawns all enemies for a single sub-wave honoring cadence and tracking sub-wave completion.
    /// </summary>
    private IEnumerator RunSubWaveSpawns(HordeSubWave subWave, int subWaveId)
    {
        List<WaveEnemyType> enemyTypes = BuildEnemyTypesForSubWave(subWave);
        if (enemyTypes.Count == 0)
        {
            MarkSubWaveSpawningComplete(subWaveId);
            yield break;
        }

        List<WaveSpawnAssignment> spawnAssignments = BuildSpawnAssignments(subWave, enemyTypes.Count);
        if (spawnAssignments.Count == 0)
        {
            MarkSubWaveSpawningComplete(subWaveId);
            yield break;
        }

        enemyTypeStatesBuffer.Clear();
        int totalRemaining = 0;
        for (int i = 0; i < enemyTypes.Count; i++)
        {
            WaveEnemyType type = enemyTypes[i];
            int count = Mathf.Max(0, type.EnemyCount);
            enemyTypeStatesBuffer.Add(new WaveEnemyTypeState(type.EnemyDefinition, type.RuntimeModifiers, type.SpawnOffset, count));
            totalRemaining += count;
        }

        UpdateSubWaveSpawnBudget(subWaveId, totalRemaining);
        if (totalRemaining == 0)
        {
            MarkSubWaveSpawningComplete(subWaveId);
            yield break;
        }

        float cadence = Mathf.Max(0.05f, subWave.SpawnCadenceSeconds);
        while (totalRemaining > 0)
        {
            bool spawnedThisCycle = false;
            int assignmentCount = spawnAssignments.Count;
            for (int i = 0; i < assignmentCount && totalRemaining > 0; i++)
            {
                WaveSpawnAssignment assignment = spawnAssignments[i];
                int typeIndex = ResolveNextEnemyTypeIndex(assignment, enemyTypeStatesBuffer);
                if (typeIndex < 0)
                    continue;

                WaveEnemyTypeState state = enemyTypeStatesBuffer[typeIndex];
                if (state.Definition == null || state.Definition.EnemyPool == null || state.RemainingCount <= 0)
                    continue;

                SpawnEnemyInstance(state.Definition, assignment.SpawnNode, state.Modifiers, state.SpawnOffset, subWaveId);
                state.RemainingCount--;
                enemyTypeStatesBuffer[typeIndex] = state;
                totalRemaining--;
                DecrementSubWaveSpawnBudget(subWaveId);
                spawnedThisCycle = true;
            }

            if (totalRemaining > 0)
            {
                if (!spawnedThisCycle)
                {
                    Debug.LogWarning("Wave spawn aborted: remaining enemies could not be matched to any spawn assignments. Check per-spawner enemy type lists.");
                    MarkSubWaveSpawningComplete(subWaveId);
                    yield break;
                }

                yield return new WaitForSeconds(cadence);
            }
        }

        MarkSubWaveSpawningComplete(subWaveId);
    }

    private List<WaveEnemyType> BuildEnemyTypesForSubWave(HordeSubWave subWave)
    {
        enemyTypesBuffer.Clear();

        IReadOnlyList<WaveEnemyType> configured = subWave.EnemyTypes;
        if (configured != null && configured.Count > 0)
        {
            int configuredCount = configured.Count;
            for (int i = 0; i < configuredCount; i++)
            {
                WaveEnemyType type = configured[i];
                if (type.EnemyDefinition != null && type.EnemyDefinition.EnemyPool != null && type.EnemyCount > 0)
                    enemyTypesBuffer.Add(type);
            }
        }

        if (enemyTypesBuffer.Count == 0 && subWave.HasLegacyEnemy && subWave.LegacyEnemyDefinition != null && subWave.LegacyEnemyDefinition.EnemyPool != null && subWave.LegacyEnemyCount > 0)
            enemyTypesBuffer.Add(new WaveEnemyType(subWave.LegacyEnemyDefinition, subWave.LegacyRuntimeModifiers, subWave.LegacyEnemyCount, subWave.LegacySpawnOffset));

        return enemyTypesBuffer;
    }

    private List<WaveSpawnAssignment> BuildSpawnAssignments(HordeSubWave subWave, int enemyTypeCount)
    {
        spawnAssignmentBuffer.Clear();
        if (enemyTypeCount <= 0)
            return spawnAssignmentBuffer;

        IReadOnlyList<WaveSpawnAssignment> configured = subWave.SpawnAssignments;
        if (configured != null && configured.Count > 0)
        {
            int configuredCount = configured.Count;
            for (int i = 0; i < configuredCount; i++)
            {
                WaveSpawnAssignment assignment = configured[i];
                List<int> allowedTypes = BuildValidatedAllowedTypes(assignment.AllowedEnemyTypeIndices, enemyTypeCount);
                spawnAssignmentBuffer.Add(new WaveSpawnAssignment(assignment.SpawnNode, allowedTypes));
            }
        }

        if (spawnAssignmentBuffer.Count == 0)
        {
            IReadOnlyList<Vector2Int> nodes = subWave.SpawnNodes;
            if (nodes != null && nodes.Count > 0)
            {
                List<int> defaultAllowedTypes = BuildDefaultAllowedTypes(enemyTypeCount);
                int nodeCount = nodes.Count;
                for (int i = 0; i < nodeCount; i++)
                    spawnAssignmentBuffer.Add(new WaveSpawnAssignment(nodes[i], new List<int>(defaultAllowedTypes)));
            }
        }

        return spawnAssignmentBuffer;
    }

    private List<int> BuildValidatedAllowedTypes(IReadOnlyList<int> source, int enemyTypeCount)
    {
        List<int> result = new List<int>();
        if (enemyTypeCount <= 0)
            return result;

        if (source != null)
        {
            int sourceCount = source.Count;
            for (int i = 0; i < sourceCount; i++)
            {
                int index = source[i];
                if (index >= 0 && index < enemyTypeCount && !result.Contains(index))
                    result.Add(index);
            }
        }

        if (result.Count == 0)
        {
            for (int i = 0; i < enemyTypeCount; i++)
                result.Add(i);
        }

        return result;
    }

    private List<int> BuildDefaultAllowedTypes(int enemyTypeCount)
    {
        List<int> result = new List<int>(enemyTypeCount);
        for (int i = 0; i < enemyTypeCount; i++)
            result.Add(i);
        return result;
    }

    /// <summary>
    /// Registers a sub-wave in the runtime dictionary with default counters.
    /// </summary>
    private void EnsureSubWaveState(int subWaveId, string label)
    {
        if (subWaveStates.ContainsKey(subWaveId))
            return;

        SubWaveRuntimeState state = new SubWaveRuntimeState(subWaveId, label);
        subWaveStates[subWaveId] = state;
    }

    /// <summary>
    /// Updates the remaining spawn budget for a sub-wave before emission begins.
    /// </summary>
    private void UpdateSubWaveSpawnBudget(int subWaveId, int totalToSpawn)
    {
        if (!subWaveStates.TryGetValue(subWaveId, out SubWaveRuntimeState state))
            return;

        state.RemainingToSpawn = totalToSpawn;
        subWaveStates[subWaveId] = state;
    }

    /// <summary>
    /// Decrements the tracked spawn budget as enemies are emitted.
    /// </summary>
    private void DecrementSubWaveSpawnBudget(int subWaveId)
    {
        if (!subWaveStates.TryGetValue(subWaveId, out SubWaveRuntimeState state))
            return;

        if (state.RemainingToSpawn > 0)
            state.RemainingToSpawn--;

        subWaveStates[subWaveId] = state;
    }

    /// <summary>
    /// Flags a sub-wave as having completed its spawn routine.
    /// </summary>
    private void MarkSubWaveSpawningComplete(int subWaveId)
    {
        if (!subWaveStates.TryGetValue(subWaveId, out SubWaveRuntimeState state))
            return;

        state.SpawnRoutineCompleted = true;
        state.RemainingToSpawn = 0;
        subWaveStates[subWaveId] = state;
    }

    /// <summary>
    /// Returns true when every tracked sub-wave has finished spawning and all of their enemies are cleared.
    /// </summary>
    private bool AreAllSubWavesCleared()
    {
        if (subWaveStates.Count == 0)
            return true;

        foreach (KeyValuePair<int, SubWaveRuntimeState> entry in subWaveStates)
        {
            if (!entry.Value.IsCleared)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true when a specific sub-wave has completed spawning and no active enemies remain.
    /// </summary>
    private bool IsSubWaveCleared(int subWaveId)
    {
        if (!subWaveStates.TryGetValue(subWaveId, out SubWaveRuntimeState state))
            return true;

        return state.IsCleared;
    }

    /// <summary>
    /// Builds incremental identifiers for sub-wave tracking.
    /// </summary>
    private int GetNextSubWaveId()
    {
        int id = nextSubWaveId;
        nextSubWaveId++;
        return id;
    }

    private int ResolveNextEnemyTypeIndex(in WaveSpawnAssignment assignment, List<WaveEnemyTypeState> states)
    {
        IReadOnlyList<int> allowedTypes = assignment.AllowedEnemyTypeIndices;
        if (allowedTypes == null || allowedTypes.Count == 0)
            return GetFirstAvailableEnemyTypeIndex(states);

        int allowedCount = allowedTypes.Count;
        for (int i = 0; i < allowedCount; i++)
        {
            int typeIndex = allowedTypes[i];
            if (typeIndex >= 0 && typeIndex < states.Count && states[typeIndex].RemainingCount > 0)
                return typeIndex;
        }

        return -1;
    }

    private int GetFirstAvailableEnemyTypeIndex(List<WaveEnemyTypeState> states)
    {
        int count = states.Count;
        for (int i = 0; i < count; i++)
        {
            if (states[i].RemainingCount > 0)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Resolves context data and spawns one enemy instance at the requested spawn node.
    /// </summary>
    private void SpawnEnemyInstance(EnemyClassDefinition definition, Vector2Int coords, EnemyRuntimeModifiers modifiers, Vector3 spawnOffset, int subWaveId)
    {
        EnemyPoolSO pool = definition.EnemyPool;
        if (pool == null)
            return;

        Vector3 position = ResolveSpawnPosition(coords);
        Quaternion rotation = ResolveSpawnRotation(coords);
        Transform parent = ResolveSpawnParent(coords);
        EnemySpawnContext context = new EnemySpawnContext(definition, position, rotation, parent, modifiers, spawnOffset);
        context = context.WithSubWaveId(subWaveId);
        pool.Spawn(definition, context);
    }

    /// <summary>
    /// Returns the spawn position for a spawn coordinate.
    /// </summary>
    private Vector3 ResolveSpawnPosition(Vector2Int coords)
    {
        if (grid == null)
            return Vector3.zero;

        return grid.GridToWorld(coords.x, coords.y);
    }

    /// <summary>
    /// Returns the spawn rotation for a spawn coordinate.
    /// </summary>
    private Quaternion ResolveSpawnRotation(Vector2Int coords)
    {
        if (grid == null)
            return Quaternion.identity;

        return Quaternion.identity;
    }

    /// <summary>
    /// Returns the parent transform for a spawn coordinate.
    /// </summary>
    private Transform ResolveSpawnParent(Vector2Int coords)
    {
        if (grid == null)
            return null;

        return grid.transform;
    }

    /// <summary>
    /// Handles post-horde cleanup, victory, or phase rollback to building.
    /// </summary>
    private void FinalizeHordeCompletion()
    {
        bool defeatRegistered = false;
        Player.PlayerHealth health = GetPlayerHealth();
        if (health != null)
        {
            health.RegisterHordeDefeat();
            defeatRegistered = true;
        }

        if (!defeatRegistered)
            EventsManager.InvokeIncreaseCompletedHordesCounter();

        if (HasPendingHordes)
        {
            GameManager targetManager = gameManager != null ? gameManager : GameManager.Instance;
            if (targetManager != null)
                targetManager.ForcePhase(GamePhase.Building);
        }
        else
        {
            EventsManager.InvokeGameVictoryAchieved();
        }
    }

    /// <summary>
    /// Locates the player health component once and caches it.
    /// </summary>
    private PlayerHealth GetPlayerHealth()
    {
        if (cachedPlayerHealth != null)
            return cachedPlayerHealth;

       // Debug.LogError("Missing serialized variable 'cachedPlayerHealth' in HordesManager. Recurring to reflection as an emergency measure.");
        cachedPlayerHealth = FindFirstObjectByType<Player.PlayerHealth>(FindObjectsInactive.Exclude);
        return cachedPlayerHealth;
    }

    private struct WaveEnemyTypeState
    {
        public EnemyClassDefinition Definition;
        public EnemyRuntimeModifiers Modifiers;
        public Vector3 SpawnOffset;
        public int RemainingCount;

        public WaveEnemyTypeState(EnemyClassDefinition definition, EnemyRuntimeModifiers modifiers, Vector3 spawnOffset, int remainingCount)
        {
            Definition = definition;
            Modifiers = modifiers;
            SpawnOffset = spawnOffset;
            RemainingCount = remainingCount;
        }
    }

    /// <summary>
    /// Tracks runtime counters for an active sub-wave.
    /// </summary>
    private struct SubWaveRuntimeState
    {
        public int SubWaveId;
        public int RemainingToSpawn;
        public int AliveCount;
        public bool SpawnRoutineCompleted;
        public string Label;

        public bool IsCleared
        {
            get { return SpawnRoutineCompleted && AliveCount <= 0 && RemainingToSpawn <= 0; }
        }

        public SubWaveRuntimeState(int subWaveId, string label)
        {
            SubWaveId = subWaveId;
            RemainingToSpawn = 0;
            AliveCount = 0;
            SpawnRoutineCompleted = false;
            Label = string.IsNullOrWhiteSpace(label) ? "Sub-Wave" : label;
        }
    }
    #endregion
    #endregion
}
