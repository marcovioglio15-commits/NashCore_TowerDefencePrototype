using System.Collections.Generic;
using Grid;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Handles validation and spawning of player-placed turrets against Grid3D buildable cells.
    /// </summary>
    public class TurretPlacementService : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]

        [SerializeField]
        [Tooltip("Grid instance used to resolve buildable coordinates and world positions.")]
        private Grid3D grid;

        [SerializeField]
        [Tooltip("Fallback pool used when a turret definition is missing its own pool reference.")]
        private TurretPoolSO fallbackTurretPool;

        [SerializeField]
        [Tooltip("Parent transform for spawned turrets to keep hierarchy tidy.")]
        private Transform turretRoot;

        [Header("Debug")]

        [SerializeField]
        [Tooltip("Draws placement gizmos at the last checked coordinate.")]
        private bool drawPlacementGizmos = true;

        #endregion

        #region Runtime

        private readonly Dictionary<Vector2Int, PooledTurret> liveTurrets = new Dictionary<Vector2Int, PooledTurret>();
        private Vector2Int lastPreviewCell;
        private TurretClassDefinition lastPreviewDefinition;
        private bool hasPreview;

        #endregion

        #region Public API

        /// <summary>
        /// Verifies whether the turret can be placed on the requested grid cell.
        /// </summary>
        public bool CanPlace(TurretClassDefinition definition, Vector2Int cell, out Vector3 worldPosition, out string failureReason)
        {
            worldPosition = Vector3.zero;
            failureReason = string.Empty;
            hasPreview = true;
            lastPreviewCell = cell;
            lastPreviewDefinition = definition;

            if (definition == null)
            {
                failureReason = "Missing turret definition";
                return false;
            }

            if (grid == null)
            {
                failureReason = "Missing grid reference";
                return false;
            }

            if (!grid.IsWithinBounds(cell))
            {
                failureReason = "Outside grid bounds";
                return false;
            }

            if (!grid.IsBuildable(cell))
            {
                failureReason = "Cell not buildable";
                return false;
            }

            if (liveTurrets.ContainsKey(cell))
            {
                failureReason = "Cell already occupied";
                return false;
            }

            worldPosition = grid.GridToWorld(cell) + Vector3.up * definition.Placement.HeightOffset;
            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Spawns a turret on the requested cell if validation succeeds.
        /// </summary>
        public PooledTurret PlaceTurret(TurretClassDefinition definition, Vector2Int cell, Quaternion rotation)
        {
            Vector3 position;
            string reason;
            if (!CanPlace(definition, cell, out position, out reason))
                return null;

            TurretPoolSO pool = definition.TurretPool != null ? definition.TurretPool : fallbackTurretPool;
            if (pool == null)
                return null;

            TurretSpawnContext context = new TurretSpawnContext(definition, position, rotation, cell, turretRoot);
            PooledTurret turret = pool.Spawn(definition, context);
            if (turret == null)
                return null;

            grid.SetTowerState(cell, true);
            liveTurrets[cell] = turret;
            return turret;
        }

        /// <summary>
        /// Despawns a turret placed on the given cell and frees the grid node.
        /// </summary>
        public bool RemoveTurret(Vector2Int cell)
        {
            if (!liveTurrets.ContainsKey(cell))
                return false;

            PooledTurret turret = liveTurrets[cell];
            liveTurrets.Remove(cell);

            if (grid != null)
                grid.SetTowerState(cell, false);

            if (turret != null)
                turret.RequestDespawn();

            return true;
        }

        /// <summary>
        /// Checks whether a turret is currently tracked on the provided grid cell.
        /// </summary>
        public bool TryGetTurret(Vector2Int cell, out PooledTurret turret)
        {
            return liveTurrets.TryGetValue(cell, out turret);
        }

        #endregion

        #region Gizmos

        /// <summary>
        /// Draws placement and range previews for the last validated turret.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawPlacementGizmos)
                return;

            if (!hasPreview || lastPreviewDefinition == null || grid == null)
                return;

            Vector3 position = grid.GridToWorld(lastPreviewCell) + Vector3.up * lastPreviewDefinition.Placement.HeightOffset;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireSphere(position, lastPreviewDefinition.Placement.FootprintRadius);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(position, lastPreviewDefinition.Targeting.Range);
        }

        #endregion
    }
}
