using System;
using UnityEngine;
using Utils.Combat;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Pooled projectile that advances manually, probes collisions with sphere casts, and applies damage without physics components.
    /// </summary>
    public class PooledProjectile : MonoBehaviour, IPoolable<PooledProjectile, ProjectileSpawnContext>, IDamage
    {
        #region Variables And Properties
        #region Serialized Fields
        [Tooltip("Default definition used when no explicit projectile definition is provided.")] 
        [SerializeField] private ProjectileDefinition defaultClass;
        [Tooltip("Draws travel and probe gizmos for tuning.")]
        [SerializeField] private bool drawTravelGizmos = true;
        [Tooltip("Color used for travel gizmos while the projectile is active.")] 
        [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0.1f, 0.45f);
        #endregion

        #region Runtime
        public Action<PooledProjectile> Despawn { get; set; }

        private ProjectileDefinition activeDefinition;
        private ProjectileSpawnContext lastContext;
        private float scheduledDespawnSeconds;
        private int remainingPierces;
        private Vector3 travelDirection;
        private float travelSpeed;
        private float traveledDistance;
        private float lifetimeTimer;
        private Transform sourceTransform;
        private int sourceLayer;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[8];
        private readonly Collider[] damageBuffer = new Collider[12];
        private int appliedHits;
        private float activeSplashRadius;
        #endregion

        #region IDamage
        public float DamageAmount { get { return activeDefinition != null ? activeDefinition.Damage : 0f; } }
        public float CriticalChance { get { return activeDefinition != null ? activeDefinition.CriticalChance : 0f; } }
        public float CriticalMultiplier { get { return activeDefinition != null ? activeDefinition.CriticalMultiplier : 1f; } }
        public GameObject Source { get { return sourceTransform != null ? sourceTransform.gameObject : null; } }
        #endregion
        #endregion

        #region Methods
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
            ProjectileSpawnContext context = new ProjectileSpawnContext(defaultClass, transform.position, transform.forward, 1f, null, source: null, sourceLayer: gameObject.layer, overrideSplashRadius: 0f);
            PooledProjectile spawned = OnSpawn(context);
            return spawned;
        }

        /// <summary>
        /// Applies spawn context, configures travel parameters, and arms the despawn timer.
        /// </summary>
        public PooledProjectile OnSpawn(ProjectileSpawnContext context)
        {
            lastContext = context;
            ApplyDefinition(context.Definition != null ? context.Definition : defaultClass);
            if (!HasDefinition)
            {
                Debug.LogWarning("Projectile spawn aborted: missing definition.", this);
                return this;
            }

            ApplyTransform(context);
            ConfigureTravel(context);
            ConfigureImpactProfile(context);
            ScheduleAutoDespawn();
            appliedHits = 0;
            return this;
        }

        /// <summary>
        /// Resets transient runtime state.
        /// </summary>
        public void ResetPoolable()
        {
            scheduledDespawnSeconds = 0f;
            remainingPierces = 0;
            travelDirection = Vector3.forward;
            travelSpeed = 0f;
            traveledDistance = 0f;
            lifetimeTimer = 0f;
            lastContext = new ProjectileSpawnContext(defaultClass, Vector3.zero, Vector3.forward, 1f, null, null, 0, 0f);
            activeDefinition = defaultClass;
            sourceTransform = null;
            sourceLayer = 0;
            appliedHits = 0;
            activeSplashRadius = 0f;
        }
        #endregion

        #region Unity
        /// <summary>
        /// Advances the projectile and checks for collisions without relying on physics simulation.
        /// </summary>
        private void Update()
        {
            if (!HasDefinition)
                return;

            float deltaTime = Time.deltaTime;
            float stepDistance = travelSpeed * deltaTime;
            if (stepDistance <= 0f)
                return;

            ProcessStep(stepDistance);

            traveledDistance += stepDistance;
            lifetimeTimer += deltaTime;
            if (ShouldDespawn())
                OnDespawn();
        }
