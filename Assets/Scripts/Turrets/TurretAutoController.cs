using System.Collections;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Automatic targeting and firing controller that keeps pooled turrets engaging enemies without manual input.
    /// </summary>
    [DisallowMultipleComponent]
    public class TurretAutoController : MonoBehaviour
    {
        #region Variables And Properties
        #region Serialized Fields
        [Tooltip("Turret component supplying placement, transforms and fire configuration.")]
        [SerializeField] private PooledTurret pooledTurret;
        [Tooltip("Layer mask representing enemies that should be tracked and engaged.")]
        [SerializeField] private LayerMask enemyLayers = ~0;
        [Tooltip("Maximum number of colliders processed per scan iteration.")]
        [SerializeField] private int maxScanColliders = 16;
        [Tooltip("Fallback cadence used whenever the definition cadence is missing or invalid.")]
        [SerializeField] private float fallbackCadenceSeconds = 0.35f;
        [Tooltip("Cone width in degrees required before engaging automatic fire.")]
        [SerializeField] private float fireArcDegrees = 12f;
        [Tooltip("Interval in seconds used to verify whether enemies remain within the fire area while locked.")]
        [SerializeField] private float fireLockCheckInterval = 0.2f;
        [Tooltip("Transforms aligned to the current automatic fire direction.")]
        [Header("Alignment")]
        [SerializeField] private Transform[] autoFireAlignmentRoots;
        [Tooltip("Degrees per second used to smoothly align automatic fire transforms.")]
        [SerializeField] private float autoAlignmentLerpSpeed = 240f;
        [Tooltip("Draws targeting debug gizmos when the turret is selected.")]
        [SerializeField] private bool drawDebugGizmos = true;
        #endregion

        #region Runtime
        private Collider[] scanBuffer;
        private Collider activeTarget;
        private float retargetTimer;
        private float fireTimer;
        private Coroutine burstRoutine;
        private Vector3 lastAimPoint;
        private Vector3 lastAlignmentDirection;
        private bool fireLockActive;
        private float fireLockTimer;
        private bool phaseAutoEnabled = true;
        #endregion
        #endregion

        #region Methods
        #region Unity
        /// <summary>
        /// Ensures runtime dependencies are cached before spawning from pools.
        /// </summary>
        private void Awake()
        {
            if (pooledTurret == null)
                pooledTurret = GetComponent<PooledTurret>();

            int bufferSize = Mathf.Max(1, maxScanColliders);
            scanBuffer = new Collider[bufferSize];
        }

        /// <summary>
        /// Resets runtime timers when the component becomes active.
        /// </summary>
        private void OnEnable()
        {
            retargetTimer = 0f;
            fireTimer = 0f;
            activeTarget = null;
            lastAimPoint = Vector3.zero;
            lastAlignmentDirection = Vector3.zero;
            fireLockActive = false;
            fireLockTimer = 0f;
            EventsManager.GamePhaseChanged += HandleGamePhaseChanged;
            SyncPhaseState();
        }

        /// <summary>
        /// Stops firing routines whenever the turret is disabled or despawned.
        /// </summary>
        private void OnDisable()
        {
            if (burstRoutine != null)
                StopCoroutine(burstRoutine);

            burstRoutine = null;
            activeTarget = null;
            lastAlignmentDirection = Vector3.zero;
            EventsManager.GamePhaseChanged -= HandleGamePhaseChanged;
        }

        /// <summary>
        /// Handles retargeting cadence, aim blending and fire scheduling.
        /// </summary>
        private void Update()
        {
            if (pooledTurret == null || !pooledTurret.HasDefinition)
                return;

            if (!phaseAutoEnabled)
                return;

            float deltaTime = Time.deltaTime;
            TurretStatSnapshot stats = pooledTurret.ActiveStats;
            pooledTurret.CooldownHeat(deltaTime);

            Vector3 direction = Vector3.zero;

            if (fireLockActive)
            {
                UpdateFireLock(stats, deltaTime);
                if (!fireLockActive)
                    retargetTimer = 0f;

                if (!TryResolveLockedDirection(stats, out direction))
                    return;
            }
            else
            {
                retargetTimer -= deltaTime;
                if (retargetTimer <= 0f)
                {
                    retargetTimer = Mathf.Max(0.05f, stats.RetargetInterval);
                    AcquireTarget(stats);
                }

                if (!ValidateActiveTarget(stats))
                    return;

                Vector3 rawOffset = activeTarget.bounds.center - pooledTurret.transform.position;
                Vector3 horizontalOffset = ProjectToHorizontal(rawOffset);
                if (horizontalOffset.sqrMagnitude <= Mathf.Epsilon)
                    return;

                direction = horizontalOffset;

                if (!IsWithinFireArc(direction))
                {
                    pooledTurret.AimTowards(direction, deltaTime);
                    return;
                }

                pooledTurret.AimTowards(direction, deltaTime);
            }

            UpdateAlignment(direction, deltaTime);
            lastAimPoint = pooledTurret.transform.position + direction;

            fireTimer -= deltaTime;
            float cadence = Mathf.Max(fallbackCadenceSeconds, stats.AutomaticCadenceSeconds);
            if (fireTimer <= 0f)
            {
                fireTimer = cadence;
                BeginVolley(direction.normalized, stats);
                fireLockActive = true;
                fireLockTimer = fireLockCheckInterval;
            }
        }
        #endregion

        #region Targeting
        /// <summary>
        /// Projects a world-space direction onto the horizontal plane.
        /// </summary>
        private Vector3 ProjectToHorizontal(Vector3 direction)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                return Vector3.zero;

            Vector3 projected = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (projected.sqrMagnitude <= Mathf.Epsilon)
                return Vector3.zero;

            return projected;
        }

        /// <summary>
        /// Attempts to select the closest valid enemy, preferring the current one if still inside the horizontal fire arc.
        /// </summary>
        private void AcquireTarget(TurretStatSnapshot stats)
        {
            if (pooledTurret == null || !pooledTurret.HasDefinition)
                return;

            if (activeTarget != null && ValidateActiveTarget(stats))
            {
                Vector3 currentOffset = activeTarget.bounds.center - pooledTurret.transform.position;
                Vector3 currentHorizontal = ProjectToHorizontal(currentOffset);
                if (currentHorizontal.sqrMagnitude > Mathf.Epsilon && IsWithinFireArc(currentHorizontal))
                    return;
            }

            int hits = Physics.OverlapSphereNonAlloc(pooledTurret.transform.position, stats.Range, scanBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            Collider bestCollider = null;

            for (int i = 0; i < hits; i++)
            {
                Collider candidate = scanBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = candidate.bounds.center - pooledTurret.transform.position;
                Vector3 horizontalOffset = ProjectToHorizontal(offset);
                if (horizontalOffset.sqrMagnitude <= Mathf.Epsilon)
                    continue;

                float distance = horizontalOffset.magnitude;
                if (distance <= stats.DeadZoneRadius || distance >= closestDistance)
                    continue;

                bestCollider = candidate;
                closestDistance = distance;
            }

            activeTarget = bestCollider;
        }

        /// <summary>
        /// Validates that current target is still available and within range.
        /// </summary>
        private bool ValidateActiveTarget(TurretStatSnapshot stats)
        {
            if (pooledTurret == null || !pooledTurret.HasDefinition)
                return false;

            if (activeTarget == null || !activeTarget.gameObject.activeInHierarchy)
                return false;

            Vector3 offset = activeTarget.bounds.center - pooledTurret.transform.position;
            float sqrDistance = offset.sqrMagnitude;
            float maxRange = stats.Range;
            if (sqrDistance > maxRange * maxRange)
                return false;

            if (sqrDistance < stats.DeadZoneRadius * stats.DeadZoneRadius)
                return false;

            return true;
        }

        /// <summary>
        /// Maintains the fire lock and clears it when no enemies remain in the area of fire.
        /// </summary>
        private void UpdateFireLock(TurretStatSnapshot stats, float deltaTime)
        {
            fireLockTimer -= deltaTime;
            if (fireLockTimer > 0f)
                return;

            fireLockTimer = Mathf.Max(0.05f, fireLockCheckInterval);
            if (HasAnyTargetInFireArc(stats))
                return;

            fireLockActive = false;
            activeTarget = null;
        }

        /// <summary>
        /// Checks whether any enemy remains inside the fire area and arc.
        /// </summary>
        private bool HasAnyTargetInFireArc(TurretStatSnapshot stats)
        {
            if (pooledTurret == null)
                return false;

            int hits = Physics.OverlapSphereNonAlloc(pooledTurret.transform.position, stats.Range, scanBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
            if (hits == 0)
                return false;

            Transform yawTransform = pooledTurret.YawPivot != null ? pooledTurret.YawPivot : pooledTurret.transform;
            Vector3 forward = yawTransform.forward;
            Vector3 forwardHorizontal = ProjectToHorizontal(forward);
            if (forwardHorizontal.sqrMagnitude <= Mathf.Epsilon)
                return false;

            Vector3 forwardNormalized = forwardHorizontal.normalized;
            float maxAngle = Mathf.Max(1f, fireArcDegrees);
            float maxCos = Mathf.Cos(maxAngle * Mathf.Deg2Rad * 0.5f);

            for (int i = 0; i < hits; i++)
            {
                Collider candidate = scanBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = candidate.bounds.center - pooledTurret.transform.position;
                Vector3 horizontalOffset = ProjectToHorizontal(offset);
                if (horizontalOffset.sqrMagnitude <= Mathf.Epsilon)
                    continue;

                float distance = horizontalOffset.magnitude;
                if (distance <= stats.DeadZoneRadius || distance > stats.Range)
                    continue;

                Vector3 offsetNormalized = horizontalOffset.normalized;
                float dot = Vector3.Dot(forwardNormalized, offsetNormalized);
                if (dot < maxCos)
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a valid target direction while fire lock is active without rotating the turret vertically.
        /// </summary>
        private bool TryResolveLockedDirection(TurretStatSnapshot stats, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (activeTarget != null && ValidateActiveTarget(stats))
            {
                Vector3 offset = activeTarget.bounds.center - pooledTurret.transform.position;
                Vector3 horizontalOffset = ProjectToHorizontal(offset);
                if (horizontalOffset.sqrMagnitude > Mathf.Epsilon && IsWithinFireArc(horizontalOffset))
                {
                    direction = horizontalOffset;
                    return true;
                }
            }

            Collider replacement;
            if (!TryAcquireLockedTarget(stats, out replacement, out direction))
                return false;

            activeTarget = replacement;
            return true;
        }

        /// <summary>
        /// Selects any enemy within the horizontal fire arc to continue firing while locked.
        /// </summary>
        private bool TryAcquireLockedTarget(TurretStatSnapshot stats, out Collider target, out Vector3 direction)
        {
            target = null;
            direction = Vector3.zero;
            if (pooledTurret == null)
                return false;

            int hits = Physics.OverlapSphereNonAlloc(pooledTurret.transform.position, stats.Range, scanBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
            if (hits == 0)
                return false;

            Transform yawTransform = pooledTurret.YawPivot != null ? pooledTurret.YawPivot : pooledTurret.transform;
            Vector3 forward = yawTransform.forward;
            Vector3 forwardHorizontal = ProjectToHorizontal(forward);
            if (forwardHorizontal.sqrMagnitude <= Mathf.Epsilon)
                return false;

            Vector3 forwardNormalized = forwardHorizontal.normalized;
            float maxAngle = Mathf.Max(1f, fireArcDegrees);
            float maxCos = Mathf.Cos(maxAngle * Mathf.Deg2Rad * 0.5f);
            float bestDot = maxCos;

            for (int i = 0; i < hits; i++)
            {
                Collider candidate = scanBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = candidate.bounds.center - pooledTurret.transform.position;
                Vector3 horizontalOffset = ProjectToHorizontal(offset);
                if (horizontalOffset.sqrMagnitude <= Mathf.Epsilon)
                    continue;

                float distance = horizontalOffset.magnitude;
                if (distance <= stats.DeadZoneRadius || distance > stats.Range)
                    continue;

                Vector3 offsetNormalized = horizontalOffset.normalized;
                float dot = Vector3.Dot(forwardNormalized, offsetNormalized);
                if (dot < bestDot)
                    continue;

                bestDot = dot;
                target = candidate;
                direction = horizontalOffset;
            }

            return target != null;
        }

        /// <summary>
        /// Checks whether the provided direction lies within the configured horizontal fire arc.
        /// </summary>
        private bool IsWithinFireArc(Vector3 direction)
        {
            Transform yawTransform = pooledTurret != null && pooledTurret.YawPivot != null ? pooledTurret.YawPivot : pooledTurret != null ? pooledTurret.transform : null;
            if (yawTransform == null)
                return false;

            Vector3 forward = yawTransform.forward;
            Vector3 forwardHorizontal = ProjectToHorizontal(forward);
            if (forwardHorizontal.sqrMagnitude <= Mathf.Epsilon)
                return false;

            Vector3 directionHorizontal = ProjectToHorizontal(direction);
            if (directionHorizontal.sqrMagnitude <= Mathf.Epsilon)
                return false;

            float maxAngle = Mathf.Max(1f, fireArcDegrees);
            float maxCos = Mathf.Cos(maxAngle * Mathf.Deg2Rad * 0.5f);
            float dot = Vector3.Dot(forwardHorizontal.normalized, directionHorizontal.normalized);
            return dot >= maxCos;
        }

        /// <summary>
        /// Aligns optional transforms to the current automatic firing direction.
        /// </summary>
        private void UpdateAlignment(Vector3 direction, float deltaTime)
        {
            if (autoFireAlignmentRoots == null || autoFireAlignmentRoots.Length == 0)
                return;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                return;

            Vector3 normalized = direction.normalized;
            Transform upSource = pooledTurret != null && pooledTurret.YawPivot != null ? pooledTurret.YawPivot : pooledTurret != null ? pooledTurret.transform : null;
            Vector3 up = upSource != null ? upSource.up : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(normalized, up);
            float maxDegrees = Mathf.Max(0f, autoAlignmentLerpSpeed) * Mathf.Max(0f, deltaTime);
            bool applyLerp = maxDegrees > 0f;

            for (int i = 0; i < autoFireAlignmentRoots.Length; i++)
            {
                Transform root = autoFireAlignmentRoots[i];
                if (root == null)
                    continue;

                if (applyLerp)
                {
                    root.rotation = Quaternion.RotateTowards(root.rotation, rotation, maxDegrees);
                    continue;
                }

                root.rotation = rotation;
            }

            lastAlignmentDirection = normalized;
        }
        #endregion

        #region Firing
        /// <summary>
        /// Starts a new volley respecting definition parameters.
        /// </summary>
        private void BeginVolley(Vector3 forward, TurretStatSnapshot stats)
        {
            if (burstRoutine != null)
                StopCoroutine(burstRoutine);

            burstRoutine = StartCoroutine(FireBurstRoutine(forward, stats));
        }

        /// <summary>
        /// Executes projectile spawning respecting pattern and delays.
        /// </summary>
        private IEnumerator FireBurstRoutine(Vector3 forward, TurretStatSnapshot stats)
        {
            if (pooledTurret == null || !pooledTurret.HasDefinition)
                yield break;

            ProjectileDefinition projectileDefinition = pooledTurret.Definition.Projectile;
            float splashRadius = projectileDefinition != null ? Mathf.Max(0f, projectileDefinition.SplashRadius) : 0f;
            TurretFirePattern pattern = stats.AutomaticPattern;
            int projectiles = Mathf.Max(1, stats.AutomaticProjectilesPerShot);
            WaitForSeconds interDelay = null;
            bool useDelay = (pattern == TurretFirePattern.Consecutive || pattern == TurretFirePattern.Bazooka) && stats.AutomaticInterProjectileDelay > 0f;
            if (useDelay)
                interDelay = new WaitForSeconds(stats.AutomaticInterProjectileDelay);

            Vector3 upAxis = ResolveAutomaticUpAxis();
            for (int i = 0; i < projectiles; i++)
            {
                Vector3 direction = TurretFireUtility.ResolveProjectileDirection(forward, pattern, splashRadius, i, projectiles, upAxis);
                float patternSplash = pattern == TurretFirePattern.Bazooka ? splashRadius : 0f;
                TurretFireUtility.SpawnProjectile(pooledTurret, direction, splashRadiusOverride: patternSplash);

                bool shouldDelay = useDelay && i < projectiles - 1;
                if (shouldDelay && interDelay != null)
                    yield return interDelay;
            }
        }

        /// <summary>
        /// Determines the up axis used for spreading automatic volleys.
        /// </summary>
        private Vector3 ResolveAutomaticUpAxis()
        {
            if (pooledTurret != null && pooledTurret.YawPivot != null)
                return pooledTurret.YawPivot.up;

            if (pooledTurret != null)
                return pooledTurret.transform.up;

            return Vector3.up;
        }
        #endregion

        #region Phase Control
        /// <summary>
        /// Enables or disables automatic behaviour based on the active game phase.
        /// </summary>
        public void SetPhaseAutoState(bool enabled)
        {
            phaseAutoEnabled = enabled;
            if (!phaseAutoEnabled)
            {
                if (burstRoutine != null)
                    StopCoroutine(burstRoutine);

                burstRoutine = null;
                activeTarget = null;
                fireLockActive = false;
                fireLockTimer = 0f;
                retargetTimer = 0f;
            }
        }

        /// <summary>
        /// Responds to global phase changes to align auto-fire availability.
        /// </summary>
        private void HandleGamePhaseChanged(GamePhase phase)
        {
            SetPhaseAutoState(phase == GamePhase.Defence);
        }

        /// <summary>
        /// Syncs the initial phase state when coming online before events fire.
        /// </summary>
        private void SyncPhaseState()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                return;

            SetPhaseAutoState(manager.CurrentPhase == GamePhase.Defence);
        }
        #endregion

        #region Gizmos
        /// <summary>
        /// Draws range and aim gizmos for debugging.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || pooledTurret == null || !pooledTurret.HasDefinition)
                return;

            TurretStatSnapshot stats = pooledTurret.ActiveStats;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(pooledTurret.transform.position, stats.Range);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pooledTurret.transform.position, stats.DeadZoneRadius);

            if (lastAimPoint != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(pooledTurret.transform.position, lastAimPoint);
            }
        }
        #endregion
        #endregion
    }
}
