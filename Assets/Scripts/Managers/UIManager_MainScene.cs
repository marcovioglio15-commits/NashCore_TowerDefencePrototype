using System.Collections;
using System.Collections.Generic;
using Managers.UI;
using Player.Inventory;
using Scriptables.Turrets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Coordinates the build UI by reacting to catalog, preview and drag events exposed through the EventsManager.
/// </summary>
public class UIManager_MainScene : Singleton<UIManager_MainScene>
{
    #region Variables And Properties
    #region Serialized Fields
    [Header("Inventory")]
    [Tooltip("Inventory provider used to refresh the buildable catalog when the UI becomes active.")]
    [SerializeField] private BuildablesInventory buildablesInventory;

    [Header("Build Bar")]
    [Tooltip("Horizontal layout container hosting turret icons.")]
    [SerializeField] private RectTransform buildablesContainer;
    [Tooltip("Prefab instantiated for each turret entry in the build bar.")]
    [SerializeField] private BuildableIconView iconPrefab;

    [Header("Drag Preview")]
    [Tooltip("Canvas hosting the build UI, required to convert screen positions into anchored coordinates.")]
    [SerializeField] private Canvas uiCanvas;
    [Tooltip("Layer used to display the drag preview sprite.")]
    [SerializeField] private RectTransform dragLayer;
    [Tooltip("Image shown while dragging to mirror the selected turret.")]
    [SerializeField] private Image dragPreviewImage;
    [Tooltip("Color applied to the drag preview when a cell is valid.")]
    [SerializeField] private Color validDragColor = Color.white;
    [Tooltip("Color applied to the drag preview when no valid cell is available.")]
    [SerializeField] private Color invalidDragColor = new Color(1f, 0.45f, 0.45f, 0.95f);

    [Header("Turret Feedback")]
    [Tooltip("Camera used to project turret positions while drawing hold indicators.")]
    [SerializeField] private Camera worldSpaceCamera;
    [Tooltip("Layer hosting the hold indicator widget.")]
    [SerializeField] private RectTransform holdIndicatorLayer;
    [Tooltip("Prefab used to render hold progress above turrets.")]
    [SerializeField] private Image holdIndicatorPrefab;
    [Tooltip("World offset applied when positioning the hold indicator over turrets.")]
    [SerializeField] private float holdIndicatorHeightOffset = 1.75f;

    [Header("Free Aim UI Root")]
    [Tooltip("Root object hosting the build bar to hide during free-aim control.")] 
    [SerializeField] private GameObject buildBarRoot;
    [Tooltip("Root object displayed instead of the build bar while in free-aim.")] 
    [SerializeField] private GameObject freeAimRoot;

    [Header("Free Aim UI Exit")]
    [Tooltip("Image used to visualize the hold progress for exiting free-aim.")] 
    [SerializeField] private Image freeAimExitHoldImage;
    [Tooltip("Seconds required to hold the exit control before leaving free-aim.")]
    [SerializeField] private float freeAimExitHoldSeconds = 1.35f;
    [Tooltip("Alpha applied to the free-aim exit control when idle.")]
    [SerializeField, Range(0f, 1f)] private float freeAimExitIdleAlpha = 0.45f;
    [Tooltip("Alpha applied to the free-aim exit control while actively pressed.")] 
    [SerializeField, Range(0f, 1f)] private float freeAimExitActiveAlpha = 1f;
    [Tooltip("Canvas group controlling the exit button interactivity.")] 
    [SerializeField] private CanvasGroup freeAimExitCanvasGroup;

    [Header("Free Aim UI Reticle")]
    [Tooltip("Reticle displayed while in free-aim; taps inside this rect trigger manual fire.")]
    [SerializeField] private RectTransform freeAimReticle;
    [Tooltip("Canvas group used to fade the free-aim reticle.")] 
    [SerializeField] private CanvasGroup freeAimReticleCanvasGroup;