#if UNITY_EDITOR
        /// <summary>
        /// Draws distance and probe previews for debug purposes.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if(defaultClass != null && defaultClass.SplashRadius > 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, defaultClass.SplashRadius);
            }
            if (!drawTravelGizmos)
                return;

            Gizmos.color = gizmoColor;
            float fallbackSpeed = activeDefinition != null ? activeDefinition.Speed : 0f;
            float projectedDistance = activeDefinition != null && activeDefinition.MaxDistance > 0f ? activeDefinition.MaxDistance : fallbackSpeed * (activeDefinition != null ? activeDefinition.LifetimeSeconds : 0f);
            Gizmos.DrawLine(transform.position, transform.position + travelDirection * projectedDistance);
            float probeRadius = activeDefinition != null ? Mathf.Max(0.01f, activeDefinition.DamageProbeRadius) : 0.1f;
            Gizmos.DrawWireSphere(transform.position, probeRadius);
            if (activeSplashRadius > 0f)
                Gizmos.DrawWireSphere(transform.position, activeSplashRadius);
        }
#endif
#endregion

        #region Public 
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

        /// <summary>
        /// Assigns a default definition used when spawning without explicit context.
        /// </summary>
        public void AssignDefaultDefinition(ProjectileDefinition definition)
        {
            defaultClass = definition;
            activeDefinition = defaultClass;
        }
        #endregion

        #region Internal
        /// <summary>
        /// Processes a single movement step, resolving hits and applying pierce budgets.
        /// </summary>
        private void ProcessStep(float stepDistance)
        {
            float remainingStep = stepDistance;
            int safety = 0;
            while (remainingStep > 0.001f && safety < 4)
            {
                float consumed;
                bool hit = TryResolveHit(remainingStep, out consumed);
                remainingStep -= consumed;
                if (!hit)
                {
                    transform.position += travelDirection * consumed;
                }
                else
                {
                    if (remainingPierces <= 0)
                        return;
                }

                safety++;
            }
        }

        /// <summary>
        /// Performs a sphere cast to find the closest valid hit during this frame.
        /// </summary>
        private bool TryResolveHit(float stepDistance, out float consumedDistance)
        {
            consumedDistance = stepDistance;
            float probeRadius = Mathf.Max(0.01f, activeDefinition.DamageProbeRadius);
            int hits = Physics.SphereCastNonAlloc(transform.position, probeRadius, travelDirection, hitBuffer, stepDistance, ~0, QueryTriggerInteraction.Collide);
            if (hits == 0)
                return false;

            float nearestDistance = float.MaxValue;
            RaycastHit nearestHit = default;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit candidate = hitBuffer[i];
                if (candidate.collider == null || !candidate.collider.gameObject.activeInHierarchy)
                    continue;

                if (IsFriendlyCollider(candidate.collider))
                    continue;

                if (candidate.distance < nearestDistance)
                {
                    nearestDistance = candidate.distance;
                    nearestHit = candidate;
                }
            }

            if (nearestDistance > stepDistance || nearestDistance == float.MaxValue)
                return false;

            consumedDistance = nearestDistance;
            Vector3 impactPoint = transform.position + travelDirection * nearestDistance;
            transform.position = impactPoint;
            ApplyDamageAtPoint(nearestHit.collider, impactPoint);
            remainingPierces--;
            if (remainingPierces <= 0)
                OnDespawn();

            return true;
        }

        /// <summary>
        /// Applies damage to all eligible colliders inside the configured damage area.
        /// </summary>
        private void ApplyDamageAtPoint(Collider primaryCollider, Vector3 impactPoint)
        {
            float areaRadius = Mathf.Max(activeDefinition.DamageProbeRadius, activeSplashRadius);
            ApplyDamageToCollider(primaryCollider, impactPoint);
            int found = Physics.OverlapSphereNonAlloc(impactPoint, areaRadius, damageBuffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                Collider candidate = damageBuffer[i];
                if (candidate == null || candidate == primaryCollider || !candidate.gameObject.activeInHierarchy)
                    continue;

                ApplyDamageToCollider(candidate, impactPoint);
            }
        }

        /// <summary>
        /// Attempts to deliver damage to a single collider target.
        /// </summary>
        private bool ApplyDamageToCollider(Collider targetCollider, Vector3 impactPoint)
        {
            if (targetCollider == null || IsFriendlyCollider(targetCollider))
                return false;

            IDamagable damagable = targetCollider.GetComponentInParent<IDamagable>();
            if (damagable == null)
                return false;

            float resolvedDamage = ResolveDamage();
            AppliedDamagePayload payload = new AppliedDamagePayload(resolvedDamage, this);
            damagable.ApplyDamage(payload, impactPoint);
            appliedHits++;
            return true;
        }

        /// <summary>
        /// Checks whether the provided collider belongs to the source or matches the source layer.
        /// </summary>
        private bool IsFriendlyCollider(Collider candidate)
        {
            if (candidate == null)
                return true;

            if (candidate.gameObject.layer == sourceLayer)
                return true;

            if (sourceTransform == null)
                return false;

            Transform candidateRoot = candidate.transform.root;
            return candidateRoot == sourceTransform.root;
        }

        /// <summary>
        /// Calculates damage including criticals and pierce falloff.
        /// </summary>
        private float ResolveDamage()
        {
            float damage = activeDefinition.Damage;
            float falloff = Mathf.Pow(1f - Mathf.Clamp01(activeDefinition.PierceFalloffRatio), appliedHits);
            damage *= falloff;

            float critRoll = UnityEngine.Random.value;
            if (critRoll <= activeDefinition.CriticalChance)
                damage *= activeDefinition.CriticalMultiplier;

            return damage;
        }

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
            transform.SetParent(null, false);
        }

        /// <summary>
        /// Configures travel speed, direction, pierce budget, and source filtering.
        /// </summary>
        private void ConfigureTravel(ProjectileSpawnContext context)
        {
            travelDirection = context.Direction.sqrMagnitude > 0f ? context.Direction.normalized : Vector3.forward;
            travelSpeed = Mathf.Max(0f, activeDefinition.Speed * context.SpeedMultiplier);
            remainingPierces = Mathf.Max(1, activeDefinition.MaxPiercedTargets);
            traveledDistance = 0f;
            lifetimeTimer = 0f;
            sourceTransform = context.Source;
            sourceLayer = context.SourceLayer;
        }

        /// <summary>
        /// Configures impact splash radius using definition or spawn override.
        /// </summary>
        private void ConfigureImpactProfile(ProjectileSpawnContext context)
        {
            float overrideRadius = context.OverrideSplashRadius;
            activeSplashRadius = overrideRadius > 0f ? overrideRadius : activeDefinition.SplashRadius;
        }

        /// <summary>
        /// Schedules automatic despawn based on lifetime or distance budget.
        /// </summary>
        private void ScheduleAutoDespawn()
        {
            if (!HasDefinition)
                return;

            float speed = Mathf.Max(0.01f, activeDefinition.Speed);
            float travelSeconds = activeDefinition.MaxDistance > 0f ? activeDefinition.MaxDistance / speed : activeDefinition.LifetimeSeconds;
            scheduledDespawnSeconds = Mathf.Min(travelSeconds, activeDefinition.LifetimeSeconds);
        }

        /// <summary>
        /// Checks whether the projectile exceeded lifetime or distance constraints.
        /// </summary>
        private bool ShouldDespawn()
        {
            if (!HasDefinition)
                return true;

            if (scheduledDespawnSeconds > 0f && lifetimeTimer >= scheduledDespawnSeconds)
                return true;

            if (activeDefinition.MaxDistance > 0f && traveledDistance >= activeDefinition.MaxDistance)
                return true;

            return false;
        }
        #endregion
        #endregion

        #region Nested Types
        /// <summary>
        /// Simple IDamage wrapper used to relay resolved values per hit.
        /// </summary>
        private readonly struct AppliedDamagePayload : IDamage
        {
            public float DamageAmount { get; }
            public float CriticalChance { get; }
            public float CriticalMultiplier { get; }
            public GameObject Source { get; }

            public AppliedDamagePayload(float damageAmount, IDamage source)
            {
                DamageAmount = damageAmount;
                CriticalChance = source.CriticalChance;
                CriticalMultiplier = source.CriticalMultiplier;
                Source = source.Source;
            }
        }
        #endregion
    }
}
