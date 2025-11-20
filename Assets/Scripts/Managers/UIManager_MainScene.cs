using System.Collections.Generic;
using Managers.UI;
using Player.Inventory;
using Scriptables.Turrets;
using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("Camera used to project turret positions while drawing hold indicators.")][SerializeField] private Camera worldSpaceCamera;
    [Tooltip("Layer hosting the hold indicator widget.")][SerializeField] private RectTransform worldIndicatorLayer;
    [Tooltip("Prefab used to render hold progress above turrets.")][SerializeField] private Image holdIndicatorPrefab;
    [Tooltip("World offset applied when positioning the hold indicator over turrets.")][SerializeField] private float holdIndicatorHeightOffset = 1.75f;
    #endregion

    #region Runtime
    private readonly List<BuildableIconView> activeIcons = new List<BuildableIconView>();
    private bool dragActive;
    private Image activeHoldIndicator;
    private RectTransform activeHoldRect;
    private Transform activeHoldTarget;
    private bool holdIndicatorActive;
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
        if (buildablesInventory != null)
            buildablesInventory.RequestCatalogBroadcast();

        HideDragPreview();
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
        dragActive = true;
        UpdateDragPreviewSprite(definition, screenPosition);
    }

    /// <summary>
    /// Moves the drag preview while the drag gesture progresses.
    /// </summary>
    private void HandleDragUpdated(Vector2 screenPosition)
    {
        if (!dragActive)
            return;

        UpdateDragPreviewPosition(screenPosition);
    }

    /// <summary>
    /// Hides the drag preview when the gesture completes.
    /// </summary>
    private void HandleDragEnded(Vector2 screenPosition)
    {
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
        if (!dragActive || dragPreviewImage == null)
            return;

        dragPreviewImage.color = preview.HasValidCell ? validDragColor : invalidDragColor;
    }

    /// <summary>
    /// Reports placement results in the console for early debug purposes.
    /// </summary>
    private void HandlePlacementResolved(BuildPlacementResult result)
    {
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

        RectTransform layer = worldIndicatorLayer != null ? worldIndicatorLayer : dragLayer;
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
        RectTransform layer = worldIndicatorLayer != null ? worldIndicatorLayer : dragLayer;
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
