using System.Collections.Generic;
using Grid;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Handles validation and spawning of player-placed turrets against Grid3D buildable cells.
    /// </summary>
    public class TurretPlacementLogic : MonoBehaviour
    {
        #region Variables And Properties
        #region Serialized Fields
        [Header("References")]
        [Tooltip("Grid instance used to resolve buildable coordinates and world positions.")]
        [SerializeField] private Grid3D grid;
        [Tooltip("Fallback pool used when a turret definition is missing its own pool reference.")]
        [SerializeField] private TurretPoolSO fallbackTurretPool;
        [Tooltip("Parent transform for spawned turrets to keep hierarchy tidy.")]
        [SerializeField]private Transform turretRoot;
        [Tooltip("Free-aim controller used to register auxiliary renderers per placed turret.")]
        [SerializeField] private Player.Build.TurretFreeAimController freeAimController;

        [Header("Debug")]
        [Tooltip("Draws placement gizmos at the last checked coordinate.")]
        [SerializeField]
        private bool drawPlacementGizmos = true;
        #endregion

        #region State
        private readonly Dictionary<Vector2Int, PooledTurret> liveTurrets = new Dictionary<Vector2Int, PooledTurret>();
        private Vector2Int lastPreviewCell;
        private TurretClassDefinition lastPreviewDefinition;
        private Quaternion lastPreviewRotation = Quaternion.identity;
        private bool hasPreview;
        #endregion

        #region Properties
        /// <summary>
        /// Exposes the grid reference for placement helpers.
        /// </summary>
        public Grid3D Grid
        {
            get { return grid; }
        }
        #endregion
        #endregion

        #region Public
        /// <summary>
        /// Verifies whether the turret can be placed on the requested grid cell.
        /// </summary>
        public bool CanPlace(TurretClassDefinition definition, Vector2Int cell, Quaternion rotation, out Vector3 worldPosition, out string failureReason)
        {
            worldPosition = Vector3.zero;
            failureReason = string.Empty;
            hasPreview = true;
            lastPreviewCell = cell;
            lastPreviewDefinition = definition;
            lastPreviewRotation = rotation;

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

            Vector3 offset = rotation * definition.Placement.SpawnOffset;
            worldPosition = grid.GridToWorld(cell) + Vector3.up * definition.Placement.HeightOffset + offset;
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
            if (!CanPlace(definition, cell, rotation, out position, out reason))
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
            RegisterCellAuxiliaryWalls(cell, turret);
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

            UnregisterCellAuxiliaryWalls(cell, turret);
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

        #region Helpers
        /// <summary>
        /// Registers auxiliary renderers tied to the placed turret for the target cell.
        /// </summary>
        private void RegisterCellAuxiliaryWalls(Vector2Int cell, PooledTurret turret)
        {
            if (freeAimController == null || grid == null || turret == null)
                return;

            Renderer[] renderers = grid.GetBuildableWallRenderers(cell);
            if (renderers == null || renderers.Length == 0)
            {
                freeAimController.UnregisterAuxiliaryRenderers(turret);
                return;
            }

            freeAimController.RegisterAuxiliaryRenderers(turret, renderers);
        }

        /// <summary>
        /// Clears auxiliary renderer bindings when a turret is removed from a cell.
        /// </summary>
        private void UnregisterCellAuxiliaryWalls(Vector2Int cell, PooledTurret turret)
        {
            if (freeAimController == null || turret == null)
                return;

            freeAimController.UnregisterAuxiliaryRenderers(turret);
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

            Vector3 position = grid.GridToWorld(lastPreviewCell) + Vector3.up * lastPreviewDefinition.Placement.HeightOffset + lastPreviewRotation * lastPreviewDefinition.Placement.SpawnOffset;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireSphere(position, lastPreviewDefinition.Placement.FootprintRadius);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(position, lastPreviewDefinition.Targeting.Range);
        }
        #endregion
    }
}
