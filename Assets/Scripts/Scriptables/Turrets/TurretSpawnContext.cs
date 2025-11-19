using System;
using UnityEngine;

namespace Scriptables.Turrets
{
    /// <summary>
    /// Captures spawn parameters for a pooled turret instance.
    /// </summary>
    [Serializable]
    public struct TurretSpawnContext
    {
        #region Variables And Properties
        #region Serialized Fields

        [SerializeField]
        [Tooltip("Definition used by the spawned turret. When null, the pool default is applied.")]
        private TurretClassDefinition definition;

        [SerializeField]
        [Tooltip("Placement world position.")]
        private Vector3 position;

        [SerializeField]
        [Tooltip("Placement rotation.")]
        private Quaternion rotation;

        [SerializeField]
        [Tooltip("Optional parent transform for hierarchy organization.")]
        private Transform parent;

        [SerializeField]
        [Tooltip("Grid coordinate bound to this turret when placed on Grid3D.")]
        private Vector2Int gridCoordinate;

        [SerializeField]
        [Tooltip("If true, gridCoordinate is considered valid.")]
        private bool hasGridCoordinate;

        #endregion

        #region Properties

        public TurretClassDefinition Definition
        {
            get { return definition; }
        }

        public Vector3 Position
        {
            get { return position; }
        }

        public Quaternion Rotation
        {
            get { return rotation; }
        }

        public Transform Parent
        {
            get { return parent; }
        }

        public bool HasGridCoordinate
        {
            get { return hasGridCoordinate; }
        }

        public Vector2Int GridCoordinate
        {
            get { return gridCoordinate; }
        }

        #endregion
        #endregion

        #region Methods
        #region Constructors

        public TurretSpawnContext(TurretClassDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            this.definition = definition;
            this.position = position;
            this.rotation = rotation;
            this.parent = parent;
            gridCoordinate = Vector2Int.zero;
            hasGridCoordinate = false;
        }

        public TurretSpawnContext(TurretClassDefinition definition, Vector3 position, Quaternion rotation, Vector2Int gridCoordinate, Transform parent = null)
        {
            this.definition = definition;
            this.position = position;
            this.rotation = rotation;
            this.parent = parent;
            this.gridCoordinate = gridCoordinate;
            hasGridCoordinate = true;
        }

        #endregion

        #region Helpers

        public TurretSpawnContext WithDefinition(TurretClassDefinition newDefinition)
        {
            TurretSpawnContext updated = this;
            updated.definition = newDefinition;
            return updated;
        }

        public TurretSpawnContext WithParent(Transform newParent)
        {
            TurretSpawnContext updated = this;
            updated.parent = newParent;
            return updated;
        }

        public TurretSpawnContext WithGridCoordinate(Vector2Int coordinate)
        {
            TurretSpawnContext updated = this;
            updated.gridCoordinate = coordinate;
            updated.hasGridCoordinate = true;
            return updated;
        }

        #endregion
        #endregion
    }
}
