using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grid
{
    [DefaultExecutionOrder(-200)]
    /// <summary>
    /// Manages a 3D gameplay grid with offset-based origin, bitmask node states and layered debug gizmos.
    /// </summary>
    public class Grid3D : Singleton<Grid3D>
    {
        #region Variables And Properties
        #region Serialized Fields
        [Header("Grid Settings")]
        [Tooltip("Number of cells along the X axis of the grid.")]
        [SerializeField] private int gridSizeX = 10;
        [Tooltip("Number of cells along the Z axis of the grid.")]
        [SerializeField] private int gridSizeZ = 10;
        [Tooltip("Offset relative to transform.position used as grid origin. Y defines grid height.")]
        [SerializeField] private Vector3 originOffset = Vector3.zero;
        [Tooltip("Size of each cell in world units.")]
        [SerializeField] private float cellSize = 1.0f;

        [Header("Height Sampling")]
        [Tooltip("Layer mask used to snap node height to the nearest floor surface.")]
        [SerializeField] private LayerMask floorLayerMask;
        [Tooltip("Half-height in meters used when probing above and below each node for the Floor surface.")]
        [SerializeField] private float floorProbeHalfHeight = 6f;

        [Header("Static Map Configuration")]
        [Tooltip("Grid coordinates that are walkable.")]
        [SerializeField] private Vector2Int[] walkableNodes;
        [Tooltip("Grid coordinates where towers can be built.")]
        [SerializeField] private Vector2Int[] buildableNodes;
        [Tooltip("Grid coordinates used as enemy goal nodes.")]
        [SerializeField] private Vector2Int[] enemyGoalCells;

        [Header("Enemy Spawn Points")]
        [Tooltip("Grid coordinates used as enemy spawn points.")]
        [SerializeField] private Vector2Int[] enemySpawnCells;
        [Tooltip("Optional spawn point bindings assigned to enemy spawn cells for precise placement.")]
        [SerializeField]private SpawnNodeBinding[] spawnNodeBindings;

        [Header("Gizmos")]
        [Tooltip("Draws grid gizmos when the object is selected.")]
        [SerializeField] private bool drawGridGizmos = true;
        [Tooltip("Displays (x,z) text above each node.")]
        [SerializeField]private bool drawNodeCoordinates = true;
        [Tooltip("Color used for walkable cells.")]
        [SerializeField]private Color walkableColor = new Color(0.0f, 1.0f, 0.0f, 0.35f);
        [Tooltip("Color used for buildable cells.")]
        [SerializeField]private Color buildableColor = new Color(0.0f, 0.5f, 1.0f, 0.35f);
        [Tooltip("Color used for enemy goal cells.")]
        [SerializeField] private Color goalColor = new Color(1.0f, 1.0f, 0.0f, 0.55f);
        [Tooltip("Color used for enemy spawn nodes.")]
        [SerializeField]private Color spawnColor = new Color(1.0f, 0.0f, 0.0f, 0.55f);
        [Tooltip("Color used for disabled nodes.")]
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        [Tooltip("Color used for wireframe lines.")]
        [SerializeField]private Color wireColor = new Color(1.0f, 1.0f, 1.0f, 0.15f);

        [Header("Visibility Bindings")]
        [Tooltip("Walls hidden only when a turret is possessed on the matching buildable node.")]
        [SerializeField] private BuildableWallBinding[] buildableWallBindings;
        #endregion

        #region Runtime

        // Flat array of nodes.
        public GridNode[] graph;
        private bool gridInitialized;
        #endregion

        #region Nested Types
        [System.Serializable]
        /// <summary>
        /// Authoring container pairing a buildable cell with renderers that hide while its turret is possessed.
        /// </summary>
        public struct BuildableWallBinding
        {
            [Tooltip("Buildable grid coordinate associated with this visibility rule.")]
            public Vector2Int Coordinates;

            [Tooltip("Renderers hidden while the turret on this node is possessed.")]
            public Renderer[] HiddenWalls;
        }

        [System.Serializable]
        /// <summary>
        /// Binding that overrides the spawn position and rotation for a given spawn node.
        /// </summary>
        public struct SpawnNodeBinding
        {
            [Tooltip("Spawn node coordinate associated with this binding.")]
            public Vector2Int Coordinates;

            [Tooltip("Optional sliding door paired with this spawn point for build-phase previews.")]
            public SpawnPointDoor SlidingDoor;
        }
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

        public Vector2Int[] GetEnemySpawnCoords()
        {
            return enemySpawnCells != null ? enemySpawnCells : System.Array.Empty<Vector2Int>();
        }

        #endregion
        #endregion

        #region Methods
        #region Unity

        /// <summary>
        /// Ensures grid is initialized before gameplay.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
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

            if (floorProbeHalfHeight < 0.25f)
                floorProbeHalfHeight = 0.25f;

            if (floorLayerMask == 0)
            {
                int floorLayer = LayerMask.NameToLayer("Floor");
                if (floorLayer >= 0)
                    floorLayerMask = 1 << floorLayer;
            }

            ClampArrayCoords(walkableNodes);
            ClampArrayCoords(buildableNodes);
            ClampArrayCoords(enemyGoalCells);
            ClampArrayCoords(enemySpawnCells);
            ClampBindingCoords();
            ClampSpawnBindingCoords();

            InitializeGrid();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Builds or rebuilds the grid, applying static map configuration.
        /// </summary>
        private void InitializeGrid()
        {
            if (floorLayerMask == 0)
            {
                int floorLayer = LayerMask.NameToLayer("Floor");
                if (floorLayer >= 0)
                    floorLayerMask = 1 << floorLayer;
            }
            if (floorProbeHalfHeight < 0.25f)
                floorProbeHalfHeight = 0.25f;

            if (gridSizeX < 1 || gridSizeZ < 1)
            {
                graph = Array.Empty<GridNode>();
                gridInitialized = true;
                return;
            }

            int total = gridSizeX * gridSizeZ;
            graph = new GridNode[total];

            for (int z = 0; z < gridSizeZ; z++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    int index = ToIndex(x, z);
                    NodeState staticState = ResolveStaticState(x, z);
                    Vector3 position = ResolveWorldPosition(x, z);
                    graph[index] = new GridNode(index, x, z, position, staticState);

                    GridNode node = graph[index];

                    if (node == null)
                        continue;

                    if (x > 0)
                    {
                        GridNode left = graph[ToIndex(x - 1, z)];

                        if (left != null && left.Is(NodeState.Walkable))
                            node.AddEdge(left);
                    }

                    if (z > 0)
                    {
                        GridNode down = graph[ToIndex(x, z - 1)];

                        if (down != null && down.Is(NodeState.Walkable))
                            node.AddEdge(down);
                    }
                }
            }

            gridInitialized = true;
        }

        #endregion

        #region Dijkstra

        /// <summary>
        /// Executes Dijkstra traversal from the provided root node and caches distances and predecessors.
        /// </summary>
        private DijkstraInfo SearchWithDijkstraAlgorithm(in GridNode rootNode, in GridNode endNode)
        {
            return SearchWithDijkstraAlgorithm(in rootNode);
        }

        /// <summary>
        /// Executes Dijkstra traversal from the provided root node and caches distances and predecessors.
        /// </summary>
        private DijkstraInfo SearchWithDijkstraAlgorithm(in GridNode rootNode)
        {
            int nodeCount = graph != null ? graph.Length : 0;
            int[] distancesFromStart = new int[nodeCount];
            int[] cheapestPreviousPoint = new int[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                distancesFromStart[i] = int.MaxValue;
                cheapestPreviousPoint[i] = -1;
            }

            if (rootNode == null || nodeCount == 0)
                return new DijkstraInfo(distancesFromStart, cheapestPreviousPoint);

            distancesFromStart[rootNode.Index] = 0;
            PriorityQueue<DijkstraElement> priorityQueue = new PriorityQueue<DijkstraElement>(true);
            priorityQueue.Enqueue(new DijkstraElement(rootNode.Index, 0));

            while (!priorityQueue.empty())
            {
                DijkstraElement currentNodeInfo = priorityQueue.dequeue_min();
                int currentNodeIndex = currentNodeInfo.NodeIndex;
                GridNode currentNode = graph[currentNodeIndex];
                if (currentNode == null)
                    continue;

                currentNode.SortEdgesByCheapest();
                List<GridEdge> edges = currentNode.edges;
                int edgeCount = edges != null ? edges.Count : 0;

                for (int i = 0; i < edgeCount; i++)
                {
                    GridNode neighbour = edges[i].GetOppositeNode(currentNode);
                    if (neighbour == null)
                        continue;

                    int currentDistance = distancesFromStart[currentNodeIndex];
                    if (currentDistance == int.MaxValue)
                        continue;

                    int candidateDistance = currentDistance + edges[i].weight;
                    if (candidateDistance < distancesFromStart[neighbour.Index])
                    {
                        distancesFromStart[neighbour.Index] = candidateDistance;
                        cheapestPreviousPoint[neighbour.Index] = currentNode.Index;
                        priorityQueue.Enqueue(new DijkstraElement(neighbour.Index, candidateDistance));
                    }
                }
            }

            DijkstraInfo info = new DijkstraInfo(distancesFromStart, cheapestPreviousPoint);
            return info;
        }

        /// <summary>
        /// Builds the shortest path from the world-space start position to the closest enemy goal node.
        /// </summary>
        public bool TryBuildPathToClosestGoal(Vector3 startPosition, List<Vector3> worldPathBuffer)
        {
            if (worldPathBuffer == null)
                return false;

            worldPathBuffer.Clear();
            EnsureGridInitialized();

            if (graph == null || graph.Length == 0)
                return false;

            Vector2Int startCoords;
            if (!TryWorldToGrid(startPosition, out startCoords))
                return false;

            GridNode startNode;
            if (!TryGetNode(startCoords, out startNode))
                return false;

            if (!startNode.Is(NodeState.Walkable))
                return false;

            DijkstraInfo searchResult = SearchWithDijkstraAlgorithm(in startNode);
            int closestGoalIndex = ResolveClosestGoalIndex(in searchResult);
            if (closestGoalIndex < 0)
                return false;

            ReconstructWorldPath(startNode.Index, closestGoalIndex, searchResult.Previous, worldPathBuffer);
            return worldPathBuffer.Count > 0;
        }

        /// <summary>
        /// Returns the index of the closest reachable enemy goal.
        /// </summary>
        private int ResolveClosestGoalIndex(in DijkstraInfo searchResult)
        {
            if (enemyGoalCells == null || enemyGoalCells.Length == 0)
                return -1;

            int bestIndex = -1;
            int bestDistance = int.MaxValue;
            int goalCount = enemyGoalCells.Length;

            for (int i = 0; i < goalCount; i++)
            {
                GridNode goalNode;
                if (!TryGetNode(enemyGoalCells[i], out goalNode))
                    continue;

                int[] distances = searchResult.Distances;
                int distance = distances != null && goalNode.Index >= 0 && goalNode.Index < distances.Length ? distances[goalNode.Index] : int.MaxValue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = goalNode.Index;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Writes the world-space path from start to target into the provided buffer.
        /// </summary>
        private void ReconstructWorldPath(int startIndex, int targetIndex, in int[] previous, List<Vector3> worldPathBuffer)
        {
            if (previous == null || worldPathBuffer == null || graph == null || graph.Length == 0)
                return;

            int currentIndex = targetIndex;
            int safety = graph.Length + 2;

            bool reachedStart = false;

            while (currentIndex >= 0 && currentIndex < graph.Length && safety > 0)
            {
                GridNode node = graph[currentIndex];
                if (node == null)
                    break;

                worldPathBuffer.Add(node.WorldPosition);

                if (currentIndex == startIndex)
                {
                    reachedStart = true;
                    break;
                }

                currentIndex = previous[currentIndex];
                safety--;
            }

            if (!reachedStart)
            {
                worldPathBuffer.Clear();
                return;
            }

            worldPathBuffer.Reverse();
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

        /// <summary>
        /// Ensures buildable wall binding coordinates stay within grid bounds.
        /// </summary>
        private void ClampBindingCoords()
        {
            if (buildableWallBindings == null)
                return;

            for (int i = 0; i < buildableWallBindings.Length; i++)
            {
                Vector2Int clamped = buildableWallBindings[i].Coordinates;
                if (clamped.x < 0)
                    clamped.x = 0;
                if (clamped.y < 0)
                    clamped.y = 0;
                if (clamped.x >= gridSizeX)
                    clamped.x = gridSizeX - 1;
                if (clamped.y >= gridSizeZ)
                    clamped.y = gridSizeZ - 1;
                buildableWallBindings[i].Coordinates = clamped;
            }
        }

        /// <summary>
        /// Ensures spawn binding coordinates stay within grid bounds.
        /// </summary>
        private void ClampSpawnBindingCoords()
        {
            if (spawnNodeBindings == null)
                return;

            for (int i = 0; i < spawnNodeBindings.Length; i++)
            {
                Vector2Int clamped = spawnNodeBindings[i].Coordinates;
                if (clamped.x < 0)
                    clamped.x = 0;
                if (clamped.y < 0)
                    clamped.y = 0;
                if (clamped.x >= gridSizeX)
                    clamped.x = gridSizeX - 1;
                if (clamped.y >= gridSizeZ)
                    clamped.y = gridSizeZ - 1;
                spawnNodeBindings[i].Coordinates = clamped;
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
        /// Aggregates static flags assigned through the serialized arrays.
        /// </summary>
        private NodeState ResolveStaticState(int x, int z)
        {
            NodeState state = NodeState.Default;

            if (Contains(walkableNodes, x, z))
                state |= NodeState.Walkable;

            if (Contains(buildableNodes, x, z))
                state |= NodeState.Buildable;

            if (Contains(enemyGoalCells, x, z))
                state |= NodeState.IsEnemyGoal;

            if (Contains(enemySpawnCells, x, z))
                state |= NodeState.Walkable;

            return state;
        }

        /// <summary>
        /// Returns world coordinates for the provided grid coordinate.
        /// </summary>
        public Vector3 GridToWorld(Vector2Int coords)
        {
            return GridToWorld(coords.x, coords.y);
        }

        /// <summary>
        /// Computes the world position for a grid coordinate, snapping height to the nearest floor surface.
        /// </summary>
        private Vector3 ResolveWorldPosition(int x, int z)
        {
            float wx = Origin.x + (x + 0.5f) * cellSize;
            float wz = Origin.z + (z + 0.5f) * cellSize;
            float baseY = Origin.y;
            Vector3 basePosition = new Vector3(wx, baseY, wz);
            float sampledY = SampleFloorHeight(basePosition);
            basePosition.y = sampledY;
            return basePosition;
        }

        /// <summary>
        /// Samples upward and downward to locate the closest floor surface around the given position.
        /// </summary>
        private float SampleFloorHeight(Vector3 basePosition)
        {
            if (floorLayerMask == 0)
                return basePosition.y;

            float probeDistance = Mathf.Max(floorProbeHalfHeight, cellSize);
            Vector3 upperOrigin = basePosition + Vector3.up * probeDistance;
            Vector3 lowerOrigin = basePosition + Vector3.down * probeDistance;
            RaycastHit hitInfo;

            if (Physics.Raycast(upperOrigin, Vector3.down, out hitInfo, probeDistance * 2f, floorLayerMask, QueryTriggerInteraction.Ignore))
                return hitInfo.point.y;

            if (Physics.Raycast(lowerOrigin, Vector3.up, out hitInfo, probeDistance * 2f, floorLayerMask, QueryTriggerInteraction.Ignore))
                return hitInfo.point.y;

            return basePosition.y;
        }

        /// <summary>
        /// Safe accessor that returns the node at coordinates when available.
        /// </summary>
        public bool TryGetNode(Vector2Int coords, out GridNode node)
        {
            node = null;
            EnsureGridInitialized();

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

        /// <summary>
        /// Returns the cached world position for the provided coordinate, falling back to a sampled value.
        /// </summary>
        public Vector3 GridToWorld(int x, int z)
        {
            if (graph != null && graph.Length == gridSizeX * gridSizeZ)
            {
                int index = ToIndex(x, z);
                if (index >= 0 && index < graph.Length && graph[index] != null)
                    return graph[index].WorldPosition;
            }

            return ResolveWorldPosition(x, z);
        }

        /// <summary>
        /// Returns the renderer set associated with a buildable coordinate for possession visibility.
        /// </summary>
        public Renderer[] GetBuildableWallRenderers(Vector2Int coords)
        {
            if (buildableWallBindings == null || buildableWallBindings.Length == 0)
                return null;

            for (int i = 0; i < buildableWallBindings.Length; i++)
            {
                if (buildableWallBindings[i].Coordinates == coords)
                    return buildableWallBindings[i].HiddenWalls;
            }

            return null;
        }

        /// <summary>
        /// Returns the spawn door assigned to a spawn coordinate when present.
        /// </summary>
        public SpawnPointDoor GetSpawnDoor(Vector2Int coords)
        {
            if (spawnNodeBindings == null || spawnNodeBindings.Length == 0)
                return null;

            for (int i = 0; i < spawnNodeBindings.Length; i++)
            {
                if (spawnNodeBindings[i].Coordinates == coords)
                    return spawnNodeBindings[i].SlidingDoor;
            }

            return null;
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

            EnsureGridInitialized();

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

        #region Internal
        /// <summary>
        /// Ensures the grid data exists before runtime queries mutate it.
        /// </summary>
        private void EnsureGridInitialized()
        {
            if (gridInitialized && graph != null && graph.Length == gridSizeX * gridSizeZ)
                return;

            gridInitialized = false;
            InitializeGrid();
        }
        #endregion
        #endregion

        #region NestedClasses
        /// <summary>
        /// Container used to track node indices and costs within the priority queue.
        /// </summary>
        private class DijkstraElement : IComparable<DijkstraElement>
        {
            public int NodeIndex { get; private set; }
            public int PathCost { get; private set; }

            public DijkstraElement(int nodeIndex, int pathCost)
            {
                NodeIndex = nodeIndex;
                PathCost = pathCost;
            }

            //USED FOR QUEUE PRIORITY
            public int CompareTo(DijkstraElement other)
            {
                return PathCost.CompareTo(other.PathCost);
            }
        }
        #endregion
    }
}
