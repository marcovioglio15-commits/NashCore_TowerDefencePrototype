using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Pool for turret instances, providing warmup utilities and default definition binding.
    /// </summary>
    [CreateAssetMenu(fileName = "TurretPool", menuName = "Scriptables/Turrets/Turret Pool")]
    public class TurretPoolSO : APooInterface<PooledTurret, TurretSpawnContext>
    {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("Default definition used when spawn contexts omit a definition reference.")]
        private TurretClassDefinition fallbackDefinition;

        [SerializeField]
        [Tooltip("Instances pre-created on Initialize to avoid runtime allocations.")]
        private int warmupCount = 4;

        #endregion

        #region Public API

        /// <summary>
        /// Prepares the pool by instantiating a configurable number of turrets.
        /// </summary>
        public void Warmup()
        {
            Initialize(warmupCount);
        }

        /// <summary>
        /// Spawns a turret using the provided context and optional overriding definition.
        /// </summary>
        public PooledTurret Spawn(TurretClassDefinition definition, TurretSpawnContext context)
        {
            TurretSpawnContext resolved = context.WithDefinition(definition != null ? definition : fallbackDefinition);
            PooledTurret turret = Spawn(resolved);
            return turret;
        }

        /// <summary>
        /// Updates the fallback definition that will be injected during reset.
        /// </summary>
        public void SetFallbackDefinition(TurretClassDefinition definition)
        {
            fallbackDefinition = definition;
        }

        #endregion

        #region Overrides

        public override void BindPoolable(PooledTurret poolable)
        {
            if (poolable == null)
                return;

            poolable.Despawn += Despawn;
        }

        public override void ResetPoolable(PooledTurret poolable)
        {
            if (poolable == null)
                return;

            poolable.AssignDefaultDefinition(fallbackDefinition);
            poolable.ResetPoolable();
        }

        #endregion
    }
}
