using System.Collections.Generic;
using Player.Inventory;
using Scriptables.Turrets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
        [Tooltip("Camera used to project touch positions when selecting turrets.")]
        [SerializeField] private Camera interactionCamera;
        [Tooltip("Layer mask restricting turret selection hits.")]
        [SerializeField] private LayerMask turretLayers = ~0;
        [Tooltip("Maximum raycast distance considered when scanning for turrets.")]
        [SerializeField] private float maxRaycastDistance = 500f;

        [Header("Placement")]
        [Tooltip("Placement service used to despawn and respawn turrets during relocation.")]
        [SerializeField] private TurretPlacementLogic placementService;

        [Header("Timing")]
        [Tooltip("Seconds that a turret must be held before drag relocation becomes available.")]
        [SerializeField] private float repositionHoldDuration = 0.1f;
        [Tooltip("Seconds required to trigger a first-person perspective request on the held turret.")]
        [SerializeField] private float perspectiveHoldDuration = 1.35f;
        [Tooltip("Maximum finger travel in pixels tolerated while charging hold actions.")]
        [SerializeField] private float holdMovementTolerance = 12f;
        [Tooltip("Screen distance in pixels required after the hold gate is cleared to begin dragging.")]
        [SerializeField] private float dragStartDistance = 18f;
        #endregion

        #region Runtime State
        private PooledTurret trackedTurret;
        private Vector2 fingerStartPosition;
        private Vector2 primaryCurrentPosition;
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
        private bool primaryContactActive;
        private bool secondaryContactActive;
        private bool holdActive;
        private bool holdMonitorAttached;
        private TouchControls touchControls;
        private TouchControls.TouchActions touchActions;
        private InputAction primaryContactAction;
        private InputAction primaryPositionAction;
        private InputAction secondaryContactAction;
        private bool allowReposition = true;
        private bool allowPerspective = true;
        private bool allowHoldFeedback = true;
        private bool possessionLocked;
        private static readonly List<RaycastResult> uiRaycastBuffer = new List<RaycastResult>(8);
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
            EventsManager.GamePhaseChanged += HandleGamePhaseChanged;
            EventsManager.TurretFreeAimStarted += HandleTurretFreeAimStarted;
            EventsManager.TurretFreeAimEnded += HandleTurretFreeAimEnded;
            SyncPhasePermissions();
            InitializeInputBindings();
        }

        /// <summary>
        /// Ensures listeners are removed and pending interactions restored when disabled.
        /// </summary>
        private void OnDisable()
        {
            EventsManager.BuildablePlacementResolved -= HandlePlacementResolved;
            EventsManager.GamePhaseChanged -= HandleGamePhaseChanged;
            EventsManager.TurretFreeAimStarted -= HandleTurretFreeAimStarted;
            EventsManager.TurretFreeAimEnded -= HandleTurretFreeAimEnded;
            if (dragActive || awaitingPlacementResolution)
                RestoreCachedTurret();

            TeardownInputBindings();
            ResetDragState();
            ResetHoldState();
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

        #endregion

        #region Interaction Flow

        /// <summary>
        /// Initializes input action references and subscriptions.
        /// </summary>
        private void InitializeInputBindings()
        {
            InputManager manager = InputManager.Instance;
            if (manager == null)
            {
                Debug.LogError("TurretInteractionController requires InputManager.");
                enabled = false;
                return;
            }

            touchControls = manager.TouchControls;
            if (touchControls == null)
            {
                Debug.LogError("TurretInteractionController requires TouchControls to be initialized.");
                enabled = false;
                return;
            }

            touchActions = touchControls.Touch;
            primaryContactAction = touchActions.PrimaryContact;
            primaryPositionAction = touchActions.PrimaryPosition;
            secondaryContactAction = touchActions.SecondaryContact;

            primaryContactAction.started += OnPrimaryContactStarted;
            primaryContactAction.canceled += OnPrimaryContactCanceled;
            primaryPositionAction.performed += OnPrimaryPositionChanged;
            secondaryContactAction.started += OnSecondaryContactStarted;
            secondaryContactAction.canceled += OnSecondaryContactCanceled;
        }

        /// <summary>
        /// Removes input action subscriptions and clears timers.
        /// </summary>
        private void TeardownInputBindings()
        {
            DetachHoldMonitor();

            if (primaryContactAction != null)
            {
                primaryContactAction.started -= OnPrimaryContactStarted;
                primaryContactAction.canceled -= OnPrimaryContactCanceled;
            }

            if (primaryPositionAction != null)
                primaryPositionAction.performed -= OnPrimaryPositionChanged;

            if (secondaryContactAction != null)
            {
                secondaryContactAction.started -= OnSecondaryContactStarted;
                secondaryContactAction.canceled -= OnSecondaryContactCanceled;
            }

            touchControls = null;
        }

        /// <summary>
        /// Handles primary contact start and attempts to acquire a turret target.
        /// </summary>
        private void OnPrimaryContactStarted(InputAction.CallbackContext context)
        {
            primaryContactActive = true;

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.IsGamePaused)
            {
                primaryContactActive = false;
                return;
            }

            if (!allowReposition && !allowPerspective)
            {
                primaryContactActive = false;
                return;
            }

            if (possessionLocked)
            {
                primaryContactActive = false;
                return;
            }

            primaryCurrentPosition = primaryPositionAction.ReadValue<Vector2>();

            if (IsPointerOverUi(primaryCurrentPosition))
                return;

            PooledTurret turret;
            if (!TryHitTurret(primaryCurrentPosition, out turret))
                return;

            trackedTurret = turret;
            fingerStartPosition = primaryCurrentPosition;
            holdTimer = 0f;
            perspectiveTriggered = false;
            dragActive = false;
            holdActive = true;
            UpdateHoldIndicator(0f);
            debugHoldWorldPosition = turret.transform.position;
            debugHasHoldReference = true;
            AttachHoldMonitor();
        }

        /// <summary>
        /// Updates the active hold or drag when the primary position changes.
        /// </summary>
        private void OnPrimaryPositionChanged(InputAction.CallbackContext context)
        {
            primaryCurrentPosition = context.ReadValue<Vector2>();

            if (dragActive)
            {
                UpdateActiveDrag(primaryCurrentPosition);
                return;
            }

            if (holdActive)
                UpdateActiveHold();
        }

        /// <summary>
        /// Finalizes hold or drag when the primary contact ends.
        /// </summary>
        private void OnPrimaryContactCanceled(InputAction.CallbackContext context)
        {
            primaryContactActive = false;

            if (dragActive)
            {
                dragActive = false;
                awaitingPlacementResolution = true;
                EventsManager.InvokeBuildableDragEnded(primaryCurrentPosition);
            }

            ResetHoldState();
            DetachHoldMonitor();
        }

        /// <summary>
        /// Cancels hold logic when a secondary contact begins (e.g., pinch).
        /// </summary>
        private void OnSecondaryContactStarted(InputAction.CallbackContext context)
        {
            secondaryContactActive = true;
            if (dragActive)
            {
                RestoreCachedTurret();
                ResetDragState();
            }
            ResetHoldState();
        }

        /// <summary>
        /// Releases the secondary contact flag when it ends.
        /// </summary>
        private void OnSecondaryContactCanceled(InputAction.CallbackContext context)
        {
            secondaryContactActive = false;
        }

        /// <summary>
        /// Manages hold timing, drag activation and perspective requests.
        /// </summary>
        private void UpdateActiveHold()
        {
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.IsGamePaused)
            {
                if (dragActive)
                {
                    RestoreCachedTurret();
                    ResetDragState();
                }

                ResetHoldState();
                return;
            }

            if (!primaryContactActive)
            {
                ResetHoldState();
                return;
            }

            if (secondaryContactActive)
            {
                ResetHoldState();
                return;
            }

            if (trackedTurret == null)
            {
                ResetHoldState();
                return;
            }

            Vector2 displacement = primaryCurrentPosition - fingerStartPosition;
            float sqrMagnitude = displacement.sqrMagnitude;

            holdTimer += Time.deltaTime;
            float normalized = perspectiveHoldDuration > 0f ? Mathf.Clamp01(holdTimer / perspectiveHoldDuration) : 1f;
            UpdateHoldIndicator(normalized);

            bool withinTolerance = sqrMagnitude <= holdToleranceSqr;
            bool canStartDrag = allowReposition && holdTimer >= repositionHoldDuration && sqrMagnitude >= dragStartDistanceSqr;

            if (canStartDrag)
            {
                BeginDrag(primaryCurrentPosition);
                return;
            }

            if (perspectiveTriggered)
                return;

            if (allowPerspective && withinTolerance && holdTimer >= perspectiveHoldDuration)
            {
                RequestPerspectiveMode();
                return;
            }
        }

        /// <summary>
        /// Updates drag preview events while the finger remains active.
        /// </summary>
        private void UpdateActiveDrag(Vector2 currentPosition)
        {
            Vector2 delta = currentPosition - lastDragPosition;
            if (delta.sqrMagnitude > 0.01f)
            {
                EventsManager.InvokeBuildableDragUpdated(currentPosition);
                lastDragPosition = currentPosition;
            }

            if (!primaryContactActive)
            {
                dragActive = false;
                awaitingPlacementResolution = true;
                EventsManager.InvokeBuildableDragEnded(currentPosition);
            }
        }

        /// <summary>
        /// Attaches the hold timer to the input update loop.
        /// </summary>
        private void AttachHoldMonitor()
        {
            if (holdMonitorAttached)
                return;

            InputSystem.onAfterUpdate += HandleHoldTick;
            holdMonitorAttached = true;
        }

        /// <summary>
        /// Detaches the hold timer when idle.
        /// </summary>
        private void DetachHoldMonitor()
        {
            if (!holdMonitorAttached)
                return;

            InputSystem.onAfterUpdate -= HandleHoldTick;
            holdMonitorAttached = false;
        }

        /// <summary>
        /// Advances hold timing without a per-frame MonoBehaviour Update.
        /// </summary>
        private void HandleHoldTick()
        {
            if (!holdActive)
                return;

            UpdateActiveHold();
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Begins relocation by despawning the current turret and routing drag events.
        /// </summary>
        private void BeginDrag(Vector2 screenPosition)
        {
            if (!allowReposition)
            {
                ResetHoldState();
                return;
            }

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
            lastDragPosition = screenPosition;
            EventsManager.InvokeBuildableRelocationBegan(cachedDefinition);
            EventsManager.InvokeBuildableDragBegan(cachedDefinition, screenPosition);
            trackedTurret = null;
            HideHoldIndicator();
        }

        /// <summary>
        /// Converts a hold into a perspective request and fires the global event.
        /// </summary>
        private void RequestPerspectiveMode()
        {
            if (!allowPerspective)
            {
                ResetHoldState();
                return;
            }

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
        }

        /// <summary>
        /// Releases the hold state and hides UI feedback.
        /// </summary>
        private void ResetHoldState()
        {
            holdActive = false;
            primaryContactActive = false;
            secondaryContactActive = false;
            trackedTurret = null;
            holdTimer = 0f;
            perspectiveTriggered = false;
            HideHoldIndicator();
            debugHasHoldReference = false;
            DetachHoldMonitor();
        }

        /// <summary>
        /// Checks whether the provided screen position overlaps a pooled turret.
        /// </summary>
        private bool TryHitTurret(Vector2 screenPosition, out PooledTurret turret)
        {
            turret = null;
            if (float.IsNaN(screenPosition.x) || float.IsNaN(screenPosition.y))
                return false;

            if (float.IsInfinity(screenPosition.x) || float.IsInfinity(screenPosition.y))
                return false;

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
        /// Returns true if the provided screen position overlaps a UI element.
        /// </summary>
        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            EventSystem system = EventSystem.current;
            if (system == null)
                return false;

            uiRaycastBuffer.Clear();
            PointerEventData eventData = new PointerEventData(system);
            eventData.position = screenPosition;
            system.RaycastAll(eventData, uiRaycastBuffer);
            bool overUi = uiRaycastBuffer.Count > 0;
            uiRaycastBuffer.Clear();
            return overUi;
        }

        /// <summary>
        /// Pushes hold indicator progress through the UI manager.
        /// </summary>
        private void UpdateHoldIndicator(float normalizedProgress)
        {
            if (!allowHoldFeedback)
                return;

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

        /// <summary>
        /// Updates access flags when the global game phase changes.
        /// </summary>
        private void HandleGamePhaseChanged(GamePhase phase)
        {
            SetPhaseCapabilities(phase == GamePhase.Building, phase == GamePhase.Combat);
            if (!allowReposition)
                ResetHoldState();
            if (!allowHoldFeedback)
                HideHoldIndicator();
        }

        /// <summary>
        /// Prevents starting new hold interactions while a turret is already possessed.
        /// </summary>
        private void HandleTurretFreeAimStarted(PooledTurret turret)
        {
            possessionLocked = true;
            ResetHoldState();
            HideHoldIndicator();
        }

        /// <summary>
        /// Allows new holds once free-aim possession ends.
        /// </summary>
        private void HandleTurretFreeAimEnded(PooledTurret turret)
        {
            possessionLocked = false;
        }

        /// <summary>
        /// Exposes a way for the GameManager to toggle reposition and perspective permissions.
        /// </summary>
        public void SetPhaseCapabilities(bool enableReposition, bool enablePerspectiveMode)
        {
            allowReposition = enableReposition;
            allowPerspective = enablePerspectiveMode;
            allowHoldFeedback = enablePerspectiveMode;
        }

        /// <summary>
        /// Caches permissions from the GameManager when available to avoid race conditions on enable.
        /// </summary>
        private void SyncPhasePermissions()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
                return;

            SetPhaseCapabilities(manager.CurrentPhase == GamePhase.Building, manager.CurrentPhase == GamePhase.Combat);
        }
        #endregion
        #endregion
    }
}
