using Enemy;
using Grid;
using Scriptables.Enemies;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives navigation from spawn to the nearest goal using Dijkstra paths with smooth interpolation and rotation.
/// </summary>
[DisallowMultipleComponent]
public class EnemyMovement : MonoBehaviour
{
    #region Variables And Properties
    #region Serialized Fields

    [Tooltip("Scriptable asset containing interpolation, animation, and debug settings for navigation.")]
    [SerializeField] private EnemyMovementSettings movementSettings;

    [Tooltip("Transform rotated to face the direction of travel; defaults to this transform when null.")]
    [SerializeField] private Transform orientationTarget;

    [Tooltip("Animator updated with movement speed for locomotion blending.")]
    [SerializeField] private Animator animator;

    #endregion

    #region Runtime

    private Grid3D grid;
    private PooledEnemy pooledEnemy;
    private EnemyStatSnapshot activeStats;
    private readonly List<Vector3> worldPath = new List<Vector3>(32);
    private int pathCursor;
    private bool movementActive;
    private bool contactLocked;
    private float contactTimer;
    private Player.PlayerHealth lockedPlayer;
    private float lockedContactRange;
    private float spawnHeightOffset;
    private readonly Collider[] occupancyBuffer = new Collider[8];
    private float repathCooldownTimer;

    #endregion
    #endregion

    #region Methods
    #region Unity

    /// <summary>
    /// Caches component references used for movement and path generation.
    /// </summary>
    private void Awake()
    {
        grid = Grid3D.Instance;
        pooledEnemy = GetComponent<PooledEnemy>();
    }

    /// <summary>
    /// Fallback initialization when the component is enabled without pooling lifecycle.
    /// </summary>
    private void OnEnable()
    {
        if (pooledEnemy != null)
            return;

        BeginMovement(default);
    }

    /// <summary>
    /// Advances navigation each frame with smoothing and rotation alignment.
    /// </summary>
    private void Update()
    {
        float deltaTime = Time.deltaTime;
        UpdateContactLock(deltaTime);
        UpdateRepathCooldown(deltaTime);

        if (!movementActive)
            return;

        if (contactLocked)
        {
            UpdateAnimator(0f);
            return;
        }

        TickMovement(deltaTime);
    }

    #endregion

    #region Public

    /// <summary>
    /// Starts navigation from the current position using provided stats for speed evaluation.
    /// </summary>
    public void BeginMovement(in EnemyStatSnapshot stats)
    {
        activeStats = stats;
        contactLocked = false;
        contactTimer = 0f;
        lockedPlayer = null;
        lockedContactRange = 0f;
        BuildPath();
        ResolveSpawnHeightOffset();
        pathCursor = 0;
        movementActive = worldPath.Count > 0;
        UpdateAnimator(0f);
    }

    /// <summary>
    /// Stops navigation temporarily when contact with the player is detected.
    /// </summary>
    public void HandleContactEngagement(Player.PlayerHealth playerHealth, float contactRange)
    {
        lockedPlayer = playerHealth;
        lockedContactRange = Mathf.Max(0f, contactRange);
        contactTimer = ResolveContactStopDuration();
        contactLocked = true;
        UpdateAnimator(0f);
    }

    #endregion

    #region Internal

    /// <summary>
    /// Requests a path from the grid singleton and caches it for travel.
    /// </summary>
    private void BuildPath()
    {
        if (grid == null)
            grid = Grid3D.Instance;

        if (grid == null)
        {
            worldPath.Clear();
            return;
        }

        grid.TryBuildPathToClosestGoal(transform.position, worldPath);
    }

