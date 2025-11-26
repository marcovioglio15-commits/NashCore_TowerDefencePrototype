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
    }
    #endregion

    #region Internal
    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Defence)
        {
            //CloseAllSpawnDoors();
            BeginNextHorde();
            return;
        }

        if (phase == GamePhase.Building)
            PreviewNextWaveDoors();
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
        if (previewNodes == null || previewNodes.Count == 0)
        {
            //CloseAllSpawnDoors();
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
            IReadOnlyList<Vector2Int> nodes = waves[i].SpawnNodes;
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
        if (defenceStartDelay > 0f)
            yield return new WaitForSeconds(defenceStartDelay);

        IReadOnlyList<HordeWave> waves = definition.Waves;
        for (int i = 0; i < waves.Count; i++)
        {
            HordeWave wave = waves[i];
            yield return StartCoroutine(SpawnWave(wave));

            if (wave.AdvanceMode == WaveAdvanceMode.AfterClear)
            {
                yield return new WaitUntil(() => activeEnemies == 0);
                if (wave.AdvanceDelaySeconds > 0f)
                    yield return new WaitForSeconds(wave.AdvanceDelaySeconds);
            }
            else
            {
                if (wave.AdvanceDelaySeconds > 0f)
                    yield return new WaitForSeconds(wave.AdvanceDelaySeconds);
            }
        }

        yield return new WaitUntil(() => activeEnemies == 0);
        FinalizeHordeCompletion();
        hordeActive = false;
        hordeRoutine = null;
    }

    /// <summary>
    /// Spawns all enemies for a single wave honoring cadence.
    /// </summary>
    private IEnumerator SpawnWave(HordeWave wave)
    {
        EnemyClassDefinition enemyDefinition = wave.EnemyDefinition;
        if (enemyDefinition == null || enemyDefinition.EnemyPool == null)
            yield break;

        IReadOnlyList<Vector2Int> spawnNodes = wave.SpawnNodes;
        if (spawnNodes == null || spawnNodes.Count == 0)
            yield break;

        int count = Mathf.Max(1, wave.EnemyCount);
        float cadence = Mathf.Max(0.05f, wave.SpawnCadenceSeconds);
        int spawned = 0;
        int nodeCount = spawnNodes.Count;
        while (spawned < count)
        {
            for (int n = 0; n < nodeCount && spawned < count; n++)
            {
                Vector2Int coords = spawnNodes[n];
                SpawnEnemyInstance(enemyDefinition, coords, wave.RuntimeModifiers, wave.SpawnOffset);
                spawned++;
            }

            if (spawned < count)
                yield return new WaitForSeconds(cadence);
        }
    }

    /// <summary>
    /// Resolves context data and spawns one enemy instance at the requested spawn node.
    /// </summary>
    private void SpawnEnemyInstance(EnemyClassDefinition definition, Vector2Int coords, EnemyRuntimeModifiers modifiers, Vector3 spawnOffset)
    {
        EnemyPoolSO pool = definition.EnemyPool;
        if (pool == null)
            return;

        Vector3 position = ResolveSpawnPosition(coords);
        Quaternion rotation = ResolveSpawnRotation(coords);
        Transform parent = ResolveSpawnParent(coords);
        EnemySpawnContext context = new EnemySpawnContext(definition, position, rotation, parent, modifiers, spawnOffset);
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
    #endregion
    #endregion
}
