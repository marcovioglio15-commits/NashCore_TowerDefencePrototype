using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Grid;
using Enemy;

/// <summary>
/// Custom inspector for HordesManager that constrains spawn node selection to grid spawn cells.
/// </summary>
[CustomEditor(typeof(HordesManager))]
public class HordesManagerEditor : Editor
{
    #region Runtime State
    private SerializedProperty gridProperty;
    private SerializedProperty gameManagerProperty;
    private SerializedProperty hordesProperty;
    private SerializedProperty defenceStartDelayProperty;
    #endregion

    #region Unity
    private void OnEnable()
    {
        gridProperty = serializedObject.FindProperty("grid");
        gameManagerProperty = serializedObject.FindProperty("gameManager");
        hordesProperty = serializedObject.FindProperty("hordes");
        defenceStartDelayProperty = serializedObject.FindProperty("defenceStartDelay");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(gridProperty);
        EditorGUILayout.PropertyField(gameManagerProperty);
        EditorGUILayout.PropertyField(defenceStartDelayProperty);

        DrawHordesSection();

        serializedObject.ApplyModifiedProperties();
    }
    #endregion

    #region Drawing
    private void DrawHordesSection()
    {
        EditorGUILayout.PropertyField(hordesProperty, new GUIContent("Hordes"), false);
        if (!hordesProperty.isExpanded)
            return;

        Grid3D gridTarget = gridProperty.objectReferenceValue as Grid3D;
        Vector2Int[] spawnCoords = gridTarget != null ? gridTarget.GetEnemySpawnCoords() : System.Array.Empty<Vector2Int>();
        string[] spawnLabels = BuildSpawnLabels(spawnCoords);

        EditorGUI.indentLevel++;
        int hordeCount = hordesProperty.arraySize;
        for (int i = 0; i < hordeCount; i++)
        {
            SerializedProperty hordeProperty = hordesProperty.GetArrayElementAtIndex(i);
            SerializedProperty keyProperty = hordeProperty.FindPropertyRelative("key");
            SerializedProperty wavesProperty = hordeProperty.FindPropertyRelative("waves");

            EditorGUILayout.PropertyField(keyProperty);
            EditorGUILayout.PropertyField(wavesProperty, new GUIContent("Waves"), false);
            if (wavesProperty.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawWavesList(wavesProperty, spawnCoords, spawnLabels);
                DrawWaveArrayControls(wavesProperty, spawnCoords);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
        }
        EditorGUI.indentLevel--;
    }

    private void DrawWavesList(SerializedProperty wavesProperty, Vector2Int[] spawnCoords, string[] spawnLabels)
    {
        int waveCount = wavesProperty.arraySize;
        for (int i = 0; i < waveCount; i++)
        {
            SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(i);
            SerializedProperty enemyDefinitionProperty = waveProperty.FindPropertyRelative("enemyDefinition");
            SerializedProperty runtimeModifiersProperty = waveProperty.FindPropertyRelative("runtimeModifiers");
            SerializedProperty enemyCountProperty = waveProperty.FindPropertyRelative("enemyCount");
            SerializedProperty spawnCadenceProperty = waveProperty.FindPropertyRelative("spawnCadenceSeconds");
            SerializedProperty spawnOffsetProperty = waveProperty.FindPropertyRelative("spawnOffset");
            SerializedProperty spawnNodesProperty = waveProperty.FindPropertyRelative("spawnNodes");
            SerializedProperty advanceModeProperty = waveProperty.FindPropertyRelative("advanceMode");
            SerializedProperty advanceDelayProperty = waveProperty.FindPropertyRelative("advanceDelaySeconds");

            EditorGUILayout.LabelField($"Wave {i + 1}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enemyDefinitionProperty);
            EditorGUILayout.PropertyField(runtimeModifiersProperty, true);
            EditorGUILayout.PropertyField(enemyCountProperty);
            EditorGUILayout.PropertyField(spawnCadenceProperty);
            EditorGUILayout.PropertyField(spawnOffsetProperty);
            DrawSpawnNodesSelector(spawnNodesProperty, spawnCoords, spawnLabels);
            EditorGUILayout.PropertyField(advanceModeProperty);
            EditorGUILayout.PropertyField(advanceDelayProperty);
            EditorGUILayout.Space();
        }
    }

    private void DrawSpawnNodesSelector(SerializedProperty spawnNodesProperty, Vector2Int[] spawnCoords, string[] spawnLabels)
    {
        EditorGUILayout.LabelField("Spawn Nodes");
        if (spawnCoords.Length == 0)
        {
            EditorGUILayout.HelpBox("No spawn nodes found on the assigned Grid3D. Paint enemy spawn cells to enable selection.", MessageType.Warning);
            return;
        }

        int currentSize = spawnNodesProperty.arraySize;
        for (int i = 0; i < currentSize; i++)
        {
            SerializedProperty element = spawnNodesProperty.GetArrayElementAtIndex(i);
            Vector2Int currentValue = element.vector2IntValue;
            int selectedIndex = System.Array.IndexOf(spawnCoords, currentValue);
            if (selectedIndex < 0)
                selectedIndex = 0;

            int newIndex = EditorGUILayout.Popup($"Spawn #{i + 1}", selectedIndex, spawnLabels);
            if (newIndex >= 0 && newIndex < spawnCoords.Length)
                element.vector2IntValue = spawnCoords[newIndex];
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Spawn Node"))
        {
            int newIndex = spawnNodesProperty.arraySize;
            spawnNodesProperty.InsertArrayElementAtIndex(newIndex);
            spawnNodesProperty.GetArrayElementAtIndex(newIndex).vector2IntValue = spawnCoords[0];
        }
        if (GUILayout.Button("Remove Last") && spawnNodesProperty.arraySize > 0)
        {
            spawnNodesProperty.DeleteArrayElementAtIndex(spawnNodesProperty.arraySize - 1);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawWaveArrayControls(SerializedProperty wavesProperty, Vector2Int[] spawnCoords)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Wave"))
            AppendWave(wavesProperty, spawnCoords);

        if (GUILayout.Button("Remove Last") && wavesProperty.arraySize > 0)
            wavesProperty.DeleteArrayElementAtIndex(wavesProperty.arraySize - 1);

        EditorGUILayout.EndHorizontal();
    }
    #endregion

    #region Helpers
    private string[] BuildSpawnLabels(IReadOnlyList<Vector2Int> coords)
    {
        if (coords == null || coords.Count == 0)
            return new[] { "None" };

        string[] labels = new string[coords.Count];
        for (int i = 0; i < coords.Count; i++)
            labels[i] = $"({coords[i].x},{coords[i].y})";

        return labels;
    }

    private void AppendWave(SerializedProperty wavesProperty, Vector2Int[] spawnCoords)
    {
        int newIndex = wavesProperty.arraySize;
        wavesProperty.InsertArrayElementAtIndex(newIndex);
        SerializedProperty wave = wavesProperty.GetArrayElementAtIndex(newIndex);
        if (wave == null)
            return;

        SerializedProperty enemyDefinitionProperty = wave.FindPropertyRelative("enemyDefinition");
        SerializedProperty runtimeModifiersProperty = wave.FindPropertyRelative("runtimeModifiers");
        SerializedProperty enemyCountProperty = wave.FindPropertyRelative("enemyCount");
        SerializedProperty spawnCadenceProperty = wave.FindPropertyRelative("spawnCadenceSeconds");
        SerializedProperty spawnOffsetProperty = wave.FindPropertyRelative("spawnOffset");
        SerializedProperty spawnNodesProperty = wave.FindPropertyRelative("spawnNodes");
        SerializedProperty advanceModeProperty = wave.FindPropertyRelative("advanceMode");
        SerializedProperty advanceDelayProperty = wave.FindPropertyRelative("advanceDelaySeconds");

        if (enemyDefinitionProperty != null)
            enemyDefinitionProperty.objectReferenceValue = null;
        if (runtimeModifiersProperty != null)
            runtimeModifiersProperty.boxedValue = default(EnemyRuntimeModifiers);
        if (enemyCountProperty != null)
            enemyCountProperty.intValue = 5;
        if (spawnCadenceProperty != null)
            spawnCadenceProperty.floatValue = 0.5f;
        if (spawnOffsetProperty != null)
            spawnOffsetProperty.vector3Value = Vector3.zero;
        if (advanceModeProperty != null)
            advanceModeProperty.enumValueIndex = (int)WaveAdvanceMode.FixedInterval;
        if (advanceDelayProperty != null)
            advanceDelayProperty.floatValue = 1f;

        if (spawnNodesProperty != null)
        {
            spawnNodesProperty.ClearArray();
            if (spawnCoords.Length > 0)
            {
                spawnNodesProperty.InsertArrayElementAtIndex(0);
                spawnNodesProperty.GetArrayElementAtIndex(0).vector2IntValue = spawnCoords[0];
            }
        }
    }
    #endregion
}