    /// <summary>
    /// Handles contact lock timing and resume conditions.
    /// </summary>
    private void UpdateContactLock(float deltaTime)
    {
        if (!contactLocked)
            return;

        if (lockedPlayer == null || !lockedPlayer.gameObject.activeInHierarchy)
        {
            contactLocked = false;
            lockedPlayer = null;
            return;
        }

        if (contactTimer > 0f)
            contactTimer -= deltaTime;

        if (contactTimer > 0f)
            return;

        float allowedRange = lockedContactRange;
        if (allowedRange <= 0f)
        {
            contactLocked = false;
            lockedPlayer = null;
            return;
        }

        Vector3 delta = lockedPlayer.transform.position - transform.position;
        float squaredDistance = delta.sqrMagnitude;
        float squaredRange = allowedRange * allowedRange;
        if (squaredDistance > squaredRange)
        {
            contactLocked = false;
            lockedPlayer = null;
        }
    }

    /// <summary>
    /// Advances the enemy along its cached path with smooth interpolation.
    /// </summary>
    private void TickMovement(float deltaTime)
    {
        if (worldPath.Count == 0 || pathCursor >= worldPath.Count)
        {
            movementActive = false;
            UpdateAnimator(0f);
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = worldPath[pathCursor];
        Vector3 targetPositionWithOffset = ApplyHeightOffset(targetPosition);
        Vector3 direction = targetPositionWithOffset - currentPosition;
        float distance = direction.magnitude;
        float waypointTolerance = ResolveWaypointTolerance();

        if (distance <= waypointTolerance)
        {
            pathCursor++;
            if (pathCursor >= worldPath.Count)
            {
                movementActive = false;
                UpdateAnimator(0f);
                return;
            }

            targetPositionWithOffset = ApplyHeightOffset(worldPath[pathCursor]);
            direction = targetPositionWithOffset - currentPosition;
            distance = direction.magnitude;
        }

        if (IsWaypointBlocked(targetPositionWithOffset))
        {
            AlignOrientation(direction, deltaTime);
            UpdateAnimator(0f);
            if (TryRepathAroundBlock())
            {
                return;
            }
            return;
        }

        float moveSpeed = ResolveMoveSpeed();
        float stepDistance = moveSpeed * deltaTime;
        float lerpFactor = ResolvePositionLerp(deltaTime);
        if (lerpFactor <= 0f)
            lerpFactor = 1f;

        Vector3 desiredPosition = distance <= stepDistance ? targetPositionWithOffset : currentPosition + direction.normalized * stepDistance;
        Vector3 smoothedPosition = Vector3.Lerp(currentPosition, desiredPosition, lerpFactor);
        transform.position = smoothedPosition;
        AlignOrientation(direction, deltaTime);
        UpdateAnimator(moveSpeed);
    }

    /// <summary>
    /// Aligns the configured transform to face the movement direction.
    /// </summary>
    private void AlignOrientation(Vector3 direction, float deltaTime)
    {
        Transform targetTransform = orientationTarget != null ? orientationTarget : transform;
        if (targetTransform == null)
            return;

        Vector3 planarDirection = new Vector3(direction.x, 0f, direction.z);
        if (planarDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
        float rotationSpeed = ResolveRotationSpeed();
        if (rotationSpeed <= 0f)
        {
            targetTransform.rotation = desiredRotation;
            return;
        }

        targetTransform.rotation = Quaternion.RotateTowards(targetTransform.rotation, desiredRotation, rotationSpeed * deltaTime);
    }

    /// <summary>
    /// Updates the animator speed parameter for locomotion blending.
    /// </summary>
    private void UpdateAnimator(float speed)
    {
        if (animator == null)
            return;

        string parameter = movementSettings != null ? movementSettings.AnimationSpeedParameter : "Speed";
        float speedFactor = movementSettings != null ? movementSettings.AnimationSpeedFactor : 0f;
        float normalizedSpeed = Mathf.Max(0f, speed * speedFactor);
        animator.SetFloat(parameter, normalizedSpeed);
    }

    /// <summary>
    /// Computes the smoothed interpolation factor based on settings and delta time.
    /// </summary>
    private float ResolvePositionLerp(float deltaTime)
    {
        float lerpSpeed = movementSettings != null ? movementSettings.PositionLerpSpeed : 8f;
        return 1f - Mathf.Exp(-lerpSpeed * deltaTime);
    }

    /// <summary>
    /// Returns the rotation speed in degrees per second.
    /// </summary>
    private float ResolveRotationSpeed()
    {
        return movementSettings != null ? movementSettings.RotationLerpSpeed : 480f;
    }

    /// <summary>
    /// Returns the waypoint tolerance in meters.
    /// </summary>
    private float ResolveWaypointTolerance()
    {
        return movementSettings != null ? movementSettings.WaypointTolerance : 0.05f;
    }

    /// <summary>
    /// Returns the movement speed based on stats or fallback settings.
    /// </summary>
    private float ResolveMoveSpeed()
    {
        float speed = activeStats.MovementSpeed > 0f ? activeStats.MovementSpeed : 0f;
        if (speed <= 0f && movementSettings != null)
            speed = movementSettings.FallbackSpeed;

        return Mathf.Max(0.01f, speed);
    }

    /// <summary>
    /// Returns the stop duration applied after contact with the player.
    /// </summary>
    private float ResolveContactStopDuration()
    {
        return movementSettings != null ? movementSettings.ContactStopDuration : 0.5f;
    }

    /// <summary>
    /// Returns the occupancy probe radius used to block movement into filled cells.
    /// </summary>
    private float ResolveOccupancyRadius()
    {
        return movementSettings != null ? movementSettings.OccupancyProbeRadius : 0.35f;
    }

    /// <summary>
    /// Returns the layer mask used while probing occupancy.
    /// </summary>
    private int ResolveOccupancyLayerMask()
    {
        return movementSettings != null ? movementSettings.OccupancyLayerMask : ~0;
    }

    /// <summary>
    /// Prevents entering a waypoint already occupied by another enemy.
    /// </summary>
    private bool IsWaypointBlocked(Vector3 waypointPosition)
    {
        float radius = ResolveOccupancyRadius();
        if (radius <= 0f)
            return false;

        int mask = ResolveOccupancyLayerMask();
        int found = Physics.OverlapSphereNonAlloc(waypointPosition, radius, occupancyBuffer, mask, QueryTriggerInteraction.Collide);
        if (found <= 0)
            return false;

        for (int i = 0; i < found; i++)
        {
            Collider candidate = occupancyBuffer[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            EnemyMovement neighbour = candidate.GetComponentInParent<EnemyMovement>();
            if (neighbour == null || neighbour == this)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates vertical offset at spawn to keep relative height along the path.
    /// </summary>
    private void ResolveSpawnHeightOffset()
    {
        if (worldPath.Count == 0)
        {
            spawnHeightOffset = 0f;
            return;
        }

        spawnHeightOffset = transform.position.y - worldPath[0].y;
    }

    /// <summary>
    /// Applies cached spawn height offset to a target waypoint.
    /// </summary>
    private Vector3 ApplyHeightOffset(Vector3 waypoint)
    {
        return new Vector3(waypoint.x, waypoint.y + spawnHeightOffset, waypoint.z);
    }

    /// <summary>
    /// Attempts to rebuild the path around an occupied waypoint.
    /// </summary>
    private bool TryRepathAroundBlock()
    {
        if (repathCooldownTimer > 0f)
            return false;

        BuildPath();
        ResolveSpawnHeightOffset();
        pathCursor = 0;
        movementActive = worldPath.Count > 0;
        repathCooldownTimer = 0.35f;
        return movementActive;
    }

    /// <summary>
    /// Updates the cooldown preventing constant replanning every frame.
    /// </summary>
    private void UpdateRepathCooldown(float deltaTime)
    {
        if (repathCooldownTimer > 0f)
            repathCooldownTimer -= deltaTime;
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// Renders the cached navigation path for debugging purposes.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (movementSettings == null || !movementSettings.DrawPathGizmos)
            return;

        if (worldPath == null || worldPath.Count < 2)
            return;

        Color original = Gizmos.color;
        Gizmos.color = movementSettings.PathGizmoColor;

        int nodeCount = worldPath.Count;
        for (int i = 0; i < nodeCount - 1; i++)
            Gizmos.DrawLine(worldPath[i], worldPath[i + 1]);

        Gizmos.color = original;
    }

    #endregion
    #endregion
}