    [Header("Navigation")]
    [Tooltip("Scene name used when returning to the main menu.")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Pause UI")]
    [Tooltip("Button that opens the pause panel.")]
    [SerializeField] private Button pauseButton;
    [Tooltip("Panel displayed when the game is paused.")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Button that resumes gameplay from the pause panel.")]
    [SerializeField] private Button resumeButton;
    [Tooltip("Button that returns to the main menu from the pause panel.")]
    [SerializeField] private Button pausePanelMainMenuButton;

    [Header("Ending UI")]
    [Tooltip("Panel shown when the match ends.")]
    [SerializeField] private GameObject endingPanel;
    [Tooltip("Banner label summarizing the final result.")]
    [SerializeField] private TextMeshProUGUI resultLabel;
    [Tooltip("Color applied to the result label on victory.")]
    [SerializeField] private Color victoryResultColor = new Color(0.34f, 0.83f, 0.58f, 1f);
    [Tooltip("Color applied to the result label on defeat.")]
    [SerializeField] private Color defeatResultColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [Tooltip("Label listing the number of defeated hordes.")]
    [SerializeField] private TextMeshProUGUI defeatedHordesLabel;
    [Tooltip("Button that returns to the main menu from the ending panel.")]
    [SerializeField] private Button endingMainMenuButton;
    [Tooltip("Button that closes the application from the ending panel.")]
    [SerializeField] private Button endingQuitButton;
    [Tooltip("Button that loads the next configured level after a victory.")]
    [SerializeField] private Button nextLevelButton;

    [Header("Level Flow")]
    [Tooltip("Ordered scene names used to resolve the next level after a victory.")]
    [SerializeField] private List<string> levelSceneOrder = new List<string>();

    [Header("Phase Flow")]
    [Tooltip("Button that toggles between build and combat phases.")]
    [SerializeField] private Button phaseToggleButton;
    [Tooltip("Canvas group controlling the visibility of the phase banner.")]
    [SerializeField] private CanvasGroup phaseBannerCanvasGroup;
    [Tooltip("Text element displaying the active phase name.")]
    [SerializeField] private TextMeshProUGUI phaseBannerLabel;
    [Tooltip("Seconds spent fading the phase banner in after a phase change.")]
    [SerializeField] private float phaseBannerFadeInSeconds = 0.3f;
    [Tooltip("Seconds the phase banner stays fully visible after fading in.")]
    [SerializeField] private float phaseBannerHoldSeconds = 1.6f;
    [Tooltip("Seconds spent fading the phase banner out when auto-hide is enabled.")]
    [SerializeField] private float phaseBannerFadeOutSeconds = 0.45f;
    [Tooltip("Keeps the phase banner visible at all times, updating only its label.")]
    [SerializeField] private bool phaseBannerAlwaysVisible;
    [Tooltip("Text displayed when entering the build phase.")]
    [SerializeField] private string buildingPhaseLabel = "Building Phase";
    [Tooltip("Text displayed when entering the defence phase.")]
    [SerializeField] private string combatPhaseLabel = "Defence Phase";

    [Tooltip("Image with Fill amount used to visualize current player health.")]
    [Header("Player Health")]
    [SerializeField] private Image playerHealthFillImage;

    [Tooltip("Label displaying the player's current Scrap balance.")]
    [Header("Economy")]
    [SerializeField] private TextMeshProUGUI ScrapLabel;
    [Tooltip("Color applied to the Scrap label when funds are insufficient.")]
    [Header("Economy Feedback")]
    [SerializeField] private Color ScrapInsufficientColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [Tooltip("Seconds spent pulsing the Scrap label on insufficient funds.")]
    [SerializeField] private float ScrapInsufficientPulseSeconds = 0.4f;
    [Tooltip("Curve controlling the intensity of the Scrap pulse feedback.")]
    [SerializeField] private AnimationCurve ScrapInsufficientCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    #endregion

    #region Runtime
    private readonly List<BuildableIconView> activeIcons = new List<BuildableIconView>();
    private bool dragActive;
    private Image activeHoldIndicator;
    private RectTransform activeHoldRect;
    private Transform activeHoldTarget;
    private bool holdIndicatorActive;
    private bool freeAimActive;
    private bool exitHoldActive;
    private float exitHoldTimer;
    private bool controlsArmed;
    private bool freeAimUiVisible;
    private Coroutine phaseBannerRoutine;
    private bool buildUiVisible = true;
    private Coroutine ScrapPulseCoroutine;
    private Color ScrapLabelBaseColor = Color.white;
    private bool pauseActive;
    private bool endingDisplayed;
    private int defeatedHordesCount;
    private const string ResultPrefix = "RESULT : ";
    private const string HordesPrefix = "HORDES DEFEATED : ";
    private string cachedNextLevelSceneName;
    #endregion
    #endregion

    #region Methods
    #region Unity
    /// <summary>
    /// Subscribes to catalog, drag and placement events as soon as the manager is enabled.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        EventsManager.BuildablesCatalogChanged += HandleCatalogChanged;
        EventsManager.BuildableDragBegan += HandleDragBegan;
        EventsManager.BuildableDragUpdated += HandleDragUpdated;
        EventsManager.BuildableDragEnded += HandleDragEnded;
        EventsManager.BuildablePreviewUpdated += HandlePreviewUpdated;
        EventsManager.BuildablePlacementResolved += HandlePlacementResolved;
        EventsManager.TurretFreeAimStarted += HandleFreeAimStarted;
        EventsManager.TurretFreeAimEnded += HandleFreeAimEnded;
        EventsManager.GamePhaseChanged += HandleGamePhaseChanged;
        EventsManager.PlayerScrapChanged += HandlePlayerScrapChanged;
        EventsManager.PlayerScrapInsufficient += HandlePlayerScrapInsufficient;
        EventsManager.GameVictoryAchieved += HandleGameVictoryAchieved;
        EventsManager.GameDefeatTriggered += HandleGameDefeatTriggered;
        EventsManager.IncreaseCompletedHordesCounter += HandleCompletedHordesIncreased;
        EventsManager.PlayerHealthChanged += HandlePlayerHealthChanged;
        EventsManager.PlayerDeath += HandlePlayerDeath;
        if (buildablesInventory != null)
            buildablesInventory.RequestCatalogBroadcast();

        HideDragPreview();
        HideFreeAimUi();
        HideReticle();
        EnsureExitRaycast();
        controlsArmed = false;
        freeAimUiVisible = false;
        EnsureExitHandler();
        AttachPhaseButtonListener();
        SyncPhaseUiState();
        CacheScrapLabelColor();
        SyncScrapLabel();
        AttachPauseButtonListeners();
        AttachEndingButtonListeners();
        HidePausePanel();
        HideEndingPanel();
        endingDisplayed = false;
        pauseActive = false;
        defeatedHordesCount = 0;
        cachedNextLevelSceneName = string.Empty;
        UpdateDefeatedHordesLabel();
        ApplyPlayerHealthFill(1f, 1f);
    }

    /// <summary>
    /// Cleans up subscriptions when the manager is disabled.
    /// </summary>
    private void OnDisable()
    {
        EventsManager.BuildablesCatalogChanged -= HandleCatalogChanged;
        EventsManager.BuildableDragBegan -= HandleDragBegan;
        EventsManager.BuildableDragUpdated -= HandleDragUpdated;
        EventsManager.BuildableDragEnded -= HandleDragEnded;
        EventsManager.BuildablePreviewUpdated -= HandlePreviewUpdated;
        EventsManager.BuildablePlacementResolved -= HandlePlacementResolved;
        EventsManager.TurretFreeAimStarted -= HandleFreeAimStarted;
        EventsManager.TurretFreeAimEnded -= HandleFreeAimEnded;
        EventsManager.GamePhaseChanged -= HandleGamePhaseChanged;
        EventsManager.PlayerScrapChanged -= HandlePlayerScrapChanged;
        EventsManager.PlayerScrapInsufficient -= HandlePlayerScrapInsufficient;
        EventsManager.GameVictoryAchieved -= HandleGameVictoryAchieved;
        EventsManager.GameDefeatTriggered -= HandleGameDefeatTriggered;
        EventsManager.IncreaseCompletedHordesCounter -= HandleCompletedHordesIncreased;
        EventsManager.PlayerHealthChanged -= HandlePlayerHealthChanged;
        EventsManager.PlayerDeath -= HandlePlayerDeath;
        DetachPhaseButtonListener();
        HideFreeAimUi();
        HideReticle();
        controlsArmed = false;
        freeAimUiVisible = false;
        if (phaseBannerRoutine != null)
        {
            StopCoroutine(phaseBannerRoutine);
            phaseBannerRoutine = null;
        }

        StopScrapPulse();
        RestoreScrapLabelColor();
        DetachPauseButtonListeners();
        DetachEndingButtonListeners();
        HidePausePanel();
        HideEndingPanel();
        ApplyPauseState(false);
    }

    /// <summary>
    /// Updates free-aim exit hold timers.
    /// </summary>
    private void Update()
    {
        UpdateExitHoldProgress();
    }

    /// <summary>
    /// Keeps turret hold indicators aligned with their world targets.
    /// </summary>
    private void LateUpdate()
    {
        if (!holdIndicatorActive)
            return;

        UpdateHoldIndicatorPosition();
    }
    #endregion

    #region Free Aim UI
    /// <summary>
    /// Activates free-aim UI when the player possesses a turret.
    /// </summary>
    private void HandleFreeAimStarted(PooledTurret turret)
    {
        freeAimActive = true;
        CancelFreeAimExitHold();
        HideTurretHoldIndicator();
        ShowFreeAimUi();
        HideReticle();
        controlsArmed = false;
    }

    /// <summary>
    /// Restores build UI once free-aim concludes.
    /// </summary>
    private void HandleFreeAimEnded(PooledTurret turret)
    {
        freeAimActive = false;
        CancelFreeAimExitHold();
        HideFreeAimUi();
        HideReticle();
    }

    /// <summary>
    /// Hides the build bar and reveals the free-aim controls.
    /// </summary>
    private void ShowFreeAimUi()
    {
        if (buildBarRoot != null && buildBarRoot.activeSelf)
            buildBarRoot.SetActive(false);

        if (freeAimRoot != null && !freeAimRoot.activeSelf)
            freeAimRoot.SetActive(true);

        UpdateExitHoldFill(1f);
        SetExitHoldAlpha(freeAimExitIdleAlpha);
        SetExitInteractable(controlsArmed);
        freeAimUiVisible = true;
    }

    /// <summary>
    /// Restores the build bar and hides free-aim specific controls.
    /// </summary>
    private void HideFreeAimUi()
    {
        if (buildBarRoot != null)
        {
            if (buildUiVisible && !buildBarRoot.activeSelf)
                buildBarRoot.SetActive(true);
            else if (!buildUiVisible && buildBarRoot.activeSelf)
                buildBarRoot.SetActive(false);
        }

        if (freeAimRoot != null && freeAimRoot.activeSelf)
            freeAimRoot.SetActive(false);

        freeAimActive = false;
        CancelFreeAimExitHold();
        controlsArmed = false;
        freeAimUiVisible = false;
    }

    /// <summary>
    /// Starts the hold countdown used to exit free-aim.
    /// </summary>
    public void BeginFreeAimExitHold()
    {
        if (!freeAimActive)
            return;

        if (!controlsArmed)
            return;

        exitHoldActive = true;
        exitHoldTimer = 0f;
        UpdateExitHoldFill(0f);
        SetExitHoldAlpha(freeAimExitActiveAlpha);
    }

    /// <summary>
    /// Cancels the hold countdown displayed on the exit control.
    /// </summary>
    public void CancelFreeAimExitHold()
    {
        exitHoldActive = false;
        exitHoldTimer = 0f;
        UpdateExitHoldFill(1f);
        SetExitHoldAlpha(freeAimExitIdleAlpha);
    }

    /// <summary>
    /// Advances the exit hold countdown when the control is pressed.
    /// </summary>
    private void UpdateExitHoldProgress()
    {
        if (!exitHoldActive)
            return;

        float duration = Mathf.Max(0.01f, freeAimExitHoldSeconds);
        exitHoldTimer += Time.unscaledDeltaTime;
        float normalized = Mathf.Clamp01(exitHoldTimer / duration);
        UpdateExitHoldFill(normalized);

        if (exitHoldTimer >= duration)
        {
            exitHoldActive = false;
            exitHoldTimer = 0f;
            UpdateExitHoldFill(1f);
            EventsManager.InvokeTurretFreeAimExitRequested();
        }
    }

    /// <summary>
    /// Pushes exit hold progress to the dedicated UI image.
    /// </summary>
    private void UpdateExitHoldFill(float normalized)
    {
        if (freeAimExitHoldImage == null)
            return;

        freeAimExitHoldImage.fillAmount = Mathf.Clamp01(normalized);
    }

    /// <summary>
    /// Enables reticle and exit UI once camera settling ends.
    /// </summary>
    public void ArmFreeAimControls()
    {
        if (!freeAimUiVisible)
            ShowFreeAimUi();

        controlsArmed = true;
        SetExitInteractable(true);
        ShowReticle();
    }

    /// <summary>
    /// Returns true if the provided screen point lies within the free-aim reticle.
    /// </summary>
    public bool IsWithinReticle(Vector2 screenPoint)
    {
        if (freeAimReticle == null)
            return false;

        Camera eventCamera = uiCanvas != null && uiCanvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(freeAimReticle, screenPoint, eventCamera);
    }

    /// <summary>
    /// Shows the reticle graphic while in free-aim.
    /// </summary>
    private void ShowReticle()
    {
        if (freeAimReticle != null && !freeAimReticle.gameObject.activeSelf)
            freeAimReticle.gameObject.SetActive(true);

        if (freeAimReticleCanvasGroup != null)
        {
            freeAimReticleCanvasGroup.alpha = 1f;
            freeAimReticleCanvasGroup.blocksRaycasts = true;
            freeAimReticleCanvasGroup.interactable = true;
        }
    }

    /// <summary>
    /// Hides the reticle graphic when leaving free-aim.
    /// </summary>
    private void HideReticle()
    {
        if (freeAimReticleCanvasGroup != null)
        {
            freeAimReticleCanvasGroup.alpha = 0f;
            freeAimReticleCanvasGroup.blocksRaycasts = false;
            freeAimReticleCanvasGroup.interactable = false;
        }

        if (freeAimReticle != null && freeAimReticle.gameObject.activeSelf)
            freeAimReticle.gameObject.SetActive(false);
    }

    /// <summary>
    /// Adjusts the free-aim exit control alpha without affecting raycast blocking.
    /// </summary>
    private void SetExitHoldAlpha(float alpha)
    {
        if (freeAimExitHoldImage == null)
            return;

        Color color = freeAimExitHoldImage.color;
        color.a = Mathf.Clamp01(alpha);
        freeAimExitHoldImage.color = color;
    }

    /// <summary>
    /// Ensures the exit control receives raycasts when visible.
    /// </summary>
    private void EnsureExitRaycast()
    {
        if (freeAimExitHoldImage != null)
            freeAimExitHoldImage.raycastTarget = true;

        SetExitInteractable(false);
    }

    /// <summary>
    /// Toggles raycast and interactable state on the exit control canvas group.
    /// </summary>
    private void SetExitInteractable(bool enabled)
    {
        if (freeAimExitCanvasGroup != null)
        {
            freeAimExitCanvasGroup.blocksRaycasts = enabled;
            freeAimExitCanvasGroup.interactable = enabled;
        }
        else if (freeAimExitHoldImage != null)
        {
            freeAimExitHoldImage.raycastTarget = enabled;
        }
    }

    /// <summary>
    /// Ensures the exit button has a handler bound to this UI manager.
    /// </summary>
    private void EnsureExitHandler()
    {
        if (freeAimExitHoldImage == null)
            return;

        FreeAimExitButtonHandler handler = freeAimExitHoldImage.GetComponent<FreeAimExitButtonHandler>();
        if (handler == null)
            handler = freeAimExitHoldImage.gameObject.AddComponent<FreeAimExitButtonHandler>();

        if (handler != null)
            handler.SetUiManager();
    }
    #endregion

    #region Pause UI
    /// <summary>
    /// Binds the pause, resume and navigation buttons.
    /// </summary>
    private void AttachPauseButtonListeners()
    {
        if (pauseButton != null)
            pauseButton.onClick.AddListener(HandlePausePressed);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(HandleResumePressed);

        if (pausePanelMainMenuButton != null)
            pausePanelMainMenuButton.onClick.AddListener(HandleMainMenuRequested);
    }

    /// <summary>
    /// Removes listeners from pause-related buttons.
    /// </summary>
    private void DetachPauseButtonListeners()
    {
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(HandlePausePressed);

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(HandleResumePressed);

        if (pausePanelMainMenuButton != null)
            pausePanelMainMenuButton.onClick.RemoveListener(HandleMainMenuRequested);
    }

    /// <summary>
    /// Opens the pause overlay.
    /// </summary>
    private void HandlePausePressed()
    {
        if (pauseActive)
            return;

        if (endingDisplayed)
            return;

        ShowPausePanel();
    }

    /// <summary>
    /// Closes the pause overlay.
    /// </summary>
    private void HandleResumePressed()
    {
        if (!pauseActive)
            return;

        HidePausePanel();
    }

    /// <summary>
    /// Displays the pause panel and halts gameplay time.
    /// </summary>
    private void ShowPausePanel()
    {
        if (pausePanel != null && !pausePanel.activeSelf)
            pausePanel.SetActive(true);

        ApplyPauseState(true);
    }

    /// <summary>
    /// Hides the pause panel and restores gameplay time scale.
    /// </summary>
    private void HidePausePanel()
    {
        if (pausePanel != null && pausePanel.activeSelf)
            pausePanel.SetActive(false);

        ApplyPauseState(false);
    }

    /// <summary>
    /// Applies the requested pause state using the GameManager when available.
    /// </summary>
    private void ApplyPauseState(bool shouldPause)
    {
        GameManager manager = GameManager.Instance;
        if (manager != null)
            manager.ForcePause(shouldPause);
        else
            Time.timeScale = shouldPause ? 0f : 1f;

        pauseActive = shouldPause;
    }
    #endregion

    #region Ending UI
    /// <summary>
    /// Binds navigation controls inside the ending panel.
    /// </summary>
    private void AttachEndingButtonListeners()
    {
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(HandleNextLevelRequested);

        if (endingMainMenuButton != null)
            endingMainMenuButton.onClick.AddListener(HandleMainMenuRequested);

        if (endingQuitButton != null)
            endingQuitButton.onClick.AddListener(HandleQuitRequested);
    }

    /// <summary>
    /// Unbinds navigation controls inside the ending panel.
    /// </summary>
    private void DetachEndingButtonListeners()
    {
        if (nextLevelButton != null)
            nextLevelButton.onClick.RemoveListener(HandleNextLevelRequested);

        if (endingMainMenuButton != null)
            endingMainMenuButton.onClick.RemoveListener(HandleMainMenuRequested);

        if (endingQuitButton != null)
            endingQuitButton.onClick.RemoveListener(HandleQuitRequested);
    }

    /// <summary>
    /// Reacts to a global victory event.
    /// </summary>
    private void HandleGameVictoryAchieved()
    {
        ShowEndingPanel(true);
    }

    /// <summary>
    /// Reacts to a global defeat event.
    /// </summary>
    private void HandleGameDefeatTriggered()
    {
        ShowEndingPanel(false);
    }

    /// <summary>
    /// Increments the defeated horde counter.
    /// </summary>
    private void HandleCompletedHordesIncreased()
    {
        defeatedHordesCount++;
        UpdateDefeatedHordesLabel();
    }

    /// <summary>
    /// Presents the ending panel with the provided outcome.
    /// </summary>
    private void ShowEndingPanel(bool victory)
    {
        if (endingDisplayed)
            return;

        if (pauseActive)
            HidePausePanel();

        endingDisplayed = true;
        ApplyPauseState(true);

        if (endingPanel != null && !endingPanel.activeSelf)
            endingPanel.SetActive(true);

        if (pauseButton != null)
            pauseButton.interactable = false;

        UpdateNextLevelButton(victory);
        UpdateResultLabel(victory);
        UpdateDefeatedHordesLabel();
    }

    /// <summary>
    /// Hides the ending panel and restores pause interactions.
    /// </summary>
    private void HideEndingPanel()
    {
        if (endingPanel != null && endingPanel.activeSelf)
            endingPanel.SetActive(false);

        endingDisplayed = false;
        if (pauseButton != null)
            pauseButton.interactable = true;

        UpdateNextLevelButton(false);
        cachedNextLevelSceneName = string.Empty;
    }

    /// <summary>
    /// Updates the result banner text and color.
    /// </summary>
    private void UpdateResultLabel(bool victory)
    {
        if (resultLabel == null)
            return;

        string result = victory ? "VICTORY" : "DEFEAT";
        resultLabel.text = $"{ResultPrefix}{result}";
        resultLabel.color = victory ? victoryResultColor : defeatResultColor;
    }

    /// <summary>
    /// Refreshes the defeated hordes banner.
    /// </summary>
    private void UpdateDefeatedHordesLabel()
    {
        if (defeatedHordesLabel == null)
            return;

        defeatedHordesLabel.text = $"{HordesPrefix}{defeatedHordesCount}";
    }

    /// <summary>
    /// Shows the next-level button only when a valid next scene exists and the player has won.
    /// </summary>
    private void UpdateNextLevelButton(bool victory)
    {
        if (nextLevelButton == null)
            return;

        string nextSceneName = "";
        bool hasNextLevel = victory && TryGetNextLevelScene(out nextSceneName);
        cachedNextLevelSceneName = hasNextLevel ? nextSceneName : string.Empty;

        GameObject buttonObject = nextLevelButton.gameObject;
        if (buttonObject != null && buttonObject.activeSelf != hasNextLevel)
            buttonObject.SetActive(hasNextLevel);
    }
    #endregion

    #region Navigation
    /// <summary>
    /// Loads the next configured level when available.
    /// </summary>
    private void HandleNextLevelRequested()
    {
        if (string.IsNullOrEmpty(cachedNextLevelSceneName))
            return;

        ApplyPauseState(false);
        SceneManager.LoadScene(cachedNextLevelSceneName);
    }

    /// <summary>
    /// Handles navigation back to the main menu scene.
    /// </summary>
    private void HandleMainMenuRequested()
    {
        ApplyPauseState(false);
        LoadMainMenuScene();
    }

    /// <summary>
    /// Loads the configured main menu scene.
    /// </summary>
    private void LoadMainMenuScene()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("Main menu scene name is not configured.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Executes application quit through the platform-safe helper.
    /// </summary>
    private void HandleQuitRequested()
    {
        ApplyPauseState(false);
        AppUtils.Quit();
    }

    /// <summary>
    /// Resolves the next scene in the ordered list relative to the active one.
    /// </summary>
    private bool TryGetNextLevelScene(out string sceneName)
    {
        sceneName = string.Empty;

        if (levelSceneOrder == null || levelSceneOrder.Count == 0)
            return false;

        string currentSceneName = SceneManager.GetActiveScene().name;
        int sceneCount = levelSceneOrder.Count;
        for (int i = 0; i < sceneCount; i++)
        {
            string candidate = levelSceneOrder[i];
            if (string.IsNullOrEmpty(candidate))
                continue;

            if (string.Equals(candidate, currentSceneName, System.StringComparison.Ordinal))
            {
                for (int j = i + 1; j < sceneCount; j++)
                {
                    string nextScene = levelSceneOrder[j];
                    if (!string.IsNullOrEmpty(nextScene))
                    {
                        sceneName = nextScene;
                        return true;
                    }
                }

                return false;
            }
        }

        return false;
    }
    #endregion

    #region Phase Flow UI
    /// <summary>
    /// Reacts to global phase switches by updating UI visibility and feedback.
    /// </summary>
    private void HandleGamePhaseChanged(GamePhase phase)
    {
        ApplyPhaseUiState(phase, true);
        UpdatePhaseToggleInteractable();
    }

    /// <summary>
    /// Ensures build UI and free-aim UI follow the active phase rules.
    /// </summary>
    private void ApplyPhaseUiState(GamePhase phase, bool animateBanner)
    {
        bool building = phase == GamePhase.Building;
        SetBuildUiVisibility(building);
        UpdatePhaseToggleVisibility(building);
        if (building)
        {
            CancelFreeAimExitHold();
            HideFreeAimUi();
            HideReticle();
        }

        if (animateBanner)
            TriggerPhaseBanner(phase);
        else
            ApplyPhaseBannerLabel(phase, phaseBannerAlwaysVisible ? 1f : 0f);
    }

    /// <summary>
    /// Toggles the build bar visibility based on current phase and free-aim state.
    /// </summary>
    private void SetBuildUiVisibility(bool visible)
    {
        buildUiVisible = visible;
        if (!buildUiVisible)
        {
            dragActive = false;
            HideDragPreview();
        }

        if (freeAimActive)
            return;

        if (buildBarRoot != null && buildBarRoot.activeSelf != visible)
            buildBarRoot.SetActive(visible);
    }

    /// <summary>
    /// Updates the phase banner text and optionally animates its visibility.
    /// </summary>
    private void TriggerPhaseBanner(GamePhase phase)
    {
        if (phaseBannerLabel == null || phaseBannerCanvasGroup == null)
            return;

        string label = ResolvePhaseLabel(phase);
        phaseBannerLabel.text = label;

        if (phaseBannerAlwaysVisible)
        {
            ApplyPhaseBannerLabel(phase, 1f);
            return;
        }

        if (phaseBannerRoutine != null)
            StopCoroutine(phaseBannerRoutine);

        phaseBannerRoutine = StartCoroutine(PhaseBannerRoutine());
    }

    /// <summary>
    /// Coroutine handling fade-in, hold, and fade-out of the phase banner.
    /// </summary>
    private IEnumerator PhaseBannerRoutine()
    {
        SetPhaseBannerAlpha(0f);
        float fadeIn = Mathf.Max(0f, phaseBannerFadeInSeconds);
        float hold = Mathf.Max(0f, phaseBannerHoldSeconds);
        float fadeOut = Mathf.Max(0f, phaseBannerFadeOutSeconds);
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = fadeIn > 0f ? Mathf.Clamp01(elapsed / fadeIn) : 1f;
            SetPhaseBannerAlpha(normalized);
            yield return null;
        }

        SetPhaseBannerAlpha(1f);
        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = fadeOut > 0f ? Mathf.Clamp01(elapsed / fadeOut) : 1f;
            SetPhaseBannerAlpha(1f - normalized);
            yield return null;
        }

        SetPhaseBannerAlpha(0f);
        phaseBannerRoutine = null;
    }

