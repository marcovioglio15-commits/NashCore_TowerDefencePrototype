using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

// holds color data for folders inside a root group
public class ColoredFolderSettings : ScriptableObject
{
    #region Variables
    [Tooltip("All folder paths with custom color")]
    public List<string> folderPaths = new List<string>(); // simple list-based db

    [Tooltip("The color for each folder path")]
    public List<Color> folderColors = new List<Color>(); // parallel list

    [Tooltip("Mode for each folder: text/icon/both")]
    public List<ApplyMode> folderApplyModes = new List<ApplyMode>(); // parallel list

    [Tooltip("If true, new subfolders auto-copy parent color")]
    public bool autoInheritColors = true; // auto style propagation
    #endregion

    #region Types
    public enum ApplyMode
    {
        TextOnly,
        IconOnly,
        IconAndText
    }
    #endregion

    #region Methods
    // get color for a folder (or clear if not colored)
    public Color GetColorForFolder(string path)
    {
        int index = folderPaths.IndexOf(path); // simple list lookup
        if (index >= 0 && index < folderColors.Count)
            return folderColors[index];
        return Color.clear; // "no color"
    }

    // get apply mode (or IconAndText as default)
    public ApplyMode GetModeForFolder(string path)
    {
        int index = folderPaths.IndexOf(path);
        if (index >= 0 && index < folderApplyModes.Count)
            return folderApplyModes[index];
        return ApplyMode.IconAndText; // default
    }

    // write/update folder color data
    public void SetFolderData(string path, Color color, ApplyMode mode)
    {
        int index = folderPaths.IndexOf(path);

        if (index >= 0) // already exists → update
        {
            folderColors[index] = color;
            folderApplyModes[index] = mode;
        }
        else // new entry
        {
            folderPaths.Add(path);
            folderColors.Add(color);
            folderApplyModes.Add(mode);
        }

        EditorUtility.SetDirty(this); // mark so it gets saved
    }

    // recursive apply to all subfolders
    public void ApplyToSubfolders(string rootPath, Color color, ApplyMode mode)
    {
        string[] subfolders = AssetDatabase.GetSubFolders(rootPath);
        foreach (string sub in subfolders)
        {
            SetFolderData(sub, color, mode); // write data
            ApplyToSubfolders(sub, color, mode); // continue recursion
        }
    }
    #endregion
}


// main editor tool window
public class ColoredFoldersWindow : EditorWindow
{
    #region Variables

    public static ColoredFolderSettings settings; // active settings file

    private string selectedFolderPath; // folder selected by user
    private string currentRootGroup;   // active root group name

    private Color selectedColor = Color.white; // chosen color
    private ColoredFolderSettings.ApplyMode selectedMode; // apply mode
    private bool applyToSubfolders; // propagate recursively

    private string[] availableRootGroups; // list of existing roots
    private int selectedRootIndex; // dropdown index

    #endregion

    #region Menu

    [MenuItem("Tools/Colored Folders")]
    public static void ShowWindow()
    {
        GetWindow<ColoredFoldersWindow>("Colored Folders");
    }

