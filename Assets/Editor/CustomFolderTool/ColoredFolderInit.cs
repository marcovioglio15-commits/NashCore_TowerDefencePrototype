using UnityEditor;

// ensures the ProjectView drawer is always active
[InitializeOnLoad]
public static class ColoredFolderInit
{
    static ColoredFolderInit()
    {
        // remove duplicates – safe-guard
        EditorApplication.projectWindowItemOnGUI -= ColoredFoldersWindow.OnProjectItemGUI_Access;

        // add drawer once on editor startup
        EditorApplication.projectWindowItemOnGUI += ColoredFoldersWindow.OnProjectItemGUI_Access;
    }
}