    /// <summary>
    /// Applies a specific alpha and label to the banner without animation.
    /// </summary>
    private void ApplyPhaseBannerLabel(GamePhase phase, float alpha)
    {
        if (phaseBannerLabel != null)
            phaseBannerLabel.text = ResolvePhaseLabel(phase);

        SetPhaseBannerAlpha(alpha);
    }

    /// <summary>
    /// Adjusts the banner canvas alpha while blocking interactions.
    /// </summary>
    private void SetPhaseBannerAlpha(float alpha)
    {
        if (phaseBannerCanvasGroup == null)
            return;

        float clamped = Mathf.Clamp01(alpha);
        phaseBannerCanvasGroup.alpha = clamped;
        phaseBannerCanvasGroup.interactable = false;
        phaseBannerCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Provides a human-readable label for the requested phase.
    /// </summary>
    private string ResolvePhaseLabel(GamePhase phase)
    {
        return phase == GamePhase.Building ? buildingPhaseLabel : combatPhaseLabel;
    }

    /// <summary>
    /// Dispatches a phase change request through the global event pipeline.
    /// </summary>
    private void HandlePhaseButtonPressed()
    {
        EventsManager.InvokeGamePhaseAdvanceRequested();
        UpdatePhaseToggleInteractable();
    }

    /// <summary>
    /// Hooks the click listener needed for the change-phase button.
    /// </summary>
    private void AttachPhaseButtonListener()
    {
        if (phaseToggleButton == null)
            return;

        phaseToggleButton.onClick.AddListener(HandlePhaseButtonPressed);
    }

    /// <summary>
    /// Removes the click listener to avoid leaks when disabled.
    /// </summary>
    private void DetachPhaseButtonListener()
    {
        if (phaseToggleButton == null)
            return;

        phaseToggleButton.onClick.RemoveListener(HandlePhaseButtonPressed);
    }

    /// <summary>
    /// Updates the button interactable state based on GameManager constraints.
    /// </summary>
    private void UpdatePhaseToggleInteractable()
    {
        if (phaseToggleButton == null)
            return;

        GameManager manager = GameManager.Instance;
        bool canToggle = manager == null ? true : manager.CanRequestPhaseChange;
        phaseToggleButton.interactable = canToggle;
    }

    /// <summary>
    /// Enables or disables the phase toggle button visuals based on the active phase.
    /// </summary>
    private void UpdatePhaseToggleVisibility(bool buildingPhase)
    {
        if (phaseToggleButton == null)
            return;

        GameObject buttonObject = phaseToggleButton.gameObject;
        if (buttonObject != null && buttonObject.activeSelf != buildingPhase)
            buttonObject.SetActive(buildingPhase);

        if (buildingPhase)
            UpdatePhaseToggleInteractable();
        else
            phaseToggleButton.interactable = false;
    }

    /// <summary>
    /// Aligns the UI state with the active phase when enabling before events fire.
    /// </summary>
    private void SyncPhaseUiState()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
            return;

        ApplyPhaseUiState(manager.CurrentPhase, false);
        UpdatePhaseToggleVisibility(manager.CurrentPhase == GamePhase.Building);
    }
    #endregion

