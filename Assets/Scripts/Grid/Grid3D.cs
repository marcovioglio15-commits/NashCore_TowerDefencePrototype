using System.Collections.Generic;
using UnityEngine;

namespace Grid
{
    [DefaultExecutionOrder(-200)]
    /// <summary>
    /// Manages a 3D gameplay grid with offset-based origin, bitmask node states and layered debug gizmos.
    /// </summary>
    public class Grid3D : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("Number of cells along the X axis of the grid.")]
        private int gridSizeX = 10;

        [SerializeField]
        [Tooltip("Number of cells along the Z axis of the grid.")]
        private int gridSizeZ = 10;

        [SerializeField]
        [Tooltip("Offset relative to transform.position used as grid origin. Y defines grid height.")]
        private Vector3 originOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Size of each cell in world units.")]
        private float cellSize = 1.0f;

        [Header("Static Map Configuration")]

        [SerializeField]
        [Tooltip("Grid coordinates that are walkable.")]
        private Vector2Int[] walkableNodes;

        [SerializeField]
        [Tooltip("Grid coordinates where towers can be built.")]
        private Vector2Int[] buildableNodes;

        [SerializeField]
        [Tooltip("Grid coordinates used as enemy goal nodes.")]
        private Vector2Int[] enemyGoalCells;

        [Header("Enemy Spawn Points")]

        [SerializeField]
        [Tooltip("Grid coordinates used as enemy spawn points.")]
        private Vector2Int[] enemySpawnCells;

        [Header("Gizmos")]

        [SerializeField]
        [Tooltip("Draws grid gizmos when the object is selected.")]
        private bool drawGridGizmos = true;

        [SerializeField]
        [Tooltip("Displays (x,z) text above each node.")]
        private bool drawNodeCoordinates = true;

        [SerializeField]
        [Tooltip("Color used for walkable cells.")]
        private Color walkableColor = new Color(0.0f, 1.0f, 0.0f, 0.35f);

        [SerializeField]
        [Tooltip("Color used for buildable cells.")]
        private Color buildableColor = new Color(0.0f, 0.5f, 1.0f, 0.35f);

        [SerializeField]
        [Tooltip("Color used for enemy goal cells.")]
        private Color goalColor = new Color(1.0f, 1.0f, 0.0f, 0.55f);

        [SerializeField]
        [Tooltip("Color used for enemy spawn nodes.")]
        private Color spawnColor = new Color(1.0f, 0.0f, 0.0f, 0.55f);

        [SerializeField]
        [Tooltip("Color used for disabled nodes.")]
        private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);

        [SerializeField]
        [Tooltip("Color used for wireframe lines.")]
        private Color wireColor = new Color(1.0f, 1.0f, 1.0f, 0.15f);

        #endregion

        #region Runtime

        // Flat array of nodes.
        [SerializeField] private GridNode[] graph;

        #endregion

        #region Properties

        // Grid origin in world space.
        public Vector3 Origin
        {
            get { return transform.position + originOffset; }
        }

        public int GridSizeX { get { return gridSizeX; } }

        public int GridSizeZ { get { return gridSizeZ; } }

        public float CellSize { get { return cellSize; } }

        #endregion

        #region Unity

        /// <summary>
        /// Ensures grid is initialized before gameplay.
        /// </summary>
        private void Awake()
        {
            InitializeGrid();
        }

        /// <summary>
        /// Rebuilds grid in editor when parameters change.
        /// </summary>
        private void OnValidate()
        {
            if (gridSizeX < 1)
                gridSizeX = 1;

            if (gridSizeZ < 1)
                gridSizeZ = 1;

            if (cellSize < 0.01f)
                cellSize = 0.01f;

            ClampArrayCoords(walkableNodes);
            ClampArrayCoords(buildableNodes);
            ClampArrayCoords(enemyGoalCells);
            ClampArrayCoords(enemySpawnCells);

            InitializeGrid();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Builds or rebuilds the grid, applying static map configuration.
        /// </summary>
        private void InitializeGrid()
        {
            int total = gridSizeX * gridSizeZ;

            if (graph == null || graph.Length != total)
                graph = new GridNode[total];

            for (int z = 0; z < gridSizeZ; z++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    int index = ToIndex(x, z);
                    Vector3 pos = GridToWorld(x, z);

                    NodeState staticState = 0;

                    if (Contains(walkableNodes, x, z))
                        staticState |= NodeState.Walkable;

                    if (Contains(buildableNodes, x, z))
                        staticState |= NodeState.Buildable;

                    if (Contains(enemyGoalCells, x, z))
                        staticState |= NodeState.IsEnemyGoal;

                    if (graph[index] == null)
                        graph[index] = new GridNode(index, x, z, pos, staticState);
                    else
                        graph[index].SetWorldPosition(pos);

                    graph[index].SetState(NodeState.Walkable, (staticState & NodeState.Walkable) != 0);
                    graph[index].SetState(NodeState.Buildable, (staticState & NodeState.Buildable) != 0);
                    graph[index].SetState(NodeState.IsEnemyGoal, (staticState & NodeState.IsEnemyGoal) != 0);

                    int weight = 1;
                    if (!graph[index].Is(NodeState.Walkable))
                        weight = int.MaxValue;

                    if (x > 0)
                        graph[index].AddEdge(graph[ToIndex(x - 1, z)], weight);

                    if (z > 0)
                        graph[index].AddEdge(graph[ToIndex(x, z - 1)], weight);
                }
            }
        }

        #endregion

        #region Helpers

        private void ClampArrayCoords(Vector2Int[] arr)
        {
            if (arr == null)
                return;

            int count = arr.Length;

            for (int i = 0; i < count; i++)
            {
                Vector2Int c = arr[i];

                if (c.x < 0)
                    c.x = 0;

                if (c.y < 0)
                    c.y = 0;

                if (c.x >= gridSizeX)
                    c.x = gridSizeX - 1;

                if (c.y >= gridSizeZ)
                    c.y = gridSizeZ - 1;

                arr[i] = c;
            }
        }

        private bool Contains(Vector2Int[] arr, int x, int z)
        {
            if (arr == null)
                return false;

            int count = arr.Length;

            for (int i = 0; i < count; i++)
            {
                Vector2Int c = arr[i];

                if (c.x == x && c.y == z)
                    return true;
            }

            return false;
        }

        public int ToIndex(int x, int z)
        {
            return x + z * gridSizeX;
        }

        /// <summary>
        /// Returns world coordinates for the provided grid coordinate.
        /// </summary>
        public Vector3 GridToWorld(Vector2Int coords)
        {
            return GridToWorld(coords.x, coords.y);
        }

        /// <summary>
        /// Safe accessor that returns the node at coordinates when available.
        /// </summary>
        public bool TryGetNode(Vector2Int coords, out GridNode node)
        {
            node = null;
            InitializeGrid();

            if (graph == null)
                return false;

            if (coords.x < 0 || coords.y < 0)
                return false;

            if (coords.x >= gridSizeX || coords.y >= gridSizeZ)
                return false;

            int index = ToIndex(coords.x, coords.y);
            node = graph[index];
            return node != null;
        }

        /// <summary>
        /// Returns true when grid bounds contain the coordinate.
        /// </summary>
        public bool IsWithinBounds(Vector2Int coords)
        {
            return coords.x >= 0 && coords.y >= 0 && coords.x < gridSizeX && coords.y < gridSizeZ;
        }

        /// <summary>
        /// Evaluates whether a node can host a turret respecting Buildable and HasTower flags.
        /// </summary>
        public bool IsBuildable(Vector2Int coords)
        {
            GridNode node;
            if (!TryGetNode(coords, out node))
                return false;

            bool available = node.Is(NodeState.Buildable) && !node.Is(NodeState.HasTower);
            return available;
        }

        /// <summary>
        /// Sets the HasTower flag on a node to keep placement state in sync.
        /// </summary>
        public bool SetTowerState(Vector2Int coords, bool hasTower)
        {
            GridNode node;
            if (!TryGetNode(coords, out node))
                return false;

            node.SetState(NodeState.HasTower, hasTower);
            return true;
        }

        /// <summary>
        /// Attempts to convert a world position into the owning grid coordinate.
        /// </summary>
        public bool TryWorldToGrid(Vector3 worldPosition, out Vector2Int coords)
        {
            coords = Vector2Int.zero;

            if (cellSize <= Mathf.Epsilon)
                return false;

            Vector3 origin = Origin;
            float normalizedX = (worldPosition.x - origin.x) / cellSize;
            float normalizedZ = (worldPosition.z - origin.z) / cellSize;
            int gridX = Mathf.FloorToInt(normalizedX);
            int gridZ = Mathf.FloorToInt(normalizedZ);
            coords = new Vector2Int(gridX, gridZ);

            if (!IsWithinBounds(coords))
                return false;

            return true;
        }

        public Vector3 GridToWorld(int x, int z)
        {
            float wx = Origin.x + (x + 0.5f) * cellSize;
            float wz = Origin.z + (z + 0.5f) * cellSize;
            float wy = Origin.y;
            return new Vector3(wx, wy, wz);
        }

        #endregion

        #region Gizmos

        /// <summary>
        /// Draws stacked gizmo layers for each node.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawGridGizmos)
                return;

            InitializeGrid();

            if (graph == null)
                return;

            Color old = Gizmos.color;

            float layerOffset = 0.08f;

            for (int z = 0; z < gridSizeZ; z++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    GridNode node = graph[ToIndex(x, z)];

                    Vector3 basePos = node.WorldPosition;
                    Vector3 size = new Vector3(cellSize, 0.02f, cellSize);

                    int layerIndex = 0;
                    Color topLabelColor = disabledColor;
                    bool anyState = false;

                    if (Contains(walkableNodes, x, z))
                    {
                        Vector3 p = basePos + Vector3.up * (layerOffset * layerIndex);
                        Gizmos.color = walkableColor;
                        Gizmos.DrawCube(p, size);
                        Gizmos.color = wireColor;
                        Gizmos.DrawWireCube(p, size);
                        topLabelColor = walkableColor;
                        layerIndex++;
                        anyState = true;
                    }

                    if (Contains(buildableNodes, x, z))
                    {
                        Vector3 p = basePos + Vector3.up * (layerOffset * layerIndex);
                        Gizmos.color = buildableColor;
                        Gizmos.DrawCube(p, size);
                        Gizmos.color = wireColor;
                        Gizmos.DrawWireCube(p, size);
                        topLabelColor = buildableColor;
                        layerIndex++;
                        anyState = true;
                    }

                    if (Contains(enemyGoalCells, x, z))
                    {
                        Vector3 p = basePos + Vector3.up * (layerOffset * layerIndex);
                        Gizmos.color = goalColor;
                        Gizmos.DrawCube(p, size);
                        Gizmos.color = wireColor;
                        Gizmos.DrawWireCube(p, size);
                        topLabelColor = goalColor;
                        layerIndex++;
                        anyState = true;
                    }

                    if (Contains(enemySpawnCells, x, z))
                    {
                        Vector3 p = basePos + Vector3.up * (layerOffset * layerIndex);
                        Gizmos.color = spawnColor;
                        Gizmos.DrawCube(p, size);
                        Gizmos.color = wireColor;
                        Gizmos.DrawWireCube(p, size);
                        topLabelColor = spawnColor;
                        layerIndex++;
                        anyState = true;
                    }

                    if (!anyState)
                    {
                        Gizmos.color = disabledColor;
                        Gizmos.DrawCube(basePos, size);
                        Gizmos.color = wireColor;
                        Gizmos.DrawWireCube(basePos, size);
                        topLabelColor = disabledColor;
                    }

#if UNITY_EDITOR
                    if (drawNodeCoordinates)
                    {
                        float yoff = layerOffset * layerIndex + 0.1f;
                        Vector3 pos = basePos + Vector3.up * yoff;
                        string label = x.ToString() + "," + z.ToString();
                        UnityEditor.Handles.color = topLabelColor;
                        UnityEditor.Handles.Label(pos, label);
                    }

                    List<GridEdge> tempEdge = node.GetEdges();
                    Gizmos.color = Color.red;

                    for (int i = 0; i < tempEdge.Count; ++i)
                        Gizmos.DrawLine(node.WorldPosition, tempEdge[i].GetOppositeNode(node).WorldPosition);

#endif
                }
            }

            Gizmos.color = old;
        }

        #endregion
    }
}
