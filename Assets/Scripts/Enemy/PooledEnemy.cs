using System;
using UnityEngine;
using Scriptables.Enemies;
using Utils.Combat;

namespace Enemy
{
    /// <summary>
    /// Base pooled enemy behaviour applying spawn context, maintaining snapshots, and exposing debug gizmos.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledEnemy : MonoBehaviour, IPoolable<PooledEnemy, EnemySpawnContext>, IDamagable
    {
        #region Variables And Properties
        #region Serialized Fields

        [Tooltip("Default definition used when the spawn context omits one.")]
        [Header("Definitions")]
        [SerializeField] private EnemyClassDefinition defaultDefinition;

        [Tooltip("Anchor used when rendering debug gizmos; defaults to the transform when null.")]
        [Header("Debug")]
        [SerializeField] private Transform gizmoAnchor;

        [Tooltip("Color used to visualize the special effect trigger radius.")]
        [SerializeField] private Color specialEffectGizmoColor = new Color(0.95f, 0.4f, 0.2f, 0.2f);
        #endregion

        #region Runtime

        public Action<PooledEnemy> Despawn { get; set; }

        private EnemyClassDefinition activeDefinition;
        private EnemySpawnContext lastContext;
        private EnemyStatSnapshot activeStats;
        private float currentHealth;
        private float contactProbeTimer;
        private float contactEffectCooldownTimer;
        private readonly Collider[] contactBuffer = new Collider[8];
        private const float ContactProbeIntervalSeconds = 0.25f;
        private const float ContactEffectCooldownSeconds = 1.1f;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the active definition applied on spawn.
        /// </summary>
        public EnemyClassDefinition Definition
        {
            get { return activeDefinition; }
        }

        /// <summary>
        /// Returns the snapshot computed for this enemy instance.
        /// </summary>
        public EnemyStatSnapshot ActiveStats
        {
            get { return activeStats; }
        }

        /// <summary>
        /// Returns the last applied spawn context.
        /// </summary>
        public EnemySpawnContext LastContext
        {
            get { return lastContext; }
        }

        #endregion
        #endregion

        #region Methods
        #region IPoolable

        /// <summary>
        /// Callback executed when the object is queued for reuse.
        /// </summary>
        public void OnDespawn()
        {
            HordesManager instance = HordesManager.Instance;
            if (instance != null)
                instance.NotifyEnemyDespawned(this);

            if (Despawn != null)
            {
                Despawn.Invoke(this);
            }
        }

        /// <summary>
        /// Spawn without a custom context uses the configured default definition.
        /// </summary>
        public PooledEnemy OnSpawn()
        {
            EnemySpawnContext fallbackContext = new EnemySpawnContext(defaultDefinition, transform.position, transform.rotation, transform.parent, EnemyRuntimeModifiers.Identity, Vector3.zero);
            PooledEnemy spawned = OnSpawn(fallbackContext);
            return spawned;
        }

        /// <summary>
        /// Applies the provided spawn context and re-initializes runtime state.
        /// </summary>
        public PooledEnemy OnSpawn(EnemySpawnContext context)
        {
            lastContext = context;
            ApplyTransform(context);
            EnemyClassDefinition resolvedDefinition = context.Definition;
            if (resolvedDefinition == null)
            {
                resolvedDefinition = defaultDefinition;
            }

            ApplyDefinition(resolvedDefinition);
            if (activeDefinition == null)
            {
                Debug.LogWarning("Enemy spawn aborted: missing definition.", this);
                return this;
            }

            currentHealth = activeStats.MaxHealth;
            contactProbeTimer = 0f;
            contactEffectCooldownTimer = 0f;
            HordesManager instance = HordesManager.Instance;
            if (instance != null)
                instance.NotifyEnemySpawned(this);

            return this;
        }

        /// <summary>
        /// Resets transient state before the instance is re-enqueued in the pool.
        /// </summary>
        public void ResetPoolable()
        {
            EnemySpawnContext resetContext = new EnemySpawnContext(defaultDefinition, Vector3.zero, Quaternion.identity, null, EnemyRuntimeModifiers.Identity, Vector3.zero);
            lastContext = resetContext;
            activeDefinition = defaultDefinition;
            activeStats = EnemyStatSnapshot.Create(defaultDefinition, EnemyRuntimeModifiers.Identity);
            currentHealth = activeStats.MaxHealth;
            contactProbeTimer = 0f;
            contactEffectCooldownTimer = 0f;
        }

        #endregion

        #region Unity

        /// <summary>
        /// Periodically probes for contact interactions with the player.
        /// </summary>
        private void Update()
        {
            if (activeDefinition == null)
                return;

            float deltaTime = Time.deltaTime;
            if (contactProbeTimer > 0f)
                contactProbeTimer -= deltaTime;
            if (contactEffectCooldownTimer > 0f)
                contactEffectCooldownTimer -= deltaTime;

            if (contactProbeTimer > 0f)
                return;

            contactProbeTimer = ContactProbeIntervalSeconds;
            EvaluateContactRange();
        }

        #endregion

        #region Public

        /// <summary>
        /// Assigns the default definition used when no context definition is supplied.
        /// </summary>
        public void AssignDefaultDefinition(EnemyClassDefinition definition)
        {
            defaultDefinition = definition;
        }

        /// <summary>
        /// Receives incoming damage from turrets or other sources.
        /// </summary>
        public void ApplyDamage(IDamage damageSource, Vector3 hitPoint)
        {
            if (activeDefinition == null)
                return;

            float incomingDamage = damageSource != null ? Mathf.Max(0f, damageSource.DamageAmount) : 0f;
            if (incomingDamage <= 0f)
                return;

            float negation = Mathf.Clamp(activeStats.DamageNegationPercent, -1f, 0.95f);
            float effectiveDamage = incomingDamage * (1f - negation);
            if (effectiveDamage <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - effectiveDamage);
            if (currentHealth <= 0f)
                HandleDeath();
        }

        #endregion

        #region Private

        private void ApplyDefinition(EnemyClassDefinition definition)
        {
            activeDefinition = definition;
            activeStats = EnemyStatSnapshot.Create(definition, lastContext.RuntimeModifiers);
        }

        private void ApplyTransform(EnemySpawnContext context)
        {
            Vector3 finalPosition = context.Position + context.SpawnOffset;
            transform.SetPositionAndRotation(finalPosition, context.Rotation);
            if (context.Parent != null)
            {
                transform.SetParent(context.Parent, true);
            }
        }

        private void OnDrawGizmosSelected()
        {
            EnemyClassDefinition gizmoDefinition = activeDefinition != null ? activeDefinition : defaultDefinition;
            if (gizmoDefinition == null)
            {
                return;
            }

            float radius = Mathf.Max(0f, gizmoDefinition.Contact.ContactRange);
            if (radius <= 0f)
            {
                return;
            }

            Transform anchor = gizmoAnchor != null ? gizmoAnchor : transform;
            Gizmos.color = specialEffectGizmoColor;
            Gizmos.DrawWireSphere(anchor.position, radius);
        }

        /// <summary>
        /// Probes for player targets within the configured contact radius.
        /// </summary>
        private void EvaluateContactRange()
        {
            float radius = activeStats.ContactRange;
            if (radius <= 0f)
                return;

            Transform anchor = gizmoAnchor != null ? gizmoAnchor : transform;
            int found = Physics.OverlapSphereNonAlloc(anchor.position, radius, contactBuffer, ~0, QueryTriggerInteraction.Collide);
            if (found <= 0)
                return;

            for (int i = 0; i < found; i++)
            {
                Collider candidate = contactBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Player.PlayerHealth playerHealth = candidate.GetComponentInParent<Player.PlayerHealth>();
                if (playerHealth == null)
                    continue;

                ApplyContactToPlayer(playerHealth);
                break;
            }
        }

        /// <summary>
        /// Applies contact damage and special effects to the provided player target.
        /// </summary>
        private void ApplyContactToPlayer(Player.PlayerHealth playerHealth)
        {
            if (playerHealth == null)
                return;

            if (contactEffectCooldownTimer > 0f)
                return;

            contactEffectCooldownTimer = ContactEffectCooldownSeconds;

            if (activeStats.ShieldDamage > 0f)
            {
                EnemyContactDamage payload = new EnemyContactDamage(activeStats.ShieldDamage, gameObject);
                playerHealth.ApplyDamage(payload, transform.position);
            }

            if (activeStats.SpecialEffect == EnemySpecialEffect.HalvesShield)
                playerHealth.ApplyShieldHalvingEffect();
        }

        /// <summary>
        /// Finalizes the kill flow for this enemy instance.
        /// </summary>
        private void HandleDeath()
        {
            OnDespawn();
        }

        #endregion
        #endregion

        #region Nested Struct
        /// <summary>
        /// Payload used when enemies damage the player on contact.
        /// </summary>
        internal readonly struct EnemyContactDamage : IDamage
        {
            public float DamageAmount { get; }
            public float CriticalChance { get { return 0f; } }
            public float CriticalMultiplier { get { return 1f; } }
            public GameObject Source { get; }

            public EnemyContactDamage(float damageAmount, GameObject source)
            {
                DamageAmount = Mathf.Max(0f, damageAmount);
                Source = source;
            }
        }
        #endregion
    }

}