    #region Player Health UI
    /// <summary>
    /// Updates the player health fill when health changes.
    /// </summary>
    private void HandlePlayerHealthChanged(float currentHealth, float maxHealth)
    {
        ApplyPlayerHealthFill(currentHealth, maxHealth);
    }

    /// <summary>
    /// Clears the health fill when the player dies.
    /// </summary>
    private void HandlePlayerDeath()
    {
        ApplyPlayerHealthFill(0f, 1f);
    }

    /// <summary>
    /// Writes the normalized health value into the assigned fill image.
    /// </summary>
    private void ApplyPlayerHealthFill(float currentHealth, float maxHealth)
    {
        if (playerHealthFillImage == null)
            return;

        float safeMax = Mathf.Max(Mathf.Epsilon, maxHealth);
        float normalized = Mathf.Clamp01(currentHealth / safeMax);
        playerHealthFillImage.fillAmount = normalized;
    }
    #endregion

    #region Economy UI
    /// <summary>
    /// Updates the Scrap label when the player's balance changes.
    /// </summary>
    private void HandlePlayerScrapChanged(int Scrap)
    {
        UpdateScrapLabel(Scrap);
    }

    /// <summary>
    /// Plays the insufficient Scrap feedback when a build attempt is blocked.
    /// </summary>
    private void HandlePlayerScrapInsufficient(int currentScrap, int requiredScrap)
    {
        TriggerScrapPulse();
    }

