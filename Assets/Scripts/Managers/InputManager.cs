using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// Centralized mobile gesture manager backed by the generated TouchControls input wrapper.
/// </summary>
[DefaultExecutionOrder(-200)]
public class InputManager : Singleton<InputManager>
{
    #region VAriables And Properties
    #region
    [Header("Swipe detection")]
    [Tooltip("Minimum travel distance in pixels to classify the gesture as a swipe.")]
    [SerializeField] private float swipeDistanceThreshold = 80f;
    [Tooltip("Maximum time in seconds allowed to cover the swipe distance.")]
    [SerializeField] private float swipeTimeThreshold = 0.35f;

    [Header("Drag detection")]
    [Tooltip("Minimum travel distance in pixels before a drag begins transmitting delta.")]
    [SerializeField] private float dragStartDistanceThreshold = 8f;

    [Header("Hold detection")]
    [Tooltip("Minimum press duration in seconds before a hold is emitted.")]
    [SerializeField] private float holdDurationThreshold = 0.5f;

    [Tooltip("Minimum change in distance between the two touches to register a pinch.")]
    [Header("Pinch detection")]
    [SerializeField] private float pinchDistanceThreshold = 5f;

    [Header("Debug")]
    [Tooltip("Draws runtime gizmos for the active input gesture.")]
    [SerializeField] private bool drawDebugGizmos = true;
    [Tooltip("Depth in world units used to render screen-space gizmos.")]
    [SerializeField] private float debugGizmoDepth = 10f;

    #endregion

    #region Properties

    public float SwipeDistanceThreshold => swipeDistanceThreshold;
    public float SwipeTimeThreshold => swipeTimeThreshold;
    public float DragStartDistanceThreshold => dragStartDistanceThreshold;
    public float PinchDistanceThreshold => pinchDistanceThreshold;
    public TouchControls TouchControls => touchControls;

    #endregion

    #region Runtime State

    private TouchControls touchControls;
    private TouchControls.TouchActions touchActions;
    private InputAction primaryContactAction;
    private InputAction primaryPositionAction;
    private InputAction secondaryContactAction;
    private InputAction secondaryPositionAction;

    private bool primaryContactActive;
    private bool secondaryContactActive;
    private bool primaryGestureActive;
    private bool pinchGestureActive;
    private bool holdRaised;
    private bool swipeRaised;
    private bool dragActive;
    private bool holdMonitorAttached;

    private double primaryStartTime;
    private Vector2 primaryStartPosition;
    private Vector2 primaryCurrentPosition;
    private Vector2 primaryLastPosition;
    private Vector2 secondaryCurrentPosition;
    private Vector2 pinchPreviousVector;

    private static readonly Color PrimaryGizmoColor = new Color(0.12f, 0.73f, 1f, 0.95f);
    private static readonly Color SecondaryGizmoColor = new Color(1f, 0.58f, 0.1f, 0.95f);
    private static readonly Color PinchGizmoColor = new Color(0.35f, 1f, 0.4f, 0.9f);
    private const float GizmoSphereRadius = 10f;
    private const float GizmoLineWidth = 2.5f;

    #endregion
    #endregion

    #region Methods
    #region Unity Lifecycle

