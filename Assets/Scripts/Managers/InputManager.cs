using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Centralized mobile gesture manager built on the new Input System (tap, hold, drag, swipe, pinch).
/// This version ensures EnhancedTouch is initialized once and persists across scenes,
/// fixing the issue where Touch.activeTouches becomes 0 after scene changes or when
/// starting from a non-game scene.
/// </summary>
[DefaultExecutionOrder(-200)]
public class InputManager : Singleton<InputManager>
{
    #region Variables And Properties

    #region Serialized Configuration
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

    [Header("Pinch detection")]
    [Tooltip("Minimum change in distance between the two touches to register a pinch.")]
    [SerializeField] private float pinchDistanceThreshold = 5f;
    #endregion

    #region Runtime State
    public float SwipeDistanceThreshold => swipeDistanceThreshold;
    public float SwipeTimeThreshold => swipeTimeThreshold;
    public float DragStartDistanceThreshold => dragStartDistanceThreshold;
    public float PinchDistanceThreshold => pinchDistanceThreshold;

    private bool primaryGestureActive;
    private int primaryFingerId = -1;
    private Vector2 primaryStartPosition;
    private Vector2 primaryLastPosition;
    private double primaryStartTime;
    private bool holdRaised;
    private bool swipeRaised;
    private bool dragActive;

    private bool pinchActive;
    private Vector2 pinchPreviousVector;
    #endregion

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Ensures the manager persists across scenes and initializes the touch pipeline ONCE.
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
    }

    /// <summary>
    /// Update loop always available across scenes.
    /// </summary>
    private void Update()
    {
        ProcessTouchGestures();
    }

    #endregion

    #region Public Configuration
    public void SetSwipeDistanceThreshold(float value) => swipeDistanceThreshold = Mathf.Max(0f, value);
    public void SetSwipeTimeThreshold(float value) => swipeTimeThreshold = Mathf.Max(0.01f, value);
    public void SetDragStartDistanceThreshold(float value) => dragStartDistanceThreshold = Mathf.Max(0f, value);
    public void SetPinchDistanceThreshold(float value) => pinchDistanceThreshold = Mathf.Max(0f, value);
    #endregion

    #region Update Loop

    private void ProcessTouchGestures()
    {
        if (!EnhancedTouchSupport.enabled)
            return;

        if (Touch.activeTouches.Count >= 2)
        {
            HandlePinchGesture();
            CancelPrimaryGesture();
            return;
        }

        ResetPinchState();

        if (Touch.activeTouches.Count == 0)
        {
            if (primaryGestureActive)
                FinalizePrimaryGesture();

            return;
        }

        Touch primaryTouch = GetPrimaryTouch();

        if (!primaryTouch.isInProgress)
        {
            if (primaryGestureActive)
                FinalizePrimaryGesture();

            return;
        }

        if (!primaryGestureActive)
            BeginPrimaryGesture(primaryTouch);

        UpdatePrimaryGesture(primaryTouch);
    }

    #endregion

    #region Touch Pipeline

    private Touch GetPrimaryTouch()
    {
        for (int i = 0; i < Touch.activeTouches.Count; i++)
        {
            Touch candidate = Touch.activeTouches[i];
            if (primaryGestureActive && candidate.finger.index == primaryFingerId)
                return candidate;
        }

        return Touch.activeTouches[0];
    }

    #endregion

    #region Single Finger Gestures

    private void BeginPrimaryGesture(Touch primaryTouch)
    {
        primaryGestureActive = true;
        primaryFingerId = primaryTouch.finger.index;
        primaryStartPosition = primaryTouch.screenPosition;
        primaryLastPosition = primaryStartPosition;
        primaryStartTime = Time.timeAsDouble;
        holdRaised = false;
        swipeRaised = false;
        dragActive = false;
    }

    private void UpdatePrimaryGesture(Touch primaryTouch)
    {
        Vector2 currentPosition = primaryTouch.screenPosition;
        Vector2 displacement = currentPosition - primaryStartPosition;
        double elapsed = Time.timeAsDouble - primaryStartTime;

        if (!holdRaised)
        {
            bool minimalMovement = displacement.magnitude <= dragStartDistanceThreshold;
            if (elapsed >= holdDurationThreshold && minimalMovement)
            {
                EventsManager.InvokeHoldBegan(currentPosition);
                holdRaised = true;
            }
        }

        if (!swipeRaised)
        {
            bool swipeReady = displacement.magnitude >= swipeDistanceThreshold && elapsed <= swipeTimeThreshold;
            if (swipeReady)
            {
                EventsManager.InvokeSwipe(displacement);
                swipeRaised = true;
            }
        }

        if (!dragActive && !swipeRaised && displacement.magnitude >= dragStartDistanceThreshold)
            dragActive = true;

        if (dragActive)
        {
            Vector2 frameDelta = currentPosition - primaryLastPosition;
            if (frameDelta.sqrMagnitude > 0f)
                EventsManager.InvokeDrag(frameDelta);
        }

        primaryLastPosition = currentPosition;

        if (primaryTouch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
            primaryTouch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            FinalizePrimaryGesture();
        }
    }

    private void FinalizePrimaryGesture()
    {
        Vector2 totalDisplacement = primaryLastPosition - primaryStartPosition;
        double elapsed = Time.timeAsDouble - primaryStartTime;

        if (holdRaised)
            EventsManager.InvokeHoldEnded(primaryLastPosition);

        bool tapped = !swipeRaised && !dragActive && !holdRaised &&
                      totalDisplacement.magnitude <= dragStartDistanceThreshold;

        if (tapped)
            EventsManager.InvokeTap(primaryLastPosition);

        bool qualifiesLateSwipe =
            !swipeRaised && totalDisplacement.magnitude >= swipeDistanceThreshold &&
            elapsed <= swipeTimeThreshold;

        if (qualifiesLateSwipe)
            EventsManager.InvokeSwipe(totalDisplacement);

        ResetGestureState();
    }

    private void CancelPrimaryGesture()
    {
        if (holdRaised)
            EventsManager.InvokeHoldEnded(primaryLastPosition);

        ResetGestureState();
    }

    private void ResetGestureState()
    {
        primaryGestureActive = false;
        primaryFingerId = -1;
        primaryStartPosition = Vector2.zero;
        primaryLastPosition = Vector2.zero;
        primaryStartTime = 0d;
        holdRaised = false;
        swipeRaised = false;
        dragActive = false;
    }

    #endregion

    #region Pinch Gestures

    private void HandlePinchGesture()
    {
        if (Touch.activeTouches.Count < 2)
        {
            ResetPinchState();
            return;
        }

        Touch firstTouch = Touch.activeTouches[0];
        Touch secondTouch = Touch.activeTouches[1];

        if (!firstTouch.isInProgress || !secondTouch.isInProgress)
        {
            ResetPinchState();
            return;
        }

        Vector2 currentVector = secondTouch.screenPosition - firstTouch.screenPosition;

        if (!pinchActive)
        {
            pinchPreviousVector = currentVector;
            pinchActive = true;
            return;
        }

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

    private void ResetPinchState()
    {
        pinchActive = false;
        pinchPreviousVector = Vector2.zero;
    }

    #endregion
}
