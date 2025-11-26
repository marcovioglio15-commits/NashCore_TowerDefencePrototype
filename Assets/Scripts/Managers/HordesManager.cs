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
    #endregion

    #region Properties
    public bool HasPendingHordes
    {
        get { return currentHordeIndex + 1 < hordes.Count; }
    }
    #endregion

    #region Unity
    /// <summary>
    /// Subscribes to phase changes to automatically start hordes.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        EventsManager.GamePhaseChanged += HandlePhaseChanged;
    }

    /// <summary>
    /// Removes subscriptions when disabled.
    /// </summary>
    private void OnDisable()
    {
        EventsManager.GamePhaseChanged -= HandlePhaseChanged;
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
        if (phase != GamePhase.Defence)
            return;

        BeginNextHorde();
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
        for (int i = 0; i < count; i++)
        {
            Vector2Int coords = spawnNodes[i % spawnNodes.Count];
            SpawnEnemyInstance(enemyDefinition, coords, wave.RuntimeModifiers);
            if (i < count - 1)
                yield return new WaitForSeconds(cadence);
        }
    }

    /// <summary>
    /// Resolves context data and spawns one enemy instance at the requested spawn node.
    /// </summary>
    private void SpawnEnemyInstance(EnemyClassDefinition definition, Vector2Int coords, EnemyRuntimeModifiers modifiers)
    {
        EnemyPoolSO pool = definition.EnemyPool;
        if (pool == null)
            return;

        Vector3 position = ResolveSpawnPosition(coords);
        Quaternion rotation = ResolveSpawnRotation(coords);
        Transform parent = ResolveSpawnParent(coords);
        EnemySpawnContext context = new EnemySpawnContext(definition, position, rotation, parent, modifiers);
        pool.Spawn(definition, context);
    }

    /// <summary>
    /// Returns the spawn position for a spawn coordinate.
    /// </summary>
    private Vector3 ResolveSpawnPosition(Vector2Int coords)
    {
        if (grid == null)
            return Vector3.zero;

        Transform anchor = grid.GetSpawnPoint(coords);
        if (anchor != null)
            return anchor.position;

        return grid.GridToWorld(coords.x, coords.y);
    }

    /// <summary>
    /// Returns the spawn rotation for a spawn coordinate.
    /// </summary>
    private Quaternion ResolveSpawnRotation(Vector2Int coords)
    {
        if (grid == null)
            return Quaternion.identity;

        Transform anchor = grid.GetSpawnPoint(coords);
        if (anchor != null)
            return anchor.rotation;

        return Quaternion.identity;
    }

    /// <summary>
    /// Returns the parent transform for a spawn coordinate.
    /// </summary>
    private Transform ResolveSpawnParent(Vector2Int coords)
    {
        if (grid == null)
            return null;

        Transform anchor = grid.GetSpawnPoint(coords);
        if (anchor != null)
            return anchor.parent;

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
}

/// <summary>
/// Describes when the next wave should start after the current one finishes spawning.
/// </summary>
public enum WaveAdvanceMode
{
    FixedInterval,
    AfterClear
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
    public IReadOnlyList<Vector2Int> SpawnNodes { get { return spawnNodes != null ? spawnNodes : System.Array.Empty<Vector2Int>(); } }
    public WaveAdvanceMode AdvanceMode { get { return advanceMode; } }
    public float AdvanceDelaySeconds { get { return advanceDelaySeconds; } }
}
