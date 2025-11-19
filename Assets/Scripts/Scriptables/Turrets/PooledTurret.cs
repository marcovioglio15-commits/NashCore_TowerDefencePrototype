using System;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Base pooled turret behaviour handling spawn context application, runtime bookkeeping and debug visualization.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledTurret : MonoBehaviour, IPoolable<PooledTurret, TurretSpawnContext>
    {
        #region Variables And Properties
        #region Serialized Fields

        [Header("Definitions")]

        [SerializeField]
        [Tooltip("Default definition used when the spawn context does not provide one.")]
        private TurretClassDefinition defaultDefinition;

        [Header("Transforms")]

        [SerializeField]
        [Tooltip("Yaw transform rotated during target tracking.")]
        private Transform yawRoot;

        [SerializeField]
        [Tooltip("Pitch transform rotated during target tracking.")]
        private Transform pitchRoot;

        [SerializeField]
        [Tooltip("Projectile origin used for cone visualization and muzzle placement.")]
        private Transform muzzle;

        [Header("Debug")]

        [SerializeField]
        [Tooltip("Range gizmo color for editor previews.")]
        private Color rangeColor = new Color(0.15f, 0.85f, 1f, 0.45f);

        [SerializeField]
        [Tooltip("Footprint gizmo color for placement previews.")]
        private Color footprintColor = new Color(0.25f, 0.9f, 0.35f, 0.25f);

        #endregion

        #region Runtime

        public Action<PooledTurret> Despawn { get; set; }

        private TurretClassDefinition activeDefinition;
        private TurretSpawnContext lastContext;
        private float cooldownTimer;
        private float heatLevel;

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

        public bool HasDefinition
        {
            get { return activeDefinition != null; }
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
            ApplyDefinition(context.Definition != null ? context.Definition : defaultDefinition);
            if (!HasDefinition)
            {
                Debug.LogWarning("Turret spawn aborted: missing definition.", this);
                return this;
            }

            ApplyTransform(context);
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
        }

        #endregion

        #region Public API

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

            float maxDegrees = Definition.Targeting.TurnRate * deltaTime;
            Vector3 planarForward = new Vector3(forward.x, 0f, forward.z);
            if (planarForward.sqrMagnitude > 0f && yawRoot != null)
            {
                Quaternion desired = Quaternion.LookRotation(planarForward.normalized, Vector3.up);
                yawRoot.rotation = Quaternion.RotateTowards(yawRoot.rotation, desired, maxDegrees);
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
            heatLevel = Mathf.Clamp(heatLevel + amount, 0f, Definition.Sustain.MaxHeat);
        }

        /// <summary>
        /// Dissipates heat over time to avoid per-frame Update usage.
        /// </summary>
        public void CooldownHeat(float deltaTime)
        {
            if (Definition.Sustain.HeatDissipationSeconds <= 0f)
                return;

            float dissipation = deltaTime / Definition.Sustain.HeatDissipationSeconds * Definition.Sustain.MaxHeat;
            heatLevel = Mathf.Max(0f, heatLevel - dissipation);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!HasDefinition)
                return;

            Gizmos.color = rangeColor;
            Gizmos.DrawWireSphere(transform.position, Definition.Targeting.Range);

            Gizmos.color = footprintColor;
            Gizmos.DrawWireSphere(transform.position, Definition.Placement.FootprintRadius);

            if (muzzle == null)
                return;

            if (Definition.AutomaticFire.ProjectilesPerShot > 1 && Definition.AutomaticFire.Pattern == TurretFirePattern.Cone)
                DrawConeGizmo(Definition.AutomaticFire.ConeAngleDegrees, Definition.Targeting.Range);

            if (Definition.FreeAimFire.ProjectilesPerShot > 1 && Definition.FreeAimFire.Pattern == TurretFirePattern.Cone)
                DrawConeGizmo(Definition.FreeAimFire.ConeAngleDegrees, Definition.Targeting.Range * 0.75f);
        }

        /// <summary>
        /// Draws a wireframe cone preview from the muzzle.
        /// </summary>
        private void DrawConeGizmo(float angle, float length)
        {
            Vector3 forward = muzzle.forward;
            Quaternion leftRotation = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up);
            Quaternion rightRotation = Quaternion.AngleAxis(angle * 0.5f, Vector3.up);
            Vector3 left = leftRotation * forward * length;
            Vector3 right = rightRotation * forward * length;

            Gizmos.DrawLine(muzzle.position, muzzle.position + left);
            Gizmos.DrawLine(muzzle.position, muzzle.position + right);
            Gizmos.DrawWireSphere(muzzle.position + forward * length, Definition.Placement.Clearance + 0.05f);
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

        #endregion
        #endregion
    }
}
