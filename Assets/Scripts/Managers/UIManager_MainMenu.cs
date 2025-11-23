using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Coordinates the main menu interactions for starting or exiting the game.
/// </summary>
public class UIManager_MainMenu : MonoBehaviour
{
    #region Variables And Properties
    #region Serialized Fields
    [Header("Navigation")]
    [Tooltip("Scene name to load when starting the game from the main menu.")]
    [SerializeField] private string mainSceneName = "TS_TowersPositioning";
    [Tooltip("Button that loads the configured main scene.")]
    [SerializeField] private Button playButton;
    [Tooltip("Button that quits the application.")]
    [SerializeField] private Button quitButton;
    #endregion
    #endregion

    #region Methods
    #region Unity
    /// <summary>
    /// Registers main menu button callbacks.
    /// </summary>
    private void OnEnable()
    {
        AttachButtonListeners();
    }

    /// <summary>
    /// Unregisters main menu button callbacks.
    /// </summary>
    private void OnDisable()
    {
        DetachButtonListeners();
    }
    #endregion

    #region Setup
    /// <summary>
    /// Binds menu buttons to their handlers.
    /// </summary>
    private void AttachButtonListeners()
    {
        if (playButton != null)
            playButton.onClick.AddListener(HandlePlayPressed);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitPressed);
    }

    /// <summary>
    /// Removes menu button handlers.
    /// </summary>
    private void DetachButtonListeners()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(HandlePlayPressed);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuitPressed);
    }
    #endregion

    #region Actions
    /// <summary>
    /// Loads the configured main scene.
    /// </summary>
    private void HandlePlayPressed()
    {
        if (string.IsNullOrEmpty(mainSceneName))
        {
            Debug.LogError("Main scene name is not configured for the main menu.", this);
            return;
        }

        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// Quits the application using a platform-safe helper.
    /// </summary>
    private void HandleQuitPressed()
    {
        AppUtils.Quit();
    }
    #endregion
    #endregion
}
