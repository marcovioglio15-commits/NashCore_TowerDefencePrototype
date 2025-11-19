using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Pool for projectile instances, binding despawn hooks and providing warmup utilities.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectilePool", menuName = "Scriptables/Turrets/Projectile Pool")]
    public class ProjectilePoolSO : APooInterface<PooledProjectile, ProjectileSpawnContext>
    {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("Default definition used when spawn contexts omit a definition reference.")]
        private ProjectileDefinition fallbackDefinition;

        [SerializeField]
        [Tooltip("Number of projectiles to pre-instantiate on initialize to limit spikes.")]
        private int warmupCount = 16;

        #endregion

        #region Public API

        /// <summary>
        /// Preloads the pool to the configured warmupCount.
        /// </summary>
        public void Warmup()
        {
            Initialize(warmupCount);
        }

        /// <summary>
        /// Spawns a projectile using the provided context and optional definition override.
        /// </summary>
        public PooledProjectile Spawn(ProjectileDefinition definition, ProjectileSpawnContext context)
        {
            ProjectileSpawnContext resolved = context.WithDefinition(definition != null ? definition : fallbackDefinition);
            PooledProjectile projectile = Spawn(resolved);
            return projectile;
        }

        /// <summary>
        /// Updates the fallback definition that will be injected during reset.
        /// </summary>
        public void SetFallbackDefinition(ProjectileDefinition definition)
        {
            fallbackDefinition = definition;
        }

        #endregion

        #region Overrides

        public override void BindPoolable(PooledProjectile poolable)
        {
            if (poolable == null)
                return;

            poolable.Despawn += Despawn;
        }

        public override void ResetPoolable(PooledProjectile poolable)
        {
            if (poolable == null)
                return;

            poolable.AssignDefaultDefinition(fallbackDefinition);
            poolable.ResetPoolable();
        }

        #endregion
    }
}
