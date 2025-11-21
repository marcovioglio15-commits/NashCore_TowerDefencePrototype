using UnityEngine;
using UnityEngine.UI;

namespace CrossDeviceScalability
{
    /// <summary>
    /// Controls camera viewport and UI anchoring to keep the play area consistent across devices.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PlayAreaScaler : MonoBehaviour
    {
        #region Variables And Properties
        #region Serialized Data
        [Tooltip("Profile containing the reference resolution and target orientation.")]
        [SerializeField] private PlayAreaProfile profile;

        [Tooltip("Camera that renders the play area; defaults to the local camera when left empty.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Optional UI root that will be clamped to the play area viewport.")]
        [SerializeField] private RectTransform playAreaUIRoot;

        [Tooltip("Canvas scalers that must follow the play area reference resolution.")]
        [SerializeField] private CanvasScaler[] canvasScalers;

        [Tooltip("Color used to fill the unused screen stripes outside the play area.")]
        [SerializeField] private Color barColor = Color.black;

        [Tooltip("Enables gizmo rendering for the play area frustum.")]
        [SerializeField] private bool gizmoEnabled = true;

        [Tooltip("Depth used exclusively for the play area gizmo illustration.")]
        [SerializeField] private float gizmoDepth = 12f;
        #endregion

        #region State
        private Rect currentViewportRect = new Rect(0f, 0f, 1f, 1f);
        private Vector2Int cachedScreenSize = Vector2Int.zero;
        private Vector2Int cachedReferenceResolution = Vector2Int.zero;
        private PlayAreaProfile.OrientationPreference cachedOrientation = PlayAreaProfile.OrientationPreference.Landscape;
        private Camera resolvedCamera;
        private bool pendingFirstApply = true;

        private static readonly Vector3[] NearCorners = new Vector3[4];
        private static readonly Vector3[] FarCorners = new Vector3[4];
        #endregion

        #region Properties
        /// <summary>
        /// Gets the active viewport rect being applied to the target camera.
        /// </summary>
        public Rect ViewportRect
        {
            get
            {
                return currentViewportRect;
            }
        }
        #endregion
        #endregion

        #region Methods
        #region Unity Events
        /// <summary>
        /// Ensures play area is initialized when the component becomes active.
        /// </summary>
        private void OnEnable()
        {
            pendingFirstApply = true;
            UpdatePlayArea(true);
        }

        /// <summary>
        /// Keeps the play area aligned every frame, including in edit mode.
        /// </summary>
        private void Update()
        {
            bool force = !Application.isPlaying;
            UpdatePlayArea(force);
        }

        /// <summary>
        /// Refreshes configuration when inspector values change.
        /// </summary>
        private void OnValidate()
        {
            pendingFirstApply = true;

            if (gizmoDepth < 0.01f)
                gizmoDepth = 0.01f;

            UpdatePlayArea(true);
        }

        /// <summary>
        /// Draws the play area frustum gizmo.
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawPlayAreaGizmo();
        }

