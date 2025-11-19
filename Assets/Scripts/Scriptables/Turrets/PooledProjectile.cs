using System;
using System.Collections;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Pooled projectile handling spawn context application, basic flight bootstrap and auto-despawn scheduling.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PooledProjectile : MonoBehaviour, IPoolable<PooledProjectile, ProjectileSpawnContext>
    {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("Default definition used when no explicit projectile definition is provided.")]
        private ProjectileDefinition defaultDefinition;

        [SerializeField]
        [Tooltip("Cached rigidbody used to apply initial velocity without per-frame updates.")]
        private Rigidbody body;

        [SerializeField]
        [Tooltip("Debug color for max distance gizmo.")]
        private Color gizmoColor = new Color(1f, 0.5f, 0.1f, 0.45f);

        #endregion

        #region Runtime

        public Action<PooledProjectile> Despawn { get; set; }

        private ProjectileDefinition activeDefinition;
        private ProjectileSpawnContext lastContext;
        private float scheduledDespawnSeconds;
        private int remainingPierces;
        private Coroutine despawnRoutine;

        #endregion

        #region Properties

        public ProjectileDefinition Definition
        {
            get { return activeDefinition; }
        }

        public ProjectileSpawnContext LastContext
        {
            get { return lastContext; }
        }

        public bool HasDefinition
        {
            get { return activeDefinition != null; }
        }

        #endregion

        #region Unity

        private void Reset()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
        }

        #endregion

        #region IPoolable

        /// <summary>
        /// Requests despawn through the pool.
        /// </summary>
        public void OnDespawn()
        {
            if (Despawn != null)
                Despawn.Invoke(this);
        }

        /// <summary>
        /// Spawns using the default definition and current transform orientation.
        /// </summary>
        public PooledProjectile OnSpawn()
        {
            ProjectileSpawnContext context = new ProjectileSpawnContext(defaultDefinition, transform.position, transform.forward, 1f, transform.parent);
            PooledProjectile spawned = OnSpawn(context);
            return spawned;
        }

        /// <summary>
        /// Applies spawn context, updates velocity and schedules automatic despawn.
        /// </summary>
        public PooledProjectile OnSpawn(ProjectileSpawnContext context)
        {
            lastContext = context;
            ApplyDefinition(context.Definition != null ? context.Definition : defaultDefinition);
            if (!HasDefinition)
            {
                Debug.LogWarning("Projectile spawn aborted: missing definition.", this);
                return this;
            }

            ApplyTransform(context);
            ApplyVelocity(context);
            remainingPierces = Mathf.Max(0, Definition.MaxPiercedTargets);
            ScheduleAutoDespawn();
            return this;
        }

        /// <summary>
        /// Resets transient runtime state.
        /// </summary>
        public void ResetPoolable()
        {
            if (despawnRoutine != null)
                StopCoroutine(despawnRoutine);

            despawnRoutine = null;
            scheduledDespawnSeconds = 0f;
            remainingPierces = 0;
            lastContext = new ProjectileSpawnContext(defaultDefinition, Vector3.zero, Vector3.forward, 1f, null);
            activeDefinition = defaultDefinition;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Reports a hit and returns remaining pierce slots.
        /// </summary>
        public int RegisterHit()
        {
            if (remainingPierces > 0)
                remainingPierces--;

            if (remainingPierces <= 0)
                OnDespawn();

            return remainingPierces;
        }

        /// <summary>
        /// Calculates final damage including crits for the next hit.
        /// </summary>
        public float ResolveDamage(System.Random random)
        {
            if (random == null)
                return Definition.Damage;

            float roll = (float)random.NextDouble();
            if (roll <= Definition.CriticalChance)
                return Definition.Damage * Definition.CriticalMultiplier;

            return Definition.Damage;
        }

        /// <summary>
        /// Assigns a default definition used when spawning without explicit context.
        /// </summary>
        public void AssignDefaultDefinition(ProjectileDefinition definition)
        {
            defaultDefinition = definition;
            activeDefinition = defaultDefinition;
        }

        #endregion

        #region Gizmos

        /// <summary>
        /// Draws distance and splash previews for debug purposes.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!HasDefinition)
                return;

            Gizmos.color = gizmoColor;
            float fallbackSpeed = Definition.Speed;
            float distance = Definition.MaxDistance > 0f ? Definition.MaxDistance : fallbackSpeed * Definition.LifetimeSeconds;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * distance);
            Gizmos.DrawWireSphere(transform.position, Definition.SplashRadius);
        }

        #endregion

        #region Internal

        /// <summary>
        /// Applies the provided definition to runtime state.
        /// </summary>
        private void ApplyDefinition(ProjectileDefinition definition)
        {
            if (definition == null)
                return;

            activeDefinition = definition;
        }

        /// <summary>
        /// Sets transform placement and parenting based on spawn context.
        /// </summary>
        private void ApplyTransform(ProjectileSpawnContext context)
        {
            transform.SetPositionAndRotation(context.Position, Quaternion.LookRotation(context.Direction, Vector3.up));
            if (context.Parent != null)
                transform.SetParent(context.Parent, true);
            else
                transform.SetParent(null, false);
        }

        /// <summary>
        /// Configures rigidbody velocity from spawn context.
        /// </summary>
        private void ApplyVelocity(ProjectileSpawnContext context)
        {
            if (body == null)
                return;

            float speedMultiplier = Mathf.Max(0f, context.SpeedMultiplier);
            float velocity = Definition.Speed * speedMultiplier;
            body.linearVelocity = context.Direction.normalized * velocity;
        }

        /// <summary>
        /// Schedules automatic despawn based on lifetime or distance budget.
        /// </summary>
        private void ScheduleAutoDespawn()
        {
            if (!HasDefinition)
                return;

            if (despawnRoutine != null)
                StopCoroutine(despawnRoutine);

            float speed = Mathf.Max(0.01f, Definition.Speed);
            float travelSeconds = Definition.MaxDistance > 0f ? Definition.MaxDistance / speed : Definition.LifetimeSeconds;
            scheduledDespawnSeconds = Mathf.Min(travelSeconds, Definition.LifetimeSeconds);
            despawnRoutine = StartCoroutine(AutoDespawnRoutine(scheduledDespawnSeconds));
        }

        /// <summary>
        /// Coroutine that waits and then despawns the projectile.
        /// </summary>
        private IEnumerator AutoDespawnRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            OnDespawn();
        }

        #endregion
    }
}
