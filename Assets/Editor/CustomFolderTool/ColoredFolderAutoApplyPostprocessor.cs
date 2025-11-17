using UnityEngine;
using UnityEditor;
using System.IO;

// auto-propagation when new folders appear
public class ColoredFolderAutoApplyPostprocessor : AssetPostprocessor
{
    // Unity calls this whenever assets change
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            if (!AssetDatabase.IsValidFolder(assetPath))
                continue; // ignore files

            string parentDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (string.IsNullOrEmpty(parentDir) || parentDir == "Assets")
                continue; // root folder itself → skip

            string rootName = ExtractRootName(assetPath);

            // important: load only if already exists (no auto-create here)
            ColoredFolderSettings settings = LoadSettingsForRoot(rootName);
            if (settings == null || !settings.autoInheritColors)
                continue; // no settings or feature disabled

            // read parent color
            Color parentColor = settings.GetColorForFolder(parentDir);
            if (parentColor == Color.clear)
                continue; // parent is not colored

            // get parent's mode too
            var parentMode = settings.GetModeForFolder(parentDir);

            // assign same values to the new folder
            settings.SetFolderData(assetPath, parentColor, parentMode);

            AssetDatabase.SaveAssets(); // persist result
        }
    }

    private static string ExtractRootName(string path)
    {
        string[] split = path.Split('/');
        if (split.Length >= 2 && split[0] == "Assets")
            return split[1];
        return "Assets"; // fallback
    }

    private static ColoredFolderSettings LoadSettingsForRoot(string rootName)
    {
        // load-only variant (no creation)
        string assetPath = $"Assets/Editor/ColoredFolders/ColoredFolderSettings_{rootName}.asset";
        return AssetDatabase.LoadAssetAtPath<ColoredFolderSettings>(assetPath);
    }
}
