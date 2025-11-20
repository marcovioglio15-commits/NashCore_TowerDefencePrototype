using System.Collections.Generic;
using UnityEngine;

namespace Grid
{
    [System.Serializable]
    /// <summary>
    /// Represents a single logical cell of the gameplay grid using a compact bitmask state.
    /// </summary>
    public class GridNode
    {
        #region Properties

        // Flattened index inside grid array.
        public int Index { get; private set; }

        // Grid coordinate X.
        public int X { get; private set; }
        private readonly List<GridEdge> edges;

        // Grid coordinate Z.
        public int Z { get; private set; }

        // World position of the node.
        public Vector3 WorldPosition { get; private set; }

        // Current bitmask state.
        public NodeState State { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new grid node with provided indices, world position and initial state flags.
        /// </summary>
        public GridNode(int index, int x, int z, Vector3 worldPosition, NodeState initialState)
        {
            Index = index;
            X = x;
            Z = z;
            WorldPosition = worldPosition;
            State = initialState;
            edges = new List<GridEdge>();
        }

        #endregion

        public void AddEdge(in GridNode toAttach, int weight = 1)
        {
            if (toAttach == null)
            {
                Debug.LogError("Given node cannot be null.");
                return;
            }

            for (int i = 0; i < edges.Count; i++)
                if (edges[i].GetOppositeNode(this) == toAttach)
                {
                    Debug.LogError("Edge already exist!");
                    return;
                }

            GridEdge edgeToAdd = new GridEdge(this, toAttach, weight);
            edges.Add(edgeToAdd);
            toAttach.edges.Add(edgeToAdd);
        }

        public void RemoveEdge(in GridNode toDetach)
        {
            for (int i = 0; i < edges.Count; i++)
                if (edges[i].GetOppositeNode(this) == toDetach)
                {
                    edges.Remove(edges[i]);
                    toDetach.edges.Remove(edges[i]);
                    return;
                }

            Debug.LogError("Edge already exist!");
        }
        public List<GridEdge> GetEdges()
        {
            return edges;
        }
        #region State Helpers

        /// <summary>
        /// Returns true if the node has the given flag.
        /// </summary>
        public bool Is(NodeState flag)
        {
            return (State & flag) != 0;
        }

        /// <summary>
        /// Assigns or removes a specific state flag.
        /// </summary>
        public void SetState(NodeState flag, bool value)
        {
            if (value)
                State |= flag;
            else
                State &= ~flag;
        }

        /// <summary>
        /// Updates the node world position.
        /// </summary>
        public void SetWorldPosition(Vector3 position)
        {
            WorldPosition = position;
        }

        #endregion
    }
}
