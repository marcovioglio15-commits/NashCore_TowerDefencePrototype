using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateFolders : MonoBehaviour
{
    [MenuItem("Tools/Folder Creator")]
    private static void FolderCreator()
    {
        string[] folders_assets = new string[]
        {
            "2D",
            "3D",
            "Prefabs",
            "ScriptableObjects",
            "Scripts",
            "Scenes",
            "Audio"
        };

        string[] subfolders_2D = new string[]
        {
            "Sprites",
            "UI",
        };

        string[] subfolders_2D_UI = new string[]
        {
            "Fonts",
        };

        string[] subfolders_3D = new string[]
        {
            "Textures",
            "Static Meshes",
            "VFX",
            "Materials"
        };

        string[] subfolders_3D_VFX = new string[]
{
            "VFX_Graphs",
            "VFX_Materials",
            "VFX_Sprites"
};

        FoldersCreation(folders_assets, "Assets");
        FoldersCreation(subfolders_2D, "Assets/2D");
        FoldersCreation(subfolders_2D_UI, "Assets/2D/UI");
        FoldersCreation(subfolders_3D, "Assets/3D");
        FoldersCreation(subfolders_3D_VFX, "Assets/3D/VFX");

    }

    private static void FoldersCreation(string[] folderNames, string path)
    {
        foreach (string folderName in folderNames)
        {
            string fullPath = Path.Combine(path, folderName);
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(fullPath), Path.GetFileName(fullPath));
                Debug.Log($"Created folder: {fullPath}");
            }

        }
    }
}

      


      