    // validate right-click menu
    [MenuItem("Assets/Set Folder Color...", true)]
    private static bool ValidateSetFolderColor() =>
        Selection.activeObject != null &&
        AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));

    // open window from context menu
    [MenuItem("Assets/Set Folder Color...", false, 2000)]
    private static void OpenFromContextMenu()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        ShowWindow();
        var window = (ColoredFoldersWindow)GetWindow(typeof(ColoredFoldersWindow));
        window.SetSelectedFolder(path);
    }

    #endregion

    #region Utility

    // extract root name ("Assets/X/...")
    private static string ExtractRootName(string path)
    {
        string[] split = path.Split('/');
        if (split.Length >= 2 && split[0] == "Assets")
            return split[1];
        return "Assets"; // fallback when no sub-root
    }

    // load or create settings for a root group (used in editor window only)
    private static ColoredFolderSettings LoadOrCreateSettingsForRoot(string rootName)
    {
        string folderPath = "Assets/Editor/ColoredFolders";
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath); // ensure dir exists

        string assetPath = $"{folderPath}/ColoredFolderSettings_{rootName}.asset";

        // load file
        ColoredFolderSettings data = AssetDatabase.LoadAssetAtPath<ColoredFolderSettings>(assetPath);

        // create if missing (only from editor window → intentional)
        if (data == null)
        {
            data = CreateInstance<ColoredFolderSettings>();
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();
        }

        return data;
    }

    // load existing settings WITHOUT creating new ones (used by drawer)
    public static ColoredFolderSettings LoadSettingsIfExists(string rootName)
    {
        string path = $"Assets/Editor/ColoredFolders/ColoredFolderSettings_{rootName}.asset";
        return AssetDatabase.LoadAssetAtPath<ColoredFolderSettings>(path); // null if missing
    }

    // get list of existing roots
    private static string[] GetExistingRoots()
    {
        string folderPath = "Assets/Editor/ColoredFolders";
        if (!Directory.Exists(folderPath))
            return new string[0];

        var roots = new List<string>();
        string[] files = Directory.GetFiles(folderPath, "ColoredFolderSettings_*.asset");

        // extract root name from filename
        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("ColoredFolderSettings_"))
                roots.Add(name.Substring("ColoredFolderSettings_".Length));
        }

        return roots.ToArray();
    }

    #endregion

    #region GUI Logic

    private void SetSelectedFolder(string path)
    {
        selectedFolderPath = path; // store user selection
        currentRootGroup = ExtractRootName(path); // rebuild root name

        settings = LoadOrCreateSettingsForRoot(currentRootGroup); // load/create config

        // preload gui state
        selectedColor = settings.GetColorForFolder(path);
        selectedMode = settings.GetModeForFolder(path);
        applyToSubfolders = false;

        RefreshRootDropdown(); // update list
        Repaint(); // refresh window
    }

    private void RefreshRootDropdown()
    {
        availableRootGroups = GetExistingRoots(); // read all available configs
        selectedRootIndex = Mathf.Max(0, System.Array.IndexOf(availableRootGroups, currentRootGroup));
    }


    private void OnGUI()
    {
        GUILayout.Label("Folder Color Customizer", EditorStyles.boldLabel);

        // ensure root list is available
        if (availableRootGroups == null || availableRootGroups.Length == 0)
            RefreshRootDropdown();

        // draw root selector
        if (availableRootGroups.Length > 0)
        {
            EditorGUILayout.LabelField("Root Group:");
            int newIndex = EditorGUILayout.Popup(selectedRootIndex, availableRootGroups);

            // root changed → reload its settings
            if (newIndex != selectedRootIndex)
            {
                selectedRootIndex = newIndex;
                currentRootGroup = availableRootGroups[newIndex];
                settings = LoadOrCreateSettingsForRoot(currentRootGroup);
                Repaint();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No root groups found. Pick a folder to start.", MessageType.Info);
        }

        GUILayout.Space(6);

        // toggle auto inherit
        if (settings != null)
        {
            bool newAuto = EditorGUILayout.Toggle("Auto-inherit Colors", settings.autoInheritColors);
            if (newAuto != settings.autoInheritColors)
            {
                settings.autoInheritColors = newAuto;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        GUILayout.Space(6);

        // folder selection
        if (GUILayout.Button("Select Folder..."))
        {
            string path = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                    path = "Assets" + path.Substring(Application.dataPath.Length); // convert to Unity path
                SetSelectedFolder(path);
            }
        }

        // draw folder options
        if (!string.IsNullOrEmpty(selectedFolderPath))
        {
            EditorGUILayout.LabelField("Selected:", selectedFolderPath);
            selectedColor = EditorGUILayout.ColorField("Color:", selectedColor);
            selectedMode = (ColoredFolderSettings.ApplyMode)EditorGUILayout.EnumPopup("Apply To:", selectedMode);
            applyToSubfolders = EditorGUILayout.Toggle("Apply To Subfolders", applyToSubfolders);

            if (GUILayout.Button("Apply"))
            {
                // write data
                settings.SetFolderData(selectedFolderPath, selectedColor, selectedMode);

                if (applyToSubfolders)
                    settings.ApplyToSubfolders(selectedFolderPath, selectedColor, selectedMode);

                AssetDatabase.SaveAssets(); // persist to disk
                Repaint(); // update window
                EditorApplication.RepaintProjectWindow(); // refresh project view
            }
        }

        GUILayout.Space(10);

        // clear current group only
        if (settings != null && GUILayout.Button("Clear All Colors (This Group Only)"))
        {
            settings.folderPaths.Clear();
            settings.folderColors.Clear();
            settings.folderApplyModes.Clear();
            AssetDatabase.SaveAssets();
            EditorApplication.RepaintProjectWindow();
        }
    }

    #endregion

    #region Drawer

    // public-exposed wrapper (used by initializer)
    public static void OnProjectItemGUI_Access(string guid, Rect rect)
    {
        OnProjectItemGUI(guid, rect); // small indirection
    }

    // draws colored backgrounds/icons in Project Window
    private static void OnProjectItemGUI(string guid, Rect rect)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            return; // ignore non-folders

        string rootName = ExtractRootName(path);

        // load settings only if exists → no unwanted asset creation
        ColoredFolderSettings rootSettings = LoadSettingsIfExists(rootName);
        if (rootSettings == null)
            return; // no settings for this root → no colors

        // get color mapped to folder
        Color color = rootSettings.GetColorForFolder(path);
        if (color == Color.clear)
            return; // folder not colored

        // get how color must be applied
        var mode = rootSettings.GetModeForFolder(path);

        // draw small colored square over the icon
        if (mode == ColoredFolderSettings.ApplyMode.IconOnly ||
            mode == ColoredFolderSettings.ApplyMode.IconAndText)
        {
            Rect iconRect = rect;
            iconRect.width = 16f;
            iconRect.height = 16f;
            iconRect.y += 1f;

            EditorGUI.DrawRect(iconRect, new Color(color.r, color.g, color.b, 0.35f));
        }

        // overlay color behind text area
        if (mode == ColoredFolderSettings.ApplyMode.TextOnly ||
            mode == ColoredFolderSettings.ApplyMode.IconAndText)
        {
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.25f));
        }
    }

    #endregion
}
