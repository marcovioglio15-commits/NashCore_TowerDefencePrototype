using UnityEngine;

/// <summary>
/// Fits the camera viewport to the reference play area defined in PlayAreaDefinition.
/// Preserves consistent gameplay visibility across devices using pillarbox or letterbox.
/// Optimized to avoid unnecessary computations and supports optional runtime recalculation.
/// </summary>
[DefaultExecutionOrder(-500)]
public class PlayAreaCameraFitter : MonoBehaviour
{
    #region Variables And Properties

    #region References
    [Tooltip("Reference asset that defines the target virtual play area and fitting behavior.")]
    public PlayAreaSO playAreaDefinition; 

    [Tooltip("Camera whose viewport will be adjusted. If null, the main camera will be assigned automatically.")]
    public Camera targetCamera;
    #endregion

    #region Settings
    [Tooltip("If enabled, prints detailed information about fitting operations.")]
    public bool enableDebugLog = false; 
    #endregion

    #region Runtime State
    private int cachedScreenWidth;   // stored screen width for resolution change detection
    private int cachedScreenHeight;  // stored screen height for resolution change detection
    private float nextResolutionCheckTime;  // timestamp for throttled resolution checks
    #endregion

    #endregion


    #region Unity Methods

    /// <summary>
    /// Assigns missing references, caches initial screen values and applies the first fitting.
    /// </summary>
    private void Awake()
    {
        AssignCameraIfNeeded();
        CacheCurrentScreenSize();
        FitCameraToPlayArea();
    }

    /// <summary>
    /// Performs throttled checks for resolution changes and re-applies fitting when required.
    /// </summary>
    private void Update()
    {
        if (playAreaDefinition == null ||
            !playAreaDefinition.allowRuntimeRecalculation ||
            Time.unscaledTime < nextResolutionCheckTime)
            return;

        nextResolutionCheckTime = Time.unscaledTime + playAreaDefinition.resolutionCheckInterval;

        if (!HasScreenSizeChanged())
            return;

        FitCameraToPlayArea();
        CacheCurrentScreenSize();
    }

    /// <summary>
    /// Draws a gizmo representing the normalized viewport rect when the object is selected.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (targetCamera == null)
            return;

        Rect viewport = targetCamera.rect;

        Vector3 bl = new Vector3(viewport.xMin - 0.5f, viewport.yMin - 0.5f, 0f);
        Vector3 tr = new Vector3(viewport.xMax - 0.5f, viewport.yMax - 0.5f, 0f);

        Vector3 center = (bl + tr) * 0.5f;
        Vector3 size = new Vector3(tr.x - bl.x, tr.y - bl.y, 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }

    #endregion


    #region Fitting Logic

    /// <summary>
    /// Calculates and applies the correct camera viewport rect to preserve the reference aspect ratio.
    /// </summary>
    private void FitCameraToPlayArea()
    {
        if (playAreaDefinition == null || targetCamera == null)
            return;

        float targetAspect = playAreaDefinition.ReferenceAspect;
        float w = Screen.width;
        float h = Screen.height;

        if (h <= 0f)
            return;

        float currentAspect = w / h;

        if (!playAreaDefinition.forceStrictPlayArea)
        {
            targetCamera.rect = new Rect(0f, 0f, 1f, 1f);

            if (enableDebugLog)
                Debug.Log("[PlayAreaCameraFitter] Fullscreen viewport used (strict mode disabled).");

            return;
        }

        if (currentAspect > targetAspect)
        {
            float scale = targetAspect / currentAspect;
            float xOffset = (1f - scale) * 0.5f;

            targetCamera.rect = new Rect(xOffset, 0f, scale, 1f);

            if (enableDebugLog)
                Debug.Log("[PlayAreaCameraFitter] Pillarbox applied.");
        }
        else
        {
            float scale = currentAspect / targetAspect;
            float yOffset = (1f - scale) * 0.5f;

            targetCamera.rect = new Rect(0f, yOffset, 1f, scale);

            if (enableDebugLog)
                Debug.Log("[PlayAreaCameraFitter] Letterbox applied.");
        }
    }

    #endregion


    #region Utilities

    /// <summary>
    /// Assigns Camera.main if no explicit camera reference is provided.
    /// </summary>
    private void AssignCameraIfNeeded()
    {
        if (targetCamera != null)
            return;

        targetCamera = Camera.main;
    }

    /// <summary>
    /// Stores the current screen dimensions for later comparison.
    /// </summary>
    private void CacheCurrentScreenSize()
    {
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
    }

    /// <summary>
    /// Returns true if the current screen size differs from the cached one.
    /// </summary>
    private bool HasScreenSizeChanged()
    {
        if (Screen.width != cachedScreenWidth || Screen.height != cachedScreenHeight)
            return true;

        return false;
    }

    #endregion
}