        /// <summary>
        /// Draws the play area frustum gizmo when selected.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            DrawPlayAreaGizmo();
        }
        #endregion

        #region Play Area Logic
        /// <summary>
        /// Updates the camera viewport, UI anchoring, and canvas scaling.
        /// </summary>
        private void UpdatePlayArea(bool force)
        {
            PlayAreaProfile currentProfile = profile;

            if (currentProfile == null)
                return;

            Camera camera = ResolveCamera();

            if (camera == null)
                return;

            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            Vector2Int referenceResolution = currentProfile.ReferenceResolution;
            PlayAreaProfile.OrientationPreference orientation = currentProfile.Orientation;

            bool shouldRefresh = force || pendingFirstApply;

            if (!shouldRefresh && screenSize != cachedScreenSize)
                shouldRefresh = true;

            if (!shouldRefresh && referenceResolution != cachedReferenceResolution)
                shouldRefresh = true;

            if (!shouldRefresh && orientation != cachedOrientation)
                shouldRefresh = true;

            if (!shouldRefresh)
                return;

            cachedScreenSize = screenSize;
            cachedReferenceResolution = referenceResolution;
            cachedOrientation = orientation;
            pendingFirstApply = false;

            float screenAspect = (float)screenSize.x / screenSize.y;
            float targetAspect = currentProfile.TargetAspect;
            Rect computedViewport = CalculateViewportRect(screenAspect, targetAspect);

            ApplyCameraViewport(camera, computedViewport);
            AlignUIRoot(computedViewport);
            AlignCanvasScalers(referenceResolution, orientation, camera);
        }

        /// <summary>
        /// Calculates the normalized viewport rect that maintains the target aspect ratio.
        /// </summary>
        private Rect CalculateViewportRect(float screenAspect, float targetAspect)
        {
            Rect viewport = new Rect(0f, 0f, 1f, 1f);

            if (screenAspect > targetAspect)
            {
                float viewportWidth = targetAspect / screenAspect;
                float viewportOffset = (1f - viewportWidth) * 0.5f;
                viewport = new Rect(viewportOffset, 0f, viewportWidth, 1f);
            }
            else if (screenAspect < targetAspect)
            {
                float viewportHeight = screenAspect / targetAspect;
                float viewportOffset = (1f - viewportHeight) * 0.5f;
                viewport = new Rect(0f, viewportOffset, 1f, viewportHeight);
            }

            return viewport;
        }

        /// <summary>
        /// Applies the computed viewport rect and background color to the target camera.
        /// </summary>
        private void ApplyCameraViewport(Camera camera, Rect viewport)
        {
            bool rectChanged = !RectIsApproximatelyEqual(camera.rect, viewport);

            if (rectChanged)
                camera.rect = viewport;

            currentViewportRect = viewport;
            camera.backgroundColor = barColor;
        }

        /// <summary>
        /// Aligns the optional UI root to the play area viewport and clears offsets.
        /// </summary>
        private void AlignUIRoot(Rect viewport)
        {
            if (playAreaUIRoot == null)
                return;

            playAreaUIRoot.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
            playAreaUIRoot.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
            playAreaUIRoot.offsetMin = Vector2.zero;
            playAreaUIRoot.offsetMax = Vector2.zero;
            playAreaUIRoot.pivot = new Vector2(0.5f, 0.5f);
            playAreaUIRoot.localScale = Vector3.one;
        }

        /// <summary>
        /// Synchronizes provided canvas scalers with the play area configuration.
        /// </summary>
        private void AlignCanvasScalers(Vector2Int referenceResolution, PlayAreaProfile.OrientationPreference orientation, Camera camera)
        {
            if (canvasScalers == null)
                return;

            for (int index = 0; index < canvasScalers.Length; index++)
            {
                CanvasScaler scaler = canvasScalers[index];

                if (scaler == null)
                    continue;

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(referenceResolution.x, referenceResolution.y);
                scaler.matchWidthOrHeight = orientation == PlayAreaProfile.OrientationPreference.Landscape ? 0f : 1f;

                Canvas canvas = scaler.GetComponent<Canvas>();

                if (canvas == null)
                    continue;

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                    canvas.worldCamera = camera;
            }
        }

        /// <summary>
        /// Resolves the camera to be used for play area calculations.
        /// </summary>
        private Camera ResolveCamera()
        {
            if (targetCamera != null)
                return targetCamera;

            if (resolvedCamera == null)
                resolvedCamera = GetComponent<Camera>();

            return resolvedCamera;
        }

        /// <summary>
        /// Checks if two rects are effectively identical within a small tolerance.
        /// </summary>
        private bool RectIsApproximatelyEqual(Rect first, Rect second)
        {
            const float tolerance = 0.0001f;

            float deltaX = Mathf.Abs(first.x - second.x);
            float deltaY = Mathf.Abs(first.y - second.y);
            float deltaWidth = Mathf.Abs(first.width - second.width);
            float deltaHeight = Mathf.Abs(first.height - second.height);

            if (deltaX > tolerance)
                return false;

            if (deltaY > tolerance)
                return false;

            if (deltaWidth > tolerance)
                return false;

            if (deltaHeight > tolerance)
                return false;

            return true;
        }
        #endregion

        #region Gizmo Drawing
        /// <summary>
        /// Renders a frustum gizmo representing the constrained play area.
        /// </summary>
        private void DrawPlayAreaGizmo()
        {
            if (!gizmoEnabled)
                return;

            Camera camera = ResolveCamera();

            if (camera == null)
                return;

            float nearClip = Mathf.Max(camera.nearClipPlane, 0.01f);
            float farClip = Mathf.Max(gizmoDepth, nearClip + 0.05f);
            Rect viewport = currentViewportRect;
            Color previousColor = Gizmos.color;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.7f);

            PopulateFrustumCorners(camera, viewport, nearClip, farClip);

            DrawFrustumEdges();
            Gizmos.color = previousColor;
        }

        /// <summary>
        /// Populates cached frustum corners for gizmo rendering.
        /// </summary>
        private void PopulateFrustumCorners(Camera camera, Rect viewport, float nearClip, float farClip)
        {
            camera.CalculateFrustumCorners(viewport, nearClip, Camera.MonoOrStereoscopicEye.Mono, NearCorners);
            camera.CalculateFrustumCorners(viewport, farClip, Camera.MonoOrStereoscopicEye.Mono, FarCorners);

            for (int index = 0; index < NearCorners.Length; index++)
            {
                NearCorners[index] = camera.transform.TransformPoint(NearCorners[index]);
                FarCorners[index] = camera.transform.TransformPoint(FarCorners[index]);
            }
        }

        /// <summary>
        /// Draws frustum edges using cached corners.
        /// </summary>
        private void DrawFrustumEdges()
        {
            for (int index = 0; index < 4; index++)
            {
                int next = (index + 1) % 4;
                Gizmos.DrawLine(NearCorners[index], NearCorners[next]);
                Gizmos.DrawLine(FarCorners[index], FarCorners[next]);
                Gizmos.DrawLine(NearCorners[index], FarCorners[index]);
            }
        }
        #endregion
        #endregion
    }
}
