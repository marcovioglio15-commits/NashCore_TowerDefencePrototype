using Scriptables.Turrets;
using UnityEngine;

namespace Player.Inventory
{
    /// <summary>
    /// Data object describing a placement preview result broadcast to the UI.
    /// </summary>
    public readonly struct BuildPreviewData
    {
        #region Variables And Properties
        #region Serialized-Like Fields
        public TurretClassDefinition Definition { get; }
        public Vector3 WorldPosition { get; }
        public Vector2Int Cell { get; }
        public bool HasValidCell { get; }
        #endregion
        #endregion

        #region Methods
        /// <summary>
        /// Builds a new immutable preview description.
        /// </summary>
        public BuildPreviewData(TurretClassDefinition definition, Vector3 worldPosition, Vector2Int cell, bool hasValidCell)
        {
            Definition = definition;
            WorldPosition = worldPosition;
            Cell = cell;
            HasValidCell = hasValidCell;
        }
        #endregion
    }

    /// <summary>
    /// Result payload emitted whenever a placement attempt finishes.
    /// </summary>
    public readonly struct BuildPlacementResult
    {
        #region Variables And Properties
        #region Serialized-Like Fields
        public TurretClassDefinition Definition { get; }
        public bool Success { get; }
        public string FailureReason { get; }
        public Vector3 WorldPosition { get; }
        public Vector2Int Cell { get; }
        #endregion
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new immutable placement result.
        /// </summary>
        public BuildPlacementResult(TurretClassDefinition definition, bool success, string failureReason, Vector3 worldPosition, Vector2Int cell)
        {
            Definition = definition;
            Success = success;
            FailureReason = failureReason;
            WorldPosition = worldPosition;
            Cell = cell;
        }
        #endregion
    }
}
[System.Serializable]
public struct DijkstraInfo
{
    public int[] distances;
    //public Dictionary<int, int> pathIndexesDict;
    public int[] pathIndexes;
    public DijkstraInfo(in int[] distances, /*in Dictionary<int, int> pathIndexesDict,*/ in int[] pathIndexes)
    {
        this.distances = distances;
        //this.pathIndexesDict = pathIndexesDict;
        this.pathIndexes = pathIndexes;
    }
}