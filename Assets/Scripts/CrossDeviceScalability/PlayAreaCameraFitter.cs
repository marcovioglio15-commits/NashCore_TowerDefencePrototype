using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Fits the camera viewport to the active reference profile defined in PlayAreaSO and exposes a visual gizmo for the captured area.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-500)]
public class PlayAreaCameraFitter : MonoBehaviour
{
    #region Variables And Properties

    #region References
    [Tooltip("Reference asset that defines target resolutions and fitting behavior for desktop and mobile.")]
    [SerializeField] private PlayAreaSO playAreaDefinition;

    [Tooltip("Camera whose viewport will be adjusted. If empty, the main camera will be assigned automatically.")]
    [SerializeField] private Camera targetCamera;
    #endregion

    #region Settings
    [Tooltip("If enabled, prints detailed information about fitting operations.")]
    [SerializeField] private bool enableDebugLog = false;

    [Tooltip("Draws a frustum-like gizmo that represents the area recorded at the active reference resolution.")]
    [SerializeField] private bool drawReferenceFrustum = true;

    [Tooltip("Depth in world units used to render the reference frustum gizmo.")]
    [SerializeField] private float referenceFrustumDepth = 20f;

    [Tooltip("Color used for the reference frustum gizmo lines.")]
    [SerializeField] private Color referenceFrustumColor = new Color(1f, 0.75f, 0.25f, 0.9f);
    #endregion

    #region Runtime State
    private int ScreenWidth;
    private int ScreenHeight;
    private float nextEditorCheckTime;
    private Coroutine runtimeResolutionRoutine;
    #endregion

    #endregion


    #region Methods
    #region Unity Events

    /// <summary>
    /// Initializes  data and performs first fit in both editor and runtime.
    /// </summary>
    private void Awake()
    {
        AssignCameraIfNeeded();
        CacheCurrentScreenSize();
    }

    /// <summary>
    /// Starts runtime monitoring when allowed.
    /// </summary>
    private void OnEnable()
    {
        AssignCameraIfNeeded();
        CacheCurrentScreenSize();
        FitCameraToPlayArea();
        StartRuntimeMonitoring();
    }

    /// <summary>
    /// Stops runtime monitoring when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        StopRuntimeMonitoring();
    }

    /// <summary>
    /// Reacts to editor-only resolution changes, including Device Simulator.
    /// </summary>
    private void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        PerformEditorResolutionCheck();
#endif
    }

    /// <summary>
    /// Reapplies fitting when serialized fields change in the inspector.
    /// </summary>
    private void OnValidate()
    {
        AssignCameraIfNeeded();
        FitCameraToPlayArea();
        CacheCurrentScreenSize();
        RestartRuntimeMonitoringIfNeeded();
    }

    /// <summary>
    /// Draws the reference frustum gizmo to clarify the captured area for the active profile.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (playAreaDefinition == null)
            return;

        AssignCameraIfNeeded();

        if (targetCamera == null)
            return;

        DrawReferenceFrustumGizmo();
    }

    /// <summary>
    /// Draws an editor-only wireframe representing the current viewport rect.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (targetCamera == null)
            return;

        Rect viewport = targetCamera.rect;

        Vector3 bottomLeft = new Vector3(viewport.xMin - 0.5f, viewport.yMin - 0.5f, 0f);
        Vector3 topRight = new Vector3(viewport.xMax - 0.5f, viewport.yMax - 0.5f, 0f);

        Vector3 center = (bottomLeft + topRight) * 0.5f;
        Vector3 size = new Vector3(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y, 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }

    #endregion


    #region Fitting Logic

    /// <summary>
    /// Calculates and applies the correct camera viewport rect using Device Simulator resolution in editor.
    /// </summary>
    private void FitCameraToPlayArea()
    {
        if (playAreaDefinition == null || targetCamera == null)
            return;

        float targetAspect = playAreaDefinition.ActiveReferenceAspect;
        Vector2Int referenceResolution = playAreaDefinition.ActiveReferenceResolution;
        PlayAreaSO.DeviceProfile profile = playAreaDefinition.ActiveProfile;

        float width;
        float height;
        if (!TryGetViewportSize(out width, out height) || height <= 0f)
            return;

        float currentAspect = width / height;

        if (!playAreaDefinition.forceStrictPlayArea)
        {
            targetCamera.rect = new Rect(0f, 0f, 1f, 1f);

            if (enableDebugLog)
                Debug.Log("[PlayAreaCameraFitter] Fullscreen viewport used (strict mode disabled).");

            return;
        }

        ApplyViewportRect(currentAspect, targetAspect, profile, referenceResolution);
    }

    /// <summary>
    /// Applies pillarbox or letterbox based on aspect comparison.
    /// </summary>
    private void ApplyViewportRect(float currentAspect, float targetAspect, PlayAreaSO.DeviceProfile profile, Vector2Int referenceResolution)
    {
        if (currentAspect > targetAspect)
        {
            float scale = targetAspect / currentAspect;
            float xOffset = (1f - scale) * 0.5f;

            targetCamera.rect = new Rect(xOffset, 0f, scale, 1f);

            if (enableDebugLog)
                Debug.Log("[PlayAreaCameraFitter] Pillarbox applied for " + profile + " (" + referenceResolution.x + "x" + referenceResolution.y + ").");
        }
        else
        {
            float scale = currentAspect / targetAspect;
            float yOffset = (1f - scale) * 0.5f;

            targetCamera.rect = new Rect(0f, yOffset, 1f, scale);

            if (enableDebugLog)
                Debug.Log("[PlayAreaCameraFitter] Letterbox applied for " + profile + " (" + referenceResolution.x + "x" + referenceResolution.y + ").");
        }
    }

    #endregion


    #region Monitoring

    /// <summary>
    /// Starts the coroutine that monitors runtime resolution changes without per-frame polling.
    /// </summary>
    private void StartRuntimeMonitoring()
    {
        if (!Application.isPlaying)
            return;

        if (playAreaDefinition == null)
            return;

        if (!playAreaDefinition.allowRuntimeRecalculation)
            return;

        if (runtimeResolutionRoutine != null)
            return;

        runtimeResolutionRoutine = StartCoroutine(RuntimeResolutionRoutine());
    }

    /// <summary>
    /// Stops the runtime monitoring coroutine if active.
    /// </summary>
    private void StopRuntimeMonitoring()
    {
        if (runtimeResolutionRoutine == null)
            return;

        StopCoroutine(runtimeResolutionRoutine);
        runtimeResolutionRoutine = null;
    }

    /// <summary>
    /// Restarts runtime monitoring when inspector changes require it.
    /// </summary>
    private void RestartRuntimeMonitoringIfNeeded()
    {
        if (!Application.isPlaying)
            return;

        StopRuntimeMonitoring();
        StartRuntimeMonitoring();
    }

    /// <summary>
    /// Checks resolution at configured intervals during runtime.
    /// </summary>
    private IEnumerator RuntimeResolutionRoutine()
    {
        float interval = GetSafeInterval();
        WaitForSecondsRealtime waitInstruction = new WaitForSecondsRealtime(interval);

        while (Application.isPlaying && playAreaDefinition != null && playAreaDefinition.allowRuntimeRecalculation)
        {
            yield return waitInstruction;

            if (!HasScreenSizeChanged())
                continue;

            FitCameraToPlayArea();
            CacheCurrentScreenSize();

            interval = GetSafeInterval();
            waitInstruction = new WaitForSecondsRealtime(interval);
        }

        runtimeResolutionRoutine = null;
    }

    /// <summary>
    /// Processes resolution changes in the editor without per-frame work.
    /// </summary>
    private void PerformEditorResolutionCheck()
    {
        if (playAreaDefinition == null)
            return;

        float interval = GetSafeInterval();
        if (Time.realtimeSinceStartup < nextEditorCheckTime)
            return;

        nextEditorCheckTime = Time.realtimeSinceStartup + interval;

        if (!HasScreenSizeChanged())
            return;

        FitCameraToPlayArea();
        CacheCurrentScreenSize();
    }

    #endregion


    #region Utilities

    /// <summary> 
    /// Assigns Camera.main if no explicit reference is set.
    /// </summary>
    private void AssignCameraIfNeeded()
    {
        if (targetCamera != null)
            return;

        targetCamera = Camera.main;
    }

    /// <summary>
    /// Returns the current viewport dimensions (Game View in editor, screen at runtime).
    /// </summary>
    private bool TryGetViewportSize(out float width, out float height)
    {
#if UNITY_EDITOR
        Vector2 gameViewSize = Handles.GetMainGameViewSize();
        width = gameViewSize.x;
        height = gameViewSize.y;
#else
        width = Screen.width;
        height = Screen.height;
#endif
        return height > 0f;
    }

    /// <summary>
    ///  the current resolution for later comparison.
    /// </summary>
    private void CacheCurrentScreenSize()
    {
#if UNITY_EDITOR
        Vector2 gameViewSize = Handles.GetMainGameViewSize();
        ScreenWidth = (int)gameViewSize.x;
        ScreenHeight = (int)gameViewSize.y;
#else
        ScreenWidth = Screen.width;
        ScreenHeight = Screen.height;
#endif
    }

    /// <summary>
    /// Detects resolution changes in runtime or editor, including Device Simulator.
    /// </summary>
    private bool HasScreenSizeChanged()
    {
#if UNITY_EDITOR
        Vector2 gameViewSize = Handles.GetMainGameViewSize();
        if ((int)gameViewSize.x != ScreenWidth || (int)gameViewSize.y != ScreenHeight)
            return true;
        return false;
#else
        if (Screen.width != ScreenWidth || Screen.height != ScreenHeight)
            return true;
        return false;
#endif
    }

    /// <summary>
    /// Ensures a minimum interval to prevent tight polling loops.
    /// </summary>
    private float GetSafeInterval()
    {
        float interval = 0.25f;
        if (playAreaDefinition != null)
            interval = Mathf.Max(0.1f, playAreaDefinition.resolutionCheckInterval);

        return interval;
    }

    #endregion


    #region Gizmos
    /// <summary>
    /// Draws a box that matches the active reference resolution and the camera projection.
    /// </summary>
    private void DrawReferenceFrustumGizmo()
    {
        if (!drawReferenceFrustum)
            return;

        float depth = Mathf.Max(0.1f, referenceFrustumDepth);
        float aspect = playAreaDefinition.ActiveReferenceAspect;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = targetCamera.transform.localToWorldMatrix;
        Gizmos.color = referenceFrustumColor;

        if (targetCamera.orthographic)
        {
            float height = targetCamera.orthographicSize * 2f;
            float width = height * aspect;
            float near = targetCamera.nearClipPlane;
            float sizeDepth = Mathf.Clamp(depth, 0.1f, targetCamera.farClipPlane - near);
            Vector3 size = new Vector3(width, height, sizeDepth);
            Vector3 center = new Vector3(0f, 0f, near + sizeDepth * 0.5f);
            Gizmos.DrawWireCube(center, size);
        }
        else
        {
            float farDistance = Mathf.Min(targetCamera.nearClipPlane + depth, targetCamera.farClipPlane);
            Gizmos.DrawFrustum(Vector3.zero, targetCamera.fieldOfView, farDistance, targetCamera.nearClipPlane, aspect);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    #endregion
    #endregion
}