    /// <summary>
    /// Initializes the input action references and keeps the manager alive across scenes.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
            TouchSimulation.Enable();
        }
        InitializeInputActions();
    }

    /// <summary>
    /// Enables the configured input action map.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        EnableInputActions();
    }

    /// <summary>
    /// Disables the configured input action map and timers.
    /// </summary>
    private void OnDisable()
    {
        DisableInputActions();
    }

    /// <summary>
    /// Disposes generated input resources.
    /// </summary>
    private void OnDestroy()
    {
        DisposeInputActions();
    }

    #endregion

    #region Public Configuration

    /// <summary>
    /// Updates the swipe distance threshold at runtime.
    /// </summary>
    public void SetSwipeDistanceThreshold(float value)
    {
        swipeDistanceThreshold = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Updates the swipe time window at runtime.
    /// </summary>
    public void SetSwipeTimeThreshold(float value)
    {
        swipeTimeThreshold = Mathf.Max(0.01f, value);
    }

    /// <summary>
    /// Updates the drag distance threshold at runtime.
    /// </summary>
    public void SetDragStartDistanceThreshold(float value)
    {
        dragStartDistanceThreshold = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Updates the pinch distance threshold at runtime.
    /// </summary>
    public void SetPinchDistanceThreshold(float value)
    {
        pinchDistanceThreshold = Mathf.Max(0f, value);
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Resolves the action map and binds callbacks once.
    /// </summary>
    private void InitializeInputActions()
    {
        touchControls = new TouchControls();
        touchActions = touchControls.Touch;
        primaryContactAction = touchActions.PrimaryContact;
        primaryPositionAction = touchActions.PrimaryPosition;
        secondaryContactAction = touchActions.SecondaryContact;
        secondaryPositionAction = touchActions.SecondaryPosition;

        primaryContactAction.started += OnPrimaryContactStarted;
        primaryContactAction.canceled += OnPrimaryContactCanceled;
        primaryPositionAction.performed += OnPrimaryPositionChanged;
        secondaryContactAction.started += OnSecondaryContactStarted;
        secondaryContactAction.canceled += OnSecondaryContactCanceled;
        secondaryPositionAction.performed += OnSecondaryPositionChanged;
    }

    /// <summary>
    /// Enables the input actions when the component is active.
    /// </summary>
    private void EnableInputActions()
    {
        if (touchControls == null)
            return;

        touchActions.Enable();
    }

    /// <summary>
    /// Disables the input actions and time-based checks when the component is inactive.
    /// </summary>
    private void DisableInputActions()
    {
        if (touchControls == null)
            return;

        touchActions.Disable();
        DetachHoldMonitor();
    }

    /// <summary>
    /// Releases the generated input wrapper.
    /// </summary>
    private void DisposeInputActions()
    {
        if (touchControls == null)
            return;

        DetachHoldMonitor();
        primaryContactAction.started -= OnPrimaryContactStarted;
        primaryContactAction.canceled -= OnPrimaryContactCanceled;
        primaryPositionAction.performed -= OnPrimaryPositionChanged;
        secondaryContactAction.started -= OnSecondaryContactStarted;
        secondaryContactAction.canceled -= OnSecondaryContactCanceled;
        secondaryPositionAction.performed -= OnSecondaryPositionChanged;

        touchActions.Disable();
        touchControls.Dispose();
        touchControls = null;
    }

    #endregion

    #region Input Callbacks

    /// <summary>
    /// Begins tracking for primary touch contact.
    /// </summary>
    private void OnPrimaryContactStarted(InputAction.CallbackContext context)
    {
        primaryContactActive = true;
        primaryCurrentPosition = primaryPositionAction.ReadValue<Vector2>();
        primaryLastPosition = primaryCurrentPosition;

        if (secondaryContactActive)
        {
            CancelPrimaryGesture();
            PreparePinchBaseline();
            return;
        }

        BeginPrimaryGesture();
    }

    /// <summary>
    /// Finalizes primary contact when lifted.
    /// </summary>
    private void OnPrimaryContactCanceled(InputAction.CallbackContext context)
    {
        primaryContactActive = false;

        if (pinchGestureActive)
            ResetPinchState();
        else if (primaryGestureActive)
            FinalizePrimaryGesture();

        DetachHoldMonitor();
    }

    /// <summary>
    /// Tracks primary position changes for gesture detection.
    /// </summary>
    private void OnPrimaryPositionChanged(InputAction.CallbackContext context)
    {
        primaryCurrentPosition = context.ReadValue<Vector2>();

        if (pinchGestureActive)
        {
            UpdatePinchGesture();
            return;
        }

        if (!primaryGestureActive || !primaryContactActive)
            return;

        UpdatePrimaryGesture();
    }

    /// <summary>
    /// Starts pinch mode once a second contact is detected.
    /// </summary>
    private void OnSecondaryContactStarted(InputAction.CallbackContext context)
    {
        secondaryContactActive = true;
        secondaryCurrentPosition = secondaryPositionAction.ReadValue<Vector2>();
        CancelPrimaryGesture();
        PreparePinchBaseline();
    }

    /// <summary>
    /// Ends pinch mode when the second contact is released.
    /// </summary>
    private void OnSecondaryContactCanceled(InputAction.CallbackContext context)
    {
        secondaryContactActive = false;
        ResetPinchState();

        if (primaryContactActive)
            BeginPrimaryGesture();
        else
            DetachHoldMonitor();
    }

    /// <summary>
    /// Updates pinch calculations when the secondary finger moves.
    /// </summary>
    private void OnSecondaryPositionChanged(InputAction.CallbackContext context)
    {
        secondaryCurrentPosition = context.ReadValue<Vector2>();

        if (!pinchGestureActive && primaryContactActive)
            PreparePinchBaseline();

        if (pinchGestureActive)
            UpdatePinchGesture();
    }

    #endregion

    #region Gesture Management

    /// <summary>
    /// Initializes single-finger gesture state.
    /// </summary>
    private void BeginPrimaryGesture()
    {
        primaryGestureActive = true;
        primaryStartPosition = primaryCurrentPosition;
        primaryLastPosition = primaryCurrentPosition;
        primaryStartTime = Time.timeAsDouble;
        holdRaised = false;
        swipeRaised = false;
        dragActive = false;
        AttachHoldMonitor();
    }

    /// <summary>
    /// Processes movement-based gesture conditions for the primary finger.
    /// </summary>
    private void UpdatePrimaryGesture()
    {
        Vector2 displacement = primaryCurrentPosition - primaryStartPosition;
        double elapsed = Time.timeAsDouble - primaryStartTime;

        EvaluateHold(displacement, elapsed);
        EvaluateSwipe(displacement, elapsed);
        EvaluateDrag();
        primaryLastPosition = primaryCurrentPosition;
    }

    /// <summary>
    /// Finalizes the single-finger gesture when contact ends.
    /// </summary>
    private void FinalizePrimaryGesture()
    {
        Vector2 totalDisplacement = primaryCurrentPosition - primaryStartPosition;
        double elapsed = Time.timeAsDouble - primaryStartTime;

        if (holdRaised)
            EventsManager.InvokeHoldEnded(primaryCurrentPosition);

        bool tapped = !swipeRaised && !dragActive && !holdRaised && totalDisplacement.magnitude <= dragStartDistanceThreshold;
        if (tapped)
            EventsManager.InvokeTap(primaryCurrentPosition);

        bool qualifiesLateSwipe = !swipeRaised && totalDisplacement.magnitude >= swipeDistanceThreshold && elapsed <= swipeTimeThreshold;
        if (qualifiesLateSwipe)
            EventsManager.InvokeSwipe(totalDisplacement);

        ResetPrimaryGestureState();
    }

    /// <summary>
    /// Cancels the ongoing primary gesture without resolving tap or swipe.
    /// </summary>
    private void CancelPrimaryGesture()
    {
        if (holdRaised)
            EventsManager.InvokeHoldEnded(primaryCurrentPosition);

        ResetPrimaryGestureState();
    }

    /// <summary>
    /// Reinitializes single-finger state after pinch mode.
    /// </summary>
    private void ResetPrimaryGestureState()
    {
        primaryGestureActive = false;
        holdRaised = false;
        swipeRaised = false;
        dragActive = false;
        primaryStartPosition = Vector2.zero;
        primaryLastPosition = Vector2.zero;
        primaryStartTime = 0d;
    }

    #endregion

    #region Gesture Evaluation

    /// <summary>
    /// Evaluates hold eligibility using elapsed time and displacement.
    /// </summary>
    private void EvaluateHold(Vector2 displacement, double elapsed)
    {
        if (holdRaised)
            return;

        bool minimalMovement = displacement.magnitude <= dragStartDistanceThreshold;
        if (elapsed >= holdDurationThreshold && minimalMovement)
        {
            EventsManager.InvokeHoldBegan(primaryCurrentPosition);
            holdRaised = true;
        }
    }

    /// <summary>
    /// Evaluates swipe eligibility using displacement and time budget.
    /// </summary>
    private void EvaluateSwipe(Vector2 displacement, double elapsed)
    {
        if (swipeRaised)
            return;

        bool swipeReady = displacement.magnitude >= swipeDistanceThreshold && elapsed <= swipeTimeThreshold;
        if (swipeReady)
        {
            EventsManager.InvokeSwipe(displacement);
            swipeRaised = true;
        }
    }

    /// <summary>
    /// Starts and reports drag deltas when thresholds are met.
    /// </summary>
    private void EvaluateDrag()
    {
        Vector2 displacement = primaryCurrentPosition - primaryStartPosition;
        if (!dragActive && !swipeRaised && displacement.magnitude >= dragStartDistanceThreshold)
            dragActive = true;

        if (!dragActive)
            return;

        Vector2 frameDelta = primaryCurrentPosition - primaryLastPosition;
        if (frameDelta.sqrMagnitude > 0f)
            EventsManager.InvokeDrag(frameDelta);
    }

    #endregion

    #region Pinch Gestures

    /// <summary>
    /// Captures the baseline vector between two active touches.
    /// </summary>
    private void PreparePinchBaseline()
    {
        if (!primaryContactActive || !secondaryContactActive)
            return;

        pinchPreviousVector = secondaryCurrentPosition - primaryCurrentPosition;
        pinchGestureActive = true;
        DetachHoldMonitor();
    }

    /// <summary>
    /// Processes pinch delta and emits pinch in/out events.
    /// </summary>
    private void UpdatePinchGesture()
    {
        if (!primaryContactActive || !secondaryContactActive)
            return;

        Vector2 currentVector = secondaryCurrentPosition - primaryCurrentPosition;
        float magnitudeDelta = Mathf.Abs(currentVector.magnitude - pinchPreviousVector.magnitude);

        if (magnitudeDelta >= pinchDistanceThreshold)
        {
            Vector2 delta = currentVector - pinchPreviousVector;

            if (currentVector.magnitude < pinchPreviousVector.magnitude)
                EventsManager.InvokePinchIn(delta);
            else
                EventsManager.InvokePinchOut(delta);
        }

        pinchPreviousVector = currentVector;
    }

    /// <summary>
    /// Clears pinch state and resumes single-finger tracking when possible.
    /// </summary>
    private void ResetPinchState()
    {
        pinchGestureActive = false;
        pinchPreviousVector = Vector2.zero;
    }

    #endregion

    #region Time-Based Checks

    /// <summary>
    /// Attaches a lightweight update hook used only for hold timing.
    /// </summary>
    private void AttachHoldMonitor()
    {
        if (holdMonitorAttached)
            return;

        InputSystem.onAfterUpdate += HandleTimedGestures;
        holdMonitorAttached = true;
    }

    /// <summary>
    /// Detaches the time-based update hook when idle.
    /// </summary>
    private void DetachHoldMonitor()
    {
        if (!holdMonitorAttached)
            return;

        InputSystem.onAfterUpdate -= HandleTimedGestures;
        holdMonitorAttached = false;
    }

    /// <summary>
    /// Monitors hold timing without forcing a per-frame MonoBehaviour Update.
    /// </summary>
    private void HandleTimedGestures()
    {
        if (!primaryGestureActive || pinchGestureActive)
            return;

        Vector2 displacement = primaryCurrentPosition - primaryStartPosition;
        double elapsed = Time.timeAsDouble - primaryStartTime;
        EvaluateHold(displacement, elapsed);
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// Draws screen-space gizmos for debugging gestures.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null)
            return;

        if (primaryContactActive)
            DrawTouchGizmo(sceneCamera, primaryCurrentPosition, PrimaryGizmoColor);

        if (secondaryContactActive)
            DrawTouchGizmo(sceneCamera, secondaryCurrentPosition, SecondaryGizmoColor);

        if (pinchGestureActive && primaryContactActive && secondaryContactActive)
            DrawPinchVector(sceneCamera);
    }

    /// <summary>
    /// Renders a touch marker at the provided screen position.
    /// </summary>
    private void DrawTouchGizmo(Camera camera, Vector2 screenPosition, Color color)
    {
        Vector3 worldPoint = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, debugGizmoDepth));
        Gizmos.color = color;
        Gizmos.DrawSphere(worldPoint, GizmoSphereRadius);
    }

    /// <summary>
    /// Renders the pinch vector connecting the two touches.
    /// </summary>
    private void DrawPinchVector(Camera camera)
    {
        Vector3 primaryPoint = camera.ScreenToWorldPoint(new Vector3(primaryCurrentPosition.x, primaryCurrentPosition.y, debugGizmoDepth));
        Vector3 secondaryPoint = camera.ScreenToWorldPoint(new Vector3(secondaryCurrentPosition.x, secondaryCurrentPosition.y, debugGizmoDepth));
        Gizmos.color = PinchGizmoColor;
        Gizmos.DrawLine(primaryPoint, secondaryPoint);
        Gizmos.DrawSphere((primaryPoint + secondaryPoint) * 0.5f, GizmoLineWidth);
    }

    #endregion
    #endregion
}
