using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Grid.Editor
{
    /// <summary>
    /// Custom inspector that paints walkable, buildable, goal, and spawn nodes directly onto a button-based grid preview.
    /// </summary>
    [CustomEditor(typeof(Grid3D))]
    public class Grid3DEditor : UnityEditor.Editor
    {
        #region Nested Types
        /// <summary>
        /// Paint mode applied to nodes when clicking inside the grid preview.
        /// </summary>
        private enum NodePaintMode
        {
            None,
            Walkable,
            Buildable,
            EnemyGoal,
            EnemySpawn,
            WallBinding,
            SpawnBinding
        }
        #endregion

        #region Serialized Properties
        private SerializedProperty gridSizeXProperty;
        private SerializedProperty gridSizeZProperty;
        private SerializedProperty originOffsetProperty;
        private SerializedProperty cellSizeProperty;
        private SerializedProperty walkableNodesProperty;
        private SerializedProperty buildableNodesProperty;
        private SerializedProperty enemyGoalCellsProperty;
        private SerializedProperty enemySpawnCellsProperty;
        private SerializedProperty walkableColorProperty;
        private SerializedProperty buildableColorProperty;
        private SerializedProperty goalColorProperty;
        private SerializedProperty spawnColorProperty;
        private SerializedProperty disabledColorProperty;
        private SerializedProperty drawGridGizmosProperty;
        private SerializedProperty drawNodeCoordinatesProperty;
        private SerializedProperty wireColorProperty;
        private SerializedProperty floorLayerMaskProperty;
        private SerializedProperty floorProbeHalfHeightProperty;
        private SerializedProperty buildableWallBindingsProperty;
        private SerializedProperty start;
        private SerializedProperty end;
        private SerializedProperty path;
        private SerializedProperty enemy;
        private SerializedProperty spawnPointBindingsProperty;
        #endregion

        #region Runtime State
        private NodePaintMode activePaintMode = NodePaintMode.Walkable;
        private const float ButtonSize = 28f;
        private Vector2Int selectedBindingCoords = new Vector2Int(-1, -1);
        private SerializedProperty selectedBindingProperty;
        private bool hasSelectedBinding;
        private Vector2Int selectedSpawnBindingCoords = new Vector2Int(-1, -1);
        private SerializedProperty selectedSpawnBindingProperty;
        private bool hasSelectedSpawnBinding;
        #endregion

        #region Methods
        #region Unity
        /// <summary>
        /// Caches serialized property references for faster inspector drawing.
        /// </summary>
        private void OnEnable()
        {
            gridSizeXProperty = serializedObject.FindProperty("gridSizeX");
            gridSizeZProperty = serializedObject.FindProperty("gridSizeZ");
            originOffsetProperty = serializedObject.FindProperty("originOffset");
            cellSizeProperty = serializedObject.FindProperty("cellSize");
            walkableNodesProperty = serializedObject.FindProperty("walkableNodes");
            buildableNodesProperty = serializedObject.FindProperty("buildableNodes");
            enemyGoalCellsProperty = serializedObject.FindProperty("enemyGoalCells");
            enemySpawnCellsProperty = serializedObject.FindProperty("enemySpawnCells");
            walkableColorProperty = serializedObject.FindProperty("walkableColor");
            buildableColorProperty = serializedObject.FindProperty("buildableColor");
            goalColorProperty = serializedObject.FindProperty("goalColor");
            spawnColorProperty = serializedObject.FindProperty("spawnColor");
            disabledColorProperty = serializedObject.FindProperty("disabledColor");
            drawGridGizmosProperty = serializedObject.FindProperty("drawGridGizmos");
            drawNodeCoordinatesProperty = serializedObject.FindProperty("drawNodeCoordinates");
            wireColorProperty = serializedObject.FindProperty("wireColor");
            floorLayerMaskProperty = serializedObject.FindProperty("floorLayerMask");
            floorProbeHalfHeightProperty = serializedObject.FindProperty("floorProbeHalfHeight");
            buildableWallBindingsProperty = serializedObject.FindProperty("buildableWallBindings");
            start = serializedObject.FindProperty("start");
            end = serializedObject.FindProperty("end");
            path = serializedObject.FindProperty("path");
            enemy = serializedObject.FindProperty("enemy");
            spawnPointBindingsProperty = serializedObject.FindProperty("spawnNodeBindings");
        }

        /// <summary>
        /// Draws grid settings, the active paint selector, and the interactive grid preview.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGridSettings();
            DrawStateArrays();
            DrawPaintSelector();
            DrawGridPreview();
            DrawWallBindingPanel();
            DrawSpawnBindingPanel();
            DrawColorAndGizmoControls();

            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Renders grid dimension and transform settings.
        /// </summary>
        private void DrawGridSettings()
        {
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gridSizeXProperty);
            EditorGUILayout.PropertyField(gridSizeZProperty);
            EditorGUILayout.PropertyField(originOffsetProperty);
            EditorGUILayout.PropertyField(cellSizeProperty);
            EditorGUILayout.PropertyField(floorLayerMaskProperty);
            EditorGUILayout.PropertyField(floorProbeHalfHeightProperty);
            EditorGUILayout.PropertyField(start);
            EditorGUILayout.PropertyField(end);
            EditorGUILayout.PropertyField(path);
            EditorGUILayout.PropertyField(enemy);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Shows the underlying node lists for visibility while editing.
        /// </summary>
        private void DrawStateArrays()
        {
            EditorGUILayout.LabelField("Node Collections", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(walkableNodesProperty, true);
            EditorGUILayout.PropertyField(buildableNodesProperty, true);
            EditorGUILayout.PropertyField(enemyGoalCellsProperty, true);
            EditorGUILayout.PropertyField(enemySpawnCellsProperty, true);
            EditorGUILayout.PropertyField(spawnPointBindingsProperty, true);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Displays active paint mode and color legend controls.
        /// </summary>
        private void DrawPaintSelector()
        {
            EditorGUILayout.LabelField("Grid Painter", EditorStyles.boldLabel);
            activePaintMode = (NodePaintMode)EditorGUILayout.EnumPopup("Active Paint Mode", activePaintMode);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws the interactive grid matching configured dimensions.
        /// </summary>
        private void DrawGridPreview()
        {
            Grid3D gridTarget = target as Grid3D;
            if (gridTarget == null)
                return;

            int sizeX = Mathf.Max(1, gridSizeXProperty.intValue);
            int sizeZ = Mathf.Max(1, gridSizeZProperty.intValue);

            HashSet<Vector2Int> walkableSet = BuildSetFromProperty(walkableNodesProperty);
            HashSet<Vector2Int> buildableSet = BuildSetFromProperty(buildableNodesProperty);
            HashSet<Vector2Int> goalSet = BuildSetFromProperty(enemyGoalCellsProperty);
            HashSet<Vector2Int> spawnSet = BuildSetFromProperty(enemySpawnCellsProperty);

            Color originalColor = GUI.backgroundColor;
            EditorGUILayout.LabelField("Grid Preview", EditorStyles.boldLabel);

            for (int z = sizeZ - 1; z >= 0; z--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < sizeX; x++)
                {
                    Vector2Int coords = new Vector2Int(x, z);
                    bool isWalkable = walkableSet.Contains(coords);
                    bool isBuildable = buildableSet.Contains(coords);
                    bool isGoal = goalSet.Contains(coords);
                    bool isSpawn = spawnSet.Contains(coords);
                    GUIContent label = new GUIContent(BuildCellLabel(isWalkable, isBuildable, isGoal, isSpawn, x, z));
                    GUIStyle style = new GUIStyle(GUI.skin.button);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 10;
                    GUI.backgroundColor = ResolveCellColor(isWalkable, isBuildable, isGoal, isSpawn);

                    if (GUILayout.Button(label, style, GUILayout.Width(ButtonSize), GUILayout.Height(ButtonSize)))
                    {
                        ApplyPaint(coords, walkableSet, buildableSet, goalSet, spawnSet);
                        WriteSetToProperty(walkableNodesProperty, walkableSet);
                        WriteSetToProperty(buildableNodesProperty, buildableSet);
                        WriteSetToProperty(enemyGoalCellsProperty, goalSet);
                        WriteSetToProperty(enemySpawnCellsProperty, spawnSet);
                        EditorUtility.SetDirty(gridTarget);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// Shows debug colors and gizmo toggles after the grid for clarity.
        /// </summary>
        private void DrawColorAndGizmoControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("State Colors", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(walkableColorProperty);
            EditorGUILayout.PropertyField(buildableColorProperty);
            EditorGUILayout.PropertyField(goalColorProperty);
            EditorGUILayout.PropertyField(spawnColorProperty);
            EditorGUILayout.PropertyField(disabledColorProperty);
            EditorGUILayout.PropertyField(wireColorProperty);
            EditorGUILayout.PropertyField(drawGridGizmosProperty);
            EditorGUILayout.PropertyField(drawNodeCoordinatesProperty);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Displays renderer bindings for the selected buildable node when using the wall binding paint mode.
        /// </summary>
        private void DrawWallBindingPanel()
        {
            if (!hasSelectedBinding || selectedBindingProperty == null)
                return;

            EditorGUILayout.LabelField("Wall Visibility Binding", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Selected Cell", $"{selectedBindingCoords.x},{selectedBindingCoords.y}");
            SerializedProperty wallsProperty = selectedBindingProperty.FindPropertyRelative("HiddenWalls");
            if (wallsProperty != null)
                EditorGUILayout.PropertyField(wallsProperty, new GUIContent("Hidden Walls"), true);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Displays spawn binding assignment for the selected spawn node.
        /// </summary>
        private void DrawSpawnBindingPanel()
        {
            if (!hasSelectedSpawnBinding || selectedSpawnBindingProperty == null)
                return;

            EditorGUILayout.LabelField("Spawn Binding", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Selected Spawn Cell", $"{selectedSpawnBindingCoords.x},{selectedSpawnBindingCoords.y}");
            SerializedProperty spawnPointProperty = selectedSpawnBindingProperty.FindPropertyRelative("SpawnPoint");
            if (spawnPointProperty != null)
                EditorGUILayout.PropertyField(spawnPointProperty, new GUIContent("Spawn Point"), true);
            SerializedProperty slidingDoorProperty = selectedSpawnBindingProperty.FindPropertyRelative("SlidingDoor");
            if (slidingDoorProperty != null)
                EditorGUILayout.PropertyField(slidingDoorProperty, new GUIContent("Sliding Door"), true);
            EditorGUILayout.Space();
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Builds a label showing active states and coordinates.
        /// </summary>
        private string BuildCellLabel(bool isWalkable, bool isBuildable, bool isGoal, bool isSpawn, int x, int z)
        {
            string flags = string.Empty;
            if (isWalkable)
                flags += "W";
            if (isBuildable)
                flags += "B";
            if (isGoal)
                flags += "G";
            if (isSpawn)
                flags += "S";

            if (string.IsNullOrEmpty(flags))
                flags = "-";

            return flags + System.Environment.NewLine + x.ToString() + "," + z.ToString();
        }

        /// <summary>
        /// Converts a serialized Vector2Int array into a hash set for faster lookups.
        /// </summary>
        private HashSet<Vector2Int> BuildSetFromProperty(SerializedProperty property)
        {
            HashSet<Vector2Int> set = new HashSet<Vector2Int>();
            if (property == null || !property.isArray)
                return set;

            int count = property.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                Vector2Int coords = element.vector2IntValue;
                set.Add(coords);
            }

            return set;
        }

        /// <summary>
        /// Writes a hash set of coordinates back into a serialized array property.
        /// </summary>
        private void WriteSetToProperty(SerializedProperty property, HashSet<Vector2Int> coords)
        {
            if (property == null)
                return;

            property.ClearArray();
            int index = 0;
            foreach (Vector2Int coord in coords)
            {
                property.InsertArrayElementAtIndex(index);
                SerializedProperty element = property.GetArrayElementAtIndex(index);
                element.vector2IntValue = coord;
                index++;
            }
        }

        /// <summary>
        /// Applies the current paint mode to the clicked coordinate.
        /// </summary>
        private void ApplyPaint(Vector2Int coords, HashSet<Vector2Int> walkableSet, HashSet<Vector2Int> buildableSet, HashSet<Vector2Int> goalSet, HashSet<Vector2Int> spawnSet)
        {
            if (activePaintMode != NodePaintMode.SpawnBinding)
            {
                hasSelectedSpawnBinding = false;
                selectedSpawnBindingProperty = null;
            }

            if (activePaintMode == NodePaintMode.WallBinding)
            {
                if (!buildableSet.Contains(coords))
                {
                    hasSelectedBinding = false;
                    selectedBindingProperty = null;
                    return;
                }

                hasSelectedBinding = true;
                selectedBindingCoords = coords;
                selectedBindingProperty = GetOrCreateBinding(coords);
                return;
            }

            if (activePaintMode == NodePaintMode.SpawnBinding)
            {
                if (!spawnSet.Contains(coords))
                {
                    hasSelectedSpawnBinding = false;
                    selectedSpawnBindingProperty = null;
                    return;
                }

                hasSelectedSpawnBinding = true;
                selectedSpawnBindingCoords = coords;
                selectedSpawnBindingProperty = GetOrCreateSpawnBinding(coords);
                return;
            }

            switch (activePaintMode)
            {
                case NodePaintMode.None:
                    walkableSet.Remove(coords);
                    buildableSet.Remove(coords);
                    goalSet.Remove(coords);
                    spawnSet.Remove(coords);
                    break;
                case NodePaintMode.Walkable:
                    if (walkableSet.Contains(coords))
                    {
                        walkableSet.Remove(coords);
                        break;
                    }
                    walkableSet.Add(coords);
                    break;
                case NodePaintMode.Buildable:
                    if (buildableSet.Contains(coords))
                    {
                        buildableSet.Remove(coords);
                        break;
                    }
                    buildableSet.Add(coords);
                    break;
                case NodePaintMode.EnemyGoal:
                    if (goalSet.Contains(coords))
                    {
                        goalSet.Remove(coords);
                        break;
                    }
                    goalSet.Add(coords);
                    break;
                case NodePaintMode.EnemySpawn:
                    if (spawnSet.Contains(coords))
                    {
                        spawnSet.Remove(coords);
                        break;
                    }
                    spawnSet.Add(coords);
                    break;
            }
        }

        /// <summary>
        /// Resolves the background color for a cell based on its current flags.
        /// </summary>
        private Color ResolveCellColor(bool isWalkable, bool isBuildable, bool isGoal, bool isSpawn)
        {
            Color sum = Color.black;
            int contributions = 0;

            if (isWalkable && walkableColorProperty != null)
            {
                sum += walkableColorProperty.colorValue;
                contributions++;
            }

            if (isBuildable && buildableColorProperty != null)
            {
                sum += buildableColorProperty.colorValue;
                contributions++;
            }

            if (isGoal && goalColorProperty != null)
            {
                sum += goalColorProperty.colorValue;
                contributions++;
            }

            if (isSpawn && spawnColorProperty != null)
            {
                sum += spawnColorProperty.colorValue;
                contributions++;
            }

            if (contributions == 0)
                return disabledColorProperty != null ? disabledColorProperty.colorValue : Color.gray;

            float divisor = 1f / contributions;
            Color averaged = new Color(sum.r * divisor, sum.g * divisor, sum.b * divisor, 1f);
            return averaged;
        }

        /// <summary>
        /// Returns the serialized binding property for the provided coordinates, creating one if missing.
        /// </summary>
        private SerializedProperty GetOrCreateBinding(Vector2Int coords)
        {
            if (buildableWallBindingsProperty == null)
                return null;

            int count = buildableWallBindingsProperty.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = buildableWallBindingsProperty.GetArrayElementAtIndex(i);
                SerializedProperty coordinatesProperty = element != null ? element.FindPropertyRelative("Coordinates") : null;
                if (coordinatesProperty != null && coordinatesProperty.vector2IntValue == coords)
                    return element;
            }

            int newIndex = buildableWallBindingsProperty.arraySize;
            buildableWallBindingsProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = buildableWallBindingsProperty.GetArrayElementAtIndex(newIndex);
            SerializedProperty newCoordinatesProperty = newElement != null ? newElement.FindPropertyRelative("Coordinates") : null;
            if (newCoordinatesProperty != null)
                newCoordinatesProperty.vector2IntValue = coords;

            SerializedProperty wallsProperty = newElement != null ? newElement.FindPropertyRelative("HiddenWalls") : null;
            if (wallsProperty != null)
                wallsProperty.ClearArray();

            return newElement;
        }

        /// <summary>
        /// Returns the serialized spawn binding property for the provided coordinates, creating one if missing.
        /// </summary>
        private SerializedProperty GetOrCreateSpawnBinding(Vector2Int coords)
        {
            if (spawnPointBindingsProperty == null)
                return null;

            int count = spawnPointBindingsProperty.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = spawnPointBindingsProperty.GetArrayElementAtIndex(i);
                SerializedProperty coordinatesProperty = element != null ? element.FindPropertyRelative("Coordinates") : null;
                if (coordinatesProperty != null && coordinatesProperty.vector2IntValue == coords)
                    return element;
            }

            int newIndex = spawnPointBindingsProperty.arraySize;
            spawnPointBindingsProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = spawnPointBindingsProperty.GetArrayElementAtIndex(newIndex);
            SerializedProperty newCoordinatesProperty = newElement != null ? newElement.FindPropertyRelative("Coordinates") : null;
            if (newCoordinatesProperty != null)
                newCoordinatesProperty.vector2IntValue = coords;

            SerializedProperty spawnPointProperty = newElement != null ? newElement.FindPropertyRelative("SpawnPoint") : null;
            if (spawnPointProperty != null)
                spawnPointProperty.objectReferenceValue = null;

            SerializedProperty slidingDoorProperty = newElement != null ? newElement.FindPropertyRelative("SlidingDoor") : null;
            if (slidingDoorProperty != null)
                slidingDoorProperty.objectReferenceValue = null;

            return newElement;
        }
        #endregion
        #endregion
    }
}