    /// <summary>
    /// Requests the current balance to populate the Scrap label at startup.
    /// </summary>
    private void SyncScrapLabel()
    {
        PlayerResourcesManager resources = PlayerResourcesManager.Instance;
        if (resources == null)
            return;

        UpdateScrapLabel(resources.CurrentScrap);
    }

    /// <summary>
    /// Writes the provided Scrap amount into the HUD label.
    /// </summary>
    private void UpdateScrapLabel(int Scrap)
    {
        if (ScrapLabel == null)
            return;

        ScrapLabel.text = $"CURRENT Scrap : {Scrap}";
    }

    /// <summary>
    /// Stores the baseline Scrap label color for future feedback resets.
    /// </summary>
    private void CacheScrapLabelColor()
    {
        if (ScrapLabel == null)
            return;

        ScrapLabelBaseColor = ScrapLabel.color;
    }

    /// <summary>
    /// Restores the Scrap label to its cached base color.
    /// </summary>
    private void RestoreScrapLabelColor()
    {
        if (ScrapLabel == null)
            return;

        ScrapLabel.color = ScrapLabelBaseColor;
    }

    /// <summary>
    /// Starts or restarts the pulse feedback coroutine on the Scrap label.
    /// </summary>
    private void TriggerScrapPulse()
    {
        if (ScrapLabel == null)
            return;

        if (ScrapPulseCoroutine != null)
            StopCoroutine(ScrapPulseCoroutine);

        ScrapPulseCoroutine = StartCoroutine(ScrapPulseRoutine());
    }

