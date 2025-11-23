public static class AppUtils
{
    /// <summary>
    /// Quits the application if built; stops Play Mode if running inside the Editor.
    /// </summary>
    public static void Quit()
    {
#if UNITY_EDITOR
        // Stop Play Mode when testing in the Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit the application when built
        UnityEngine.Application.Quit();
#endif
    }

    /// <summary>
    /// Legacy quit wrapper preserved for backward compatibility.
    /// </summary>
    public static void QuitGame()
    {
        Quit();
    }
}
