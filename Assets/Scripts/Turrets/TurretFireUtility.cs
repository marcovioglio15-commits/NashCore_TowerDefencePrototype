using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Static helper for resolving projectile directions and spawns shared between auto and manual fire.
    /// </summary>
    public static class TurretFireUtility
    {
        #region Methods
        #region Projectile Direction
        /// <summary>
        /// Computes the projectile direction for the provided index respecting the requested pattern.
        /// </summary>
        public static Vector3 ResolveProjectileDirection(Vector3 forward, TurretFirePattern pattern, float coneAngleDegrees, int index, int total, Vector3? upAxis = null)
        {
            if (total <= 1 || pattern != TurretFirePattern.Cone)
                return forward;

            if (coneAngleDegrees <= 0f)
                return forward;

            Vector3 rotationAxis = upAxis.HasValue ? upAxis.Value : Vector3.up;
            if (rotationAxis.sqrMagnitude <= Mathf.Epsilon)
                rotationAxis = Vector3.up;

            float step = coneAngleDegrees / (total - 1);
            float startAngle = -coneAngleDegrees * 0.5f;
            float angle = startAngle + step * index;
            Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis.normalized);
            Vector3 adjusted = rotation * forward;
            return adjusted.normalized;
        }
        #endregion

        #region Spawning
        /// <summary>
        /// Spawns a projectile aligned with the provided direction using the turret projectile pool.
        /// </summary>
        public static void SpawnProjectile(PooledTurret turret, Vector3 direction, Transform originOverride = null, Vector3? localOffset = null)
        {
            if (turret == null || !turret.HasDefinition)
                return;

            TurretClassDefinition definition = turret.Definition;
            ProjectileDefinition projectileDefinition = definition.Projectile;
            ProjectilePoolSO pool = definition.ProjectilePool != null ? definition.ProjectilePool : projectileDefinition != null ? projectileDefinition.Pool : null;
            if (pool == null || projectileDefinition == null)
                return;

            Transform origin = originOverride != null ? originOverride : turret.Muzzle != null ? turret.Muzzle : turret.transform;
            Vector3 spawnOffset = localOffset.HasValue ? localOffset.Value : Vector3.zero;
            Vector3 position = origin.position + origin.TransformVector(spawnOffset);
            ProjectileSpawnContext context = new ProjectileSpawnContext(projectileDefinition, position, direction, 1f, turret.transform, origin, origin.gameObject.layer);
            pool.Spawn(projectileDefinition, context);
        }
        #endregion
        #endregion
    }
}