    /// <summary>
    /// Stops any running Scrap pulse feedback.
    /// </summary>
    private void StopScrapPulse()
    {
        if (ScrapPulseCoroutine == null)
            return;

        StopCoroutine(ScrapPulseCoroutine);
        ScrapPulseCoroutine = null;
    }

    /// <summary>
    /// Animates the Scrap label color to highlight insufficient funds.
    /// </summary>
    private IEnumerator ScrapPulseRoutine()
    {
        RestoreScrapLabelColor();
        float duration = Mathf.Max(0.05f, ScrapInsufficientPulseSeconds);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float normalized = duration > 0f ? Mathf.Clamp01(timer / duration) : 1f;
            float curve = ScrapInsufficientCurve != null ? Mathf.Clamp01(ScrapInsufficientCurve.Evaluate(normalized)) : normalized;
            ScrapLabel.color = Color.Lerp(ScrapLabelBaseColor, ScrapInsufficientColor, curve);
            yield return null;
        }

        RestoreScrapLabelColor();
        ScrapPulseCoroutine = null;
    }
    #endregion

    #region Catalog
    /// <summary>
    /// Rebuilds the build bar when the catalog changes.
    /// </summary>
    private void HandleCatalogChanged(IReadOnlyList<TurretClassDefinition> catalog)
    {
        if (catalog == null || buildablesContainer == null || iconPrefab == null)
            return;

        for (int i = 0; i < activeIcons.Count; i++)
        {
            BuildableIconView icon = activeIcons[i];
            if (icon != null)
                Destroy(icon.gameObject);
        }

        activeIcons.Clear();

        for (int i = 0; i < catalog.Count; i++)
        {
            TurretClassDefinition definition = catalog[i];
            if (definition == null)
                continue;

            BuildableIconView instance = Instantiate(iconPrefab, buildablesContainer);
            instance.Bind(definition);
            activeIcons.Add(instance);
        }
    }
    #endregion

    #region Drag Handling
    /// <summary>
    /// Displays the drag preview when the user starts dragging a turret icon.
    /// </summary>
    private void HandleDragBegan(TurretClassDefinition definition, Vector2 screenPosition)
    {
        if (!buildUiVisible)
            return;

        dragActive = true;
        UpdateDragPreviewSprite(definition, screenPosition);
    }

    /// <summary>
    /// Moves the drag preview while the drag gesture progresses.
    /// </summary>
    private void HandleDragUpdated(Vector2 screenPosition)
    {
        if (!buildUiVisible)
            return;

        if (!dragActive)
            return;

        UpdateDragPreviewPosition(screenPosition);
    }

    /// <summary>
    /// Hides the drag preview when the gesture completes.
    /// </summary>
    private void HandleDragEnded(Vector2 screenPosition)
    {
        if (!buildUiVisible)
            return;

        if (!dragActive)
            return;

        dragActive = false;
        HideDragPreview();
    }

    /// <summary>
    /// Adjusts drag tint based on preview validation results.
    /// </summary>
    private void HandlePreviewUpdated(BuildPreviewData preview)
    {
        if (!buildUiVisible)
            return;

        if (!dragActive || dragPreviewImage == null)
            return;

        dragPreviewImage.color = preview.HasValidCell ? validDragColor : invalidDragColor;
    }

    /// <summary>
    /// Reports placement results in the console for early debug purposes.
    /// </summary>
    private void HandlePlacementResolved(BuildPlacementResult result)
    {
        if (!buildUiVisible)
            return;

        if (!result.Success)
        {
            Debug.LogWarning($"Turret placement failed: {result.FailureReason}", this);
            return;
        }

        Debug.Log($"Placed turret {result.Definition.DisplayName} at cell {result.Cell}", this);
    }
    #endregion

    #region Drag Helpers
    /// <summary>
    /// Updates drag image sprite and resets tint when a new drag begins.
    /// </summary>
    private void UpdateDragPreviewSprite(TurretClassDefinition definition, Vector2 screenPosition)
    {
        if (dragPreviewImage == null)
            return;

        dragPreviewImage.sprite = definition != null ? definition.Icon : null;
        dragPreviewImage.color = validDragColor;
        dragPreviewImage.enabled = dragPreviewImage.sprite != null;
        UpdateDragPreviewPosition(screenPosition);
    }

    /// <summary>
    /// Converts a screen position into anchored coordinates relative to the drag layer.
    /// </summary>
    private void UpdateDragPreviewPosition(Vector2 screenPosition)
    {
        if (dragPreviewImage == null || dragLayer == null)
            return;

        Camera eventCamera = uiCanvas != null && uiCanvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCanvas.worldCamera : null;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, screenPosition, eventCamera, out local))
            return;

        dragPreviewImage.rectTransform.anchoredPosition = local;
    }

    /// <summary>
    /// Hides the drag preview image.
    /// </summary>
    private void HideDragPreview()
    {
        if (dragPreviewImage == null)
            return;

        dragPreviewImage.enabled = false;
    }
    #endregion

    #region Turret Hold Feedback
    /// <summary>
    /// Updates or instantiates the turret hold indicator using the provided progress value.
    /// </summary>
    public void UpdateTurretHoldIndicator(Transform target, float normalizedProgress)
    {
        if (target == null)
        {
            HideTurretHoldIndicator();
            return;
        }

        EnsureHoldIndicatorInstance();
        if (activeHoldIndicator == null)
            return;

        activeHoldTarget = target;
        activeHoldIndicator.fillAmount = Mathf.Clamp01(normalizedProgress);
        holdIndicatorActive = true;
        if (!activeHoldIndicator.enabled)
            activeHoldIndicator.enabled = true;

        UpdateHoldIndicatorPosition();
    }

    /// <summary>
    /// Hides the turret hold indicator when no hold gesture is active.
    /// </summary>
    public void HideTurretHoldIndicator()
    {
        holdIndicatorActive = false;
        activeHoldTarget = null;
        if (activeHoldIndicator == null)
            return;

        activeHoldIndicator.enabled = false;
    }

    /// <summary>
    /// Ensures the hold indicator instance exists in the configured overlay.
    /// </summary>
    private void EnsureHoldIndicatorInstance()
    {
        if (activeHoldIndicator != null)
            return;

        if (holdIndicatorPrefab == null)
            return;

        RectTransform layer = holdIndicatorLayer != null ? holdIndicatorLayer : dragLayer;
        if (layer == null)
            return;

        activeHoldIndicator = Instantiate(holdIndicatorPrefab, layer);
        activeHoldRect = activeHoldIndicator.rectTransform;
        activeHoldIndicator.enabled = false;
    }

    /// <summary>
    /// Converts turret world positions to overlay coordinates for the hold indicator.
    /// </summary>
    private void UpdateHoldIndicatorPosition()
    {
        if (activeHoldRect == null || activeHoldTarget == null)
            return;

        Camera projectionCamera = worldSpaceCamera != null ? worldSpaceCamera : Camera.main;
        if (projectionCamera == null)
            return;

        Vector3 worldPosition = activeHoldTarget.position + Vector3.up * holdIndicatorHeightOffset;
        Vector3 screenPoint = projectionCamera.WorldToScreenPoint(worldPosition);
        RectTransform layer = holdIndicatorLayer != null ? holdIndicatorLayer : dragLayer;
        if (layer == null)
            return;

        Camera canvasCamera = uiCanvas != null && uiCanvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCanvas.worldCamera : null;
        Vector2 anchored;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPoint, canvasCamera, out anchored))
            return;

        activeHoldRect.anchoredPosition = anchored;
    }
    #endregion

    #endregion
}
