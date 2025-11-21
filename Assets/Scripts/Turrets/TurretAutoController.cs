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
        private bool fireLockActive;
        private float fireLockTimer;
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
            fireLockActive = false;
            fireLockTimer = 0f;
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
        }

        /// <summary>
        /// Handles retargeting cadence, aim blending and fire scheduling.
        /// </summary>
        private void Update()
        {
            if (pooledTurret == null || !pooledTurret.HasDefinition)
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

                direction = activeTarget.bounds.center - pooledTurret.transform.position;
                if (direction.sqrMagnitude <= Mathf.Epsilon)
                    return;

                if (!IsWithinFireArc(direction))
                {
                    pooledTurret.AimTowards(direction, deltaTime);
                    return;
                }

                pooledTurret.AimTowards(direction, deltaTime);
            }

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
        /// Attempts to select the closest valid enemy within the cone of fire.
        /// </summary>
        private void AcquireTarget(TurretStatSnapshot stats)
        {
            if (pooledTurret == null || !pooledTurret.HasDefinition)
                return;

            int hits = Physics.OverlapSphereNonAlloc(pooledTurret.transform.position, stats.Range, scanBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            Collider bestCollider = null;

            for (int i = 0; i < hits; i++)
            {
                Collider candidate = scanBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = candidate.bounds.center - pooledTurret.transform.position;
                float distance = offset.magnitude;
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
        /// Checks whether any enemy remains inside the fire area and cone.
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
            float maxAngle = Mathf.Max(1f, fireArcDegrees);
            float maxCos = Mathf.Cos(maxAngle * Mathf.Deg2Rad * 0.5f);

            for (int i = 0; i < hits; i++)
            {
                Collider candidate = scanBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = candidate.bounds.center - pooledTurret.transform.position;
                float distance = offset.magnitude;
                if (distance <= stats.DeadZoneRadius || distance > stats.Range)
                    continue;

                float dot = Vector3.Dot(forward.normalized, offset.normalized);
                if (dot < maxCos)
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a valid target direction while fire lock is active without rotating the turret.
        /// </summary>
        private bool TryResolveLockedDirection(TurretStatSnapshot stats, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (activeTarget != null && ValidateActiveTarget(stats))
            {
                direction = activeTarget.bounds.center - pooledTurret.transform.position;
                if (direction.sqrMagnitude > Mathf.Epsilon && IsWithinFireArc(direction))
                    return true;
            }

            Collider replacement;
            if (!TryAcquireLockedTarget(stats, out replacement, out direction))
                return false;

            activeTarget = replacement;
            return true;
        }

        /// <summary>
        /// Selects any enemy within the fire arc to continue firing while locked.
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
            Vector3 forward = yawTransform.forward.normalized;
            float maxAngle = Mathf.Max(1f, fireArcDegrees);
            float maxCos = Mathf.Cos(maxAngle * Mathf.Deg2Rad * 0.5f);
            float bestDot = maxCos;
            for (int i = 0; i < hits; i++)
            {
                Collider candidate = scanBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = candidate.bounds.center - pooledTurret.transform.position;
                float distance = offset.magnitude;
                if (distance <= stats.DeadZoneRadius || distance > stats.Range)
                    continue;

                Vector3 offsetNormalized = offset.normalized;
                float dot = Vector3.Dot(forward, offsetNormalized);
                if (dot < bestDot)
                    continue;

                bestDot = dot;
                target = candidate;
                direction = offset;
            }

            return target != null;
        }

        /// <summary>
        /// Checks whether the provided direction lies within the configured fire arc.
        /// </summary>
        private bool IsWithinFireArc(Vector3 direction)
        {
            Transform yawTransform = pooledTurret != null && pooledTurret.YawPivot != null ? pooledTurret.YawPivot : pooledTurret != null ? pooledTurret.transform : null;
            if (yawTransform == null)
                return false;

            Vector3 forward = yawTransform.forward.normalized;
            Vector3 directionNormalized = direction.normalized;
            float maxAngle = Mathf.Max(1f, fireArcDegrees);
            float maxCos = Mathf.Cos(maxAngle * Mathf.Deg2Rad * 0.5f);
            float dot = Vector3.Dot(forward, directionNormalized);
            return dot >= maxCos;
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

            TurretFirePattern pattern = stats.AutomaticPattern;
            int projectiles = Mathf.Max(1, stats.AutomaticProjectilesPerShot);
            WaitForSeconds interDelay = null;
            bool useDelay = pattern == TurretFirePattern.Consecutive && stats.AutomaticInterProjectileDelay > 0f;
            if (useDelay)
                interDelay = new WaitForSeconds(stats.AutomaticInterProjectileDelay);

            Vector3 upAxis = ResolveAutomaticUpAxis();
            for (int i = 0; i < projectiles; i++)
            {
                Vector3 direction = TurretFireUtility.ResolveProjectileDirection(forward, pattern, stats.AutomaticConeAngleDegrees, i, projectiles, upAxis);
                TurretFireUtility.SpawnProjectile(pooledTurret, direction);

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
