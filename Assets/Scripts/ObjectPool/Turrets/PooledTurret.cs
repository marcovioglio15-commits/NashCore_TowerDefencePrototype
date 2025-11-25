using System;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Axis components applied when aligning turret visuals to possessed camera rotation.
    /// </summary>
    public enum FreeAimFollowAxis
    {
        HorizontalOnly,
        VerticalOnly,
        HorizontalAndVertical
    }

    /// <summary>
    /// Authored transform plus axis policy used during free-aim visual alignment.
    /// </summary>
    [Serializable]
    public struct FreeAimRotationFollower
    {
        [Tooltip("Transform rotated to mirror possessed camera orientation.")]
        public Transform Target;
        [Tooltip("Axis components of the camera rotation to apply on this transform.")]
        public FreeAimFollowAxis Axis;
    }

    /// <summary>
    /// Base pooled turret behaviour handling spawn context application, runtime bookkeeping and debug visualization.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledTurret : MonoBehaviour, IPoolable<PooledTurret, TurretSpawnContext>
    {
        #region Variables And Properties
        #region Serialized Fields

        [Tooltip("Default definition used when the spawn context does not provide one.")]
        [Header("Definitions")]
        [SerializeField] private TurretClassDefinition defaultDefinition;

        [Tooltip("Yaw transform rotated during target tracking.")]
        [Header("Transforms")]
        [SerializeField] private Transform yawRoot;

        [Tooltip("Pitch transform rotated during target tracking.")]
        [SerializeField] private Transform pitchRoot;

        [Tooltip("Projectile origin used for bazooka splash visualization and muzzle placement.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Renderers disabled while the player possesses the turret to avoid camera clipping.")]
        [Header("Free Aim Presentation")]
        [SerializeField] private Renderer[] freeAimHiddenRenderers;
        [Tooltip("Transforms aligned to the possessed camera with selectable axis following.")]
        [SerializeField] private FreeAimRotationFollower[] freeAimRotationFollowers;

        [Tooltip("Range gizmo color for editor previews.")]
        [Header("Debug")]
        [SerializeField] private Color rangeColor = new Color(0.15f, 0.85f, 1f, 0.45f);

        [Tooltip("Footprint gizmo color for placement previews.")]
        [SerializeField] private Color footprintColor = new Color(0.25f, 0.9f, 0.35f, 0.25f);

        #endregion

        #region Runtime

        public Action<PooledTurret> Despawn { get; set; }

        private TurretClassDefinition activeDefinition;
        private TurretSpawnContext lastContext;
        private float cooldownTimer;
        private float heatLevel;
        private TurretStatSnapshot activeStats;
        private bool freeAimActive;
        private Quaternion yawBaseRotation;
        private Renderer[] cachedRenderers;
        private FreeAimRotationFollower[] cachedFollowerFallback;

        #endregion

        #region Properties

        public TurretClassDefinition Definition
        {
            get { return activeDefinition; }
        }

        public TurretSpawnContext LastContext
        {
            get { return lastContext; }
        }

        public TurretStatSnapshot ActiveStats
        {
            get { return activeStats; }
        }

        public Transform YawPivot
        {
            get { return yawRoot != null ? yawRoot : transform; }
        }

        public Transform PitchPivot
        {
            get { return pitchRoot; }
        }

        public bool IsInFreeAim
        {
            get { return freeAimActive; }
        }

        public bool HasDefinition
        {
            get { return activeDefinition != null; }
        }

        /// <summary>
        /// Provides read-only access to the muzzle transform used for projectile spawns.
        /// </summary>
        public Transform Muzzle
        {
            get { return muzzle; }
        }

        /// <summary>
        /// Renderer set explicitly hidden when the player possesses this turret.
        /// </summary>
        public Renderer[] FreeAimHiddenRenderers
        {
            get { return freeAimHiddenRenderers; }
        }

        /// <summary>
        /// Optional transforms rotated to follow the possessed camera yaw when visuals remain visible.
        /// </summary>
        public FreeAimRotationFollower[] FreeAimRotationFollowers
        {
            get { return freeAimRotationFollowers; }
        }

        #endregion
        #endregion

        #region Methods
        #region IPoolable

        /// <summary>
        /// Callback executed by the pool when the object is queued for reuse.
        /// </summary>
        public void OnDespawn()
        {
            if (Despawn != null)
                Despawn.Invoke(this);
        }

        /// <summary>
        /// Spawn without custom context uses the configured default definition.
        /// </summary>
        public PooledTurret OnSpawn()
        {
            TurretSpawnContext fallbackContext = new TurretSpawnContext(defaultDefinition, transform.position, transform.rotation, transform.parent);
            PooledTurret spawned = OnSpawn(fallbackContext);
            return spawned;
        }

        /// <summary>
        /// Applies the provided spawn context and re-initializes runtime state.
        /// </summary>
        public PooledTurret OnSpawn(TurretSpawnContext context)
        {
            lastContext = context;
            freeAimActive = false;
            cachedRenderers = null;
            cachedFollowerFallback = null;
            ApplyDefinition(context.Definition != null ? context.Definition : defaultDefinition);
            if (!HasDefinition)
            {
                Debug.LogWarning("Turret spawn aborted: missing definition.", this);
                return this;
            }

            ApplyTransform(context);
            CacheYawBaseRotation();
            cooldownTimer = 0f;
            heatLevel = 0f;
            return this;
        }

        /// <summary>
        /// Resets transient state before the instance is re-enqueued in the pool.
        /// </summary>
        public void ResetPoolable()
        {
            cooldownTimer = 0f;
            heatLevel = 0f;
            lastContext = new TurretSpawnContext(defaultDefinition, Vector3.zero, Quaternion.identity, null);
            activeDefinition = defaultDefinition;
            freeAimActive = false;
            RebuildStats();
            CacheYawBaseRotation();
            cachedRenderers = null;
            cachedFollowerFallback = null;
        }

        #endregion

        #region Public 

        /// <summary>
        /// Assigns a default definition to be used when pool spawning without explicit context.
        /// </summary>
        public void AssignDefaultDefinition(TurretClassDefinition definition)
        {
            defaultDefinition = definition;
        }

        /// <summary>
        /// Requests the pool to despawn this turret.
        /// </summary>
        public void RequestDespawn()
        {
            OnDespawn();
        }

        /// <summary>
        /// Applies yaw and pitch based on a desired world forward direction.
        /// </summary>
        public void AimTowards(Vector3 forward, float deltaTime)
        {
            if (!HasDefinition)
                return;

            if (yawRoot == null && pitchRoot == null)
                return;

            if (activeStats.TurnRate <= 0f)
                return;

            float maxDegrees = activeStats.TurnRate * deltaTime;
            Vector3 planarForward = new Vector3(forward.x, 0f, forward.z);
            if (planarForward.sqrMagnitude > 0f && yawRoot != null)
            {
                Quaternion desired = Quaternion.LookRotation(planarForward.normalized, Vector3.up);
                yawRoot.rotation = Quaternion.RotateTowards(yawRoot.rotation, desired, maxDegrees);
                ClampYawRotation(activeStats.YawClampDegrees);
            }

            if (pitchRoot != null)
            {
                Vector3 localForward = yawRoot != null ? yawRoot.InverseTransformDirection(forward) : forward;
                Vector3 horizontal = new Vector3(localForward.x, 0f, localForward.z).normalized;
                float dot = Vector3.Dot(localForward.normalized, horizontal);
                float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg * Mathf.Sign(localForward.y);
                Quaternion desiredPitch = Quaternion.Euler(-angle, 0f, 0f);
                pitchRoot.localRotation = Quaternion.RotateTowards(pitchRoot.localRotation, desiredPitch, maxDegrees);
            }
        }

        /// <summary>
        /// Small helper to clamp fire cadence timers externally without per-frame checks.
        /// </summary>
        public void ReduceCooldown(float deltaTime)
        {
            if (cooldownTimer <= 0f)
                return;

            cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
        }

        /// <summary>
        /// Starts a new cooldown using the active fire mode cadence.
        /// </summary>
        public void StartCooldown(float cadenceSeconds)
        {
            cooldownTimer = Mathf.Max(cadenceSeconds, 0f);
        }

        /// <summary>
        /// Tracks heat accumulation during sustained fire.
        /// </summary>
        public void AddHeat(float amount)
        {
            heatLevel = Mathf.Clamp(heatLevel + amount, 0f, activeStats.MaxHeat);
        }

        /// <summary>
        /// Dissipates heat over time to avoid per-frame Update usage.
        /// </summary>
        public void CooldownHeat(float deltaTime)
        {
            if (activeStats.HeatDissipationSeconds <= 0f)
                return;

            float dissipation = deltaTime / activeStats.HeatDissipationSeconds * activeStats.MaxHeat;
            heatLevel = Mathf.Max(0f, heatLevel - dissipation);
        }

        /// <summary>
        /// Toggles manual control mode and rebuilds the active stat snapshot.
        /// </summary>
        public void SetFreeAimState(bool enabled)
        {
            freeAimActive = enabled;
            RebuildStats();
        }

        /// <summary>
        /// Returns the renderer collection to toggle during possession, defaulting to all renderers.
        /// </summary>
        public Renderer[] GetFreeAimRendererSet()
        {
            if (freeAimHiddenRenderers != null && freeAimHiddenRenderers.Length > 0)
                return freeAimHiddenRenderers;

            if (cachedRenderers == null || cachedRenderers.Length == 0)
                cachedRenderers = GetComponentsInChildren<Renderer>(true);

            return cachedRenderers;
        }

        /// <summary>
        /// Returns authored rotation followers with axis selection, falling back to yaw-only when unspecified.
        /// </summary>
        public FreeAimRotationFollower[] GetFreeAimRotationFollowers()
        {
            if (freeAimRotationFollowers != null && freeAimRotationFollowers.Length > 0)
                return freeAimRotationFollowers;

            Transform yawTarget = yawRoot != null ? yawRoot : transform;
            if (cachedFollowerFallback == null || cachedFollowerFallback.Length != 1)
                cachedFollowerFallback = new FreeAimRotationFollower[1];

            cachedFollowerFallback[0].Target = yawTarget;
            cachedFollowerFallback[0].Axis = FreeAimFollowAxis.HorizontalOnly;
            return cachedFollowerFallback;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!HasDefinition)
                return;

            if (activeStats.Range <= 0f)
                RebuildStats();

            Gizmos.color = rangeColor;
            Gizmos.DrawWireSphere(transform.position, activeStats.Range);

            Gizmos.color = footprintColor;
            Gizmos.DrawWireSphere(transform.position, activeStats.FootprintRadius);

            if (muzzle == null)
                return;

            ProjectileDefinition projectileDefinition = Definition.Projectile;
            float splashRadius = projectileDefinition != null ? Mathf.Max(0f, projectileDefinition.SplashRadius) : 0f;
            if (Definition.AutomaticFire.Pattern == TurretFirePattern.Bazooka && splashRadius > 0f)
                DrawSplashGizmo(splashRadius);

            if (Definition.FreeAimFire.Pattern == TurretFirePattern.Bazooka && splashRadius > 0f)
                DrawSplashGizmo(splashRadius);

            DrawRotationFollowerGizmos();
        }

        /// <summary>
        /// Draws a wireframe sphere preview for bazooka splash radius.
        /// </summary>
        private void DrawSplashGizmo(float radius)
        {
            float clampedRadius = Mathf.Max(0f, radius);
            if (clampedRadius <= 0f)
                return;

            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(muzzle.position, clampedRadius);
        }

        /// <summary>
        /// Visualizes authored free-aim rotation followers with axis hints.
        /// </summary>
        private void DrawRotationFollowerGizmos()
        {
            FreeAimRotationFollower[] followers = GetFreeAimRotationFollowers();
            if (followers == null || followers.Length == 0)
                return;

            for (int i = 0; i < followers.Length; i++)
            {
                Transform follower = followers[i].Target;
                if (follower == null)
                    continue;

                Gizmos.color = new Color(0.15f, 0.6f, 1f, 0.55f);
                Gizmos.DrawWireSphere(follower.position, 0.07f);
                DrawRotationAxisLines(follower, followers[i].Axis);
            }
        }

        /// <summary>
        /// Draws axis lines describing the rotation policy for a follower.
        /// </summary>
        private void DrawRotationAxisLines(Transform follower, FreeAimFollowAxis axis)
        {
            if (follower == null)
                return;

            float scale = 0.25f;
            bool yawEnabled = axis == FreeAimFollowAxis.HorizontalOnly || axis == FreeAimFollowAxis.HorizontalAndVertical;
            bool pitchEnabled = axis == FreeAimFollowAxis.VerticalOnly || axis == FreeAimFollowAxis.HorizontalAndVertical;

            if (yawEnabled)
            {
                Gizmos.color = new Color(0.15f, 0.85f, 0.55f, 0.7f);
                Gizmos.DrawLine(follower.position, follower.position + follower.up * scale);
            }

            if (pitchEnabled)
            {
                Gizmos.color = new Color(0.95f, 0.55f, 0.2f, 0.7f);
                Gizmos.DrawLine(follower.position, follower.position + follower.right * scale);
            }
        }

        #endregion

        #region Internal

        /// <summary>
        /// Applies the provided definition to runtime state.
        /// </summary>
        private void ApplyDefinition(TurretClassDefinition definition)
        {
            if (definition == null)
                return;

            activeDefinition = definition;
            RebuildStats();
        }

        /// <summary>
        /// Sets transform placement based on the spawn context.
        /// </summary>
        private void ApplyTransform(TurretSpawnContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            if (context.Parent != null)
                transform.SetParent(context.Parent, true);
            else
                transform.SetParent(null, false);
        }

        /// <summary>
        /// Recomputes runtime stats using the active definition and free-aim flag.
        /// </summary>
        private void RebuildStats()
        {
            if (activeDefinition == null)
            {
                activeStats = default;
                return;
            }

            activeStats = TurretStatSnapshot.Create(activeDefinition, freeAimActive);
        }

        /// <summary>
        /// Stores the yaw rotation used as clamp reference.
        /// </summary>
        private void CacheYawBaseRotation()
        {
            if (yawRoot != null)
                yawBaseRotation = yawRoot.rotation;
            else
                yawBaseRotation = transform.rotation;
        }

        /// <summary>
        /// Restricts yaw rotation around the cached baseline.
        /// </summary>
        private void ClampYawRotation(float clampDegrees)
        {
            if (clampDegrees <= 0f)
                return;

            if (yawRoot == null)
                return;

            float maxAngle = clampDegrees * 0.5f;
            float angle = Quaternion.Angle(yawBaseRotation, yawRoot.rotation);
            if (angle <= maxAngle)
                return;

            yawRoot.rotation = Quaternion.RotateTowards(yawBaseRotation, yawRoot.rotation, maxAngle);
        }

        #endregion
        #endregion
    }
}
