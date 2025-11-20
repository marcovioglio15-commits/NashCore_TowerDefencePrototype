using Player.Inventory;
using Scriptables.Turrets;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Player.Build
{
    /// <summary>
    /// Coordinates turret hold gestures, allowing reposition drags or perspective requests routed through EventsManager.
    /// </summary>
    public class TurretInteractionController : MonoBehaviour
    {
        #region Variables And Properties
        #region Serialized Fields
        [Header("Raycast")]
        [Tooltip("Camera used to project touch positions when selecting turrets.")][SerializeField] private Camera interactionCamera;
        [Tooltip("Layer mask restricting turret selection hits.")][SerializeField] private LayerMask turretLayers = ~0;
        [Tooltip("Maximum raycast distance considered when scanning for turrets.")][SerializeField] private float maxRaycastDistance = 500f;

        [Header("Placement")]
        [Tooltip("Placement service used to despawn and respawn turrets during relocation.")][SerializeField] private TurretPlacementService placementService;

        [Header("Timing")]
        [Tooltip("Seconds that a turret must be held before drag relocation becomes available.")][SerializeField] private float repositionHoldDuration = 0.35f;
        [Tooltip("Seconds required to trigger a first-person perspective request on the held turret.")][SerializeField] private float perspectiveHoldDuration = 1.35f;
        [Tooltip("Maximum finger travel in pixels tolerated while charging hold actions.")][SerializeField] private float holdMovementTolerance = 12f;
        [Tooltip("Screen distance in pixels required after the hold gate is cleared to begin dragging.")][SerializeField] private float dragStartDistance = 18f;
        #endregion

        #region Runtime State
        private Finger trackedFinger;
        private PooledTurret trackedTurret;
        private Vector2 fingerStartPosition;
        private float holdTimer;
        private bool perspectiveTriggered;
        private bool dragActive;
        private Vector2 lastDragPosition;
        private TurretClassDefinition cachedDefinition;
        private Vector2Int cachedCell;
        private Quaternion cachedRotation;
        private bool awaitingPlacementResolution;
        private float holdToleranceSqr;
        private float dragStartDistanceSqr;
        private Vector3 debugHoldWorldPosition;
        private bool debugHasHoldReference;
        #endregion
        #endregion

        #region Methods
        #region Unity
        /// <summary>
        /// Computes cached squared thresholds used to minimize per-frame work.
        /// </summary>
        private void Awake()
        {
            CacheThresholds();
        }

        /// <summary>
        /// Subscribes to placement resolution events.
        /// </summary>
        private void OnEnable()
        {
            EventsManager.BuildablePlacementResolved += HandlePlacementResolved;
        }

        /// <summary>
        /// Ensures listeners are removed and pending interactions restored when disabled.
        /// </summary>
        private void OnDisable()
        {
            EventsManager.BuildablePlacementResolved -= HandlePlacementResolved;
            if (dragActive || awaitingPlacementResolution)
                RestoreCachedTurret();

            trackedFinger = null;
            trackedTurret = null;
            dragActive = false;
            awaitingPlacementResolution = false;
            perspectiveTriggered = false;
            holdTimer = 0f;
            HideHoldIndicator();
            ClearCachedPlacement();
            debugHasHoldReference = false;
        }

        /// <summary>
        /// Validates serialized data whenever values change in the inspector.
        /// </summary>
        private void OnValidate()
        {
            CacheThresholds();
        }

        /// <summary>
        /// Evaluates touch state to drive hold and drag behaviour.
        /// </summary>
        private void Update()
        {
            if (!EnhancedTouchSupport.enabled)
                return;

            if (dragActive)
            {
                UpdateActiveDrag();
                return;
            }

            if (trackedFinger != null)
            {
                UpdateActiveHold();
                return;
            }

            TryBeginHold();
        }
        #endregion

        #region Interaction Flow
        /// <summary>
        /// Attempts to latch onto a touch that just began over a turret.
        /// </summary>
        private void TryBeginHold()
        {
            int touches = Touch.activeTouches.Count;
            if (touches == 0)
                return;

            for (int i = 0; i < touches; i++)
            {
                Touch candidate = Touch.activeTouches[i];
                if (candidate.phase != TouchPhase.Began)
                    continue;

                PooledTurret turret;
                if (!TryHitTurret(candidate.screenPosition, out turret))
                    continue;

                trackedFinger = candidate.finger;
                trackedTurret = turret;
                fingerStartPosition = candidate.screenPosition;
                holdTimer = 0f;
                perspectiveTriggered = false;
                UpdateHoldIndicator(0f);
                debugHoldWorldPosition = turret.transform.position;
                debugHasHoldReference = true;
                break;
            }
        }

        /// <summary>
        /// Manages hold timing, drag activation and perspective requests.
        /// </summary>
        private void UpdateActiveHold()
        {
            Touch trackedTouch;
            if (!TryGetTrackedTouch(out trackedTouch))
            {
                ResetHoldState();
                return;
            }

            if (trackedTouch.phase == TouchPhase.Ended || trackedTouch.phase == TouchPhase.Canceled)
            {
                ResetHoldState();
                return;
            }

            if (trackedTurret == null)
            {
                ResetHoldState();
                return;
            }

            if (perspectiveTriggered)
                return;

            holdTimer += Time.deltaTime;
            float normalized = perspectiveHoldDuration > 0f ? Mathf.Clamp01(holdTimer / perspectiveHoldDuration) : 1f;
            UpdateHoldIndicator(normalized);

            Vector2 displacement = trackedTouch.screenPosition - fingerStartPosition;
            float sqrMagnitude = displacement.sqrMagnitude;

            bool withinTolerance = sqrMagnitude <= holdToleranceSqr;
            if (withinTolerance && holdTimer >= perspectiveHoldDuration)
            {
                RequestPerspectiveMode();
                return;
            }

            if (holdTimer < repositionHoldDuration)
                return;

            if (sqrMagnitude < dragStartDistanceSqr)
                return;

            BeginDrag(trackedTouch);
        }

        /// <summary>
        /// Updates drag preview events while the finger remains active.
        /// </summary>
        private void UpdateActiveDrag()
        {
            Touch trackedTouch;
            if (!TryGetTrackedTouch(out trackedTouch))
            {
                RestoreCachedTurret();
                ResetDragState();
                return;
            }

            Vector2 currentPosition = trackedTouch.screenPosition;
            Vector2 delta = currentPosition - lastDragPosition;
            if (delta.sqrMagnitude > 0.01f)
            {
                EventsManager.InvokeBuildableDragUpdated(currentPosition);
                lastDragPosition = currentPosition;
            }

            if (trackedTouch.phase != TouchPhase.Ended && trackedTouch.phase != TouchPhase.Canceled)
                return;

            dragActive = false;
            EventsManager.InvokeBuildableDragEnded(currentPosition);
            awaitingPlacementResolution = true;
            trackedFinger = null;
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Begins relocation by despawning the current turret and routing drag events.
        /// </summary>
        private void BeginDrag(Touch trackedTouch)
        {
            if (placementService == null || trackedTurret == null)
            {
                ResetHoldState();
                return;
            }

            if (!trackedTurret.HasDefinition)
            {
                ResetHoldState();
                return;
            }

            TurretSpawnContext context = trackedTurret.LastContext;
            if (!context.HasGridCoordinate)
            {
                ResetHoldState();
                return;
            }

            cachedDefinition = trackedTurret.Definition;
            cachedCell = context.GridCoordinate;
            cachedRotation = trackedTurret.transform.rotation;

            bool removed = placementService.RemoveTurret(cachedCell);
            if (!removed)
            {
                cachedDefinition = null;
                cachedCell = Vector2Int.zero;
                ResetHoldState();
                return;
            }

            dragActive = true;
            lastDragPosition = trackedTouch.screenPosition;
            EventsManager.InvokeBuildableDragBegan(cachedDefinition, trackedTouch.screenPosition);
            trackedTurret = null;
            HideHoldIndicator();
        }

        /// <summary>
        /// Converts a hold into a perspective request and fires the global event.
        /// </summary>
        private void RequestPerspectiveMode()
        {
            perspectiveTriggered = true;
            if (trackedTurret != null)
                EventsManager.InvokeTurretPerspectiveRequested(trackedTurret);

            HideHoldIndicator();
        }

        /// <summary>
        /// Handles placement callbacks to respawn the original turret if relocation failed.
        /// </summary>
        private void HandlePlacementResolved(BuildPlacementResult result)
        {
            if (!awaitingPlacementResolution)
                return;

            awaitingPlacementResolution = false;
            if (!result.Success)
            {
                RestoreCachedTurret();
                return;
            }

            ClearCachedPlacement();
        }

        /// <summary>
        /// Respawns the cached turret definition onto its original cell.
        /// </summary>
        private void RestoreCachedTurret()
        {
            if (placementService == null || cachedDefinition == null)
            {
                ClearCachedPlacement();
                return;
            }

            placementService.PlaceTurret(cachedDefinition, cachedCell, cachedRotation);
            ClearCachedPlacement();
        }

        /// <summary>
        /// Resets cached placement data after relocation completes.
        /// </summary>
        private void ClearCachedPlacement()
        {
            cachedDefinition = null;
            cachedCell = Vector2Int.zero;
            cachedRotation = Quaternion.identity;
            awaitingPlacementResolution = false;
        }

        /// <summary>
        /// Clears drag-related state without touching cached placement data.
        /// </summary>
        private void ResetDragState()
        {
            dragActive = false;
            awaitingPlacementResolution = false;
            trackedFinger = null;
        }

        /// <summary>
        /// Releases the hold state and hides UI feedback.
        /// </summary>
        private void ResetHoldState()
        {
            trackedFinger = null;
            trackedTurret = null;
            holdTimer = 0f;
            perspectiveTriggered = false;
            HideHoldIndicator();
            debugHasHoldReference = false;
        }

        /// <summary>
        /// Retrieves the current touch associated with the tracked finger.
        /// </summary>
        private bool TryGetTrackedTouch(out Touch trackedTouch)
        {
            trackedTouch = default;
            if (trackedFinger == null)
                return false;

            int touches = Touch.activeTouches.Count;
            for (int i = 0; i < touches; i++)
            {
                Touch candidate = Touch.activeTouches[i];
                if (candidate.finger != trackedFinger)
                    continue;

                trackedTouch = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the provided screen position overlaps a pooled turret.
        /// </summary>
        private bool TryHitTurret(Vector2 screenPosition, out PooledTurret turret)
        {
            turret = null;
            Camera cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
            if (cameraToUse == null)
                return false;

            Ray ray = cameraToUse.ScreenPointToRay(screenPosition);
            RaycastHit hitInfo;
            if (!Physics.Raycast(ray, out hitInfo, maxRaycastDistance, turretLayers, QueryTriggerInteraction.Ignore))
                return false;

            turret = hitInfo.collider.GetComponentInParent<PooledTurret>();
            return turret != null;
        }

        /// <summary>
        /// Pushes hold indicator progress through the UI manager.
        /// </summary>
        private void UpdateHoldIndicator(float normalizedProgress)
        {
            if (trackedTurret == null)
                return;

            UIManager_MainScene manager = UIManager_MainScene.Instance;
            if (manager == null)
                return;

            manager.UpdateTurretHoldIndicator(trackedTurret.transform, normalizedProgress);
        }

        /// <summary>
        /// Hides the hold indicator widget through the UI manager.
        /// </summary>
        private void HideHoldIndicator()
        {
            UIManager_MainScene manager = UIManager_MainScene.Instance;
            if (manager == null)
                return;

            manager.HideTurretHoldIndicator();
        }

        /// <summary>
        /// Draws a simple gizmo indicating the last held turret to aid tuning.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!debugHasHoldReference)
                return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(debugHoldWorldPosition, 0.35f);
            Gizmos.DrawLine(debugHoldWorldPosition, debugHoldWorldPosition + Vector3.up * 0.75f);
        }

        /// <summary>
        /// Ensures cached squared thresholds stay in sync with serialized data.
        /// </summary>
        private void CacheThresholds()
        {
            if (repositionHoldDuration < 0.05f)
                repositionHoldDuration = 0.05f;

            if (perspectiveHoldDuration <= repositionHoldDuration)
                perspectiveHoldDuration = repositionHoldDuration + 0.05f;

            if (holdMovementTolerance < 1f)
                holdMovementTolerance = 1f;

            if (dragStartDistance < 1f)
                dragStartDistance = 1f;

            if (maxRaycastDistance < 1f)
                maxRaycastDistance = 1f;

            holdToleranceSqr = holdMovementTolerance * holdMovementTolerance;
            dragStartDistanceSqr = dragStartDistance * dragStartDistance;
        }
        #endregion
        #endregion
    }
}
