using System.Globalization;
using TMPro;
using UnityEngine;

public class UIManager_TuningInputs : Singleton<UIManager_TuningInputs>
{
    #region Variables And Properties
    [Header("Input Manager tuning")]
    [Tooltip("Displays the last detected input event.")]
    [SerializeField] private TextMeshProUGUI InputDetectionText;
    [Tooltip("Input field used to configure the minimum swipe distance.")]
    [SerializeField] private TMP_InputField SwipeDistanceInput;
    [Tooltip("Input field used to configure the maximum swipe time window.")]
    [SerializeField] private TMP_InputField SwipeTimeInput;
    [Tooltip("Input field used to configure the minimum drag start distance.")]
    [SerializeField] private TMP_InputField DragStartDistanceInput;
    [Tooltip("Input field used to configure the minimum pinch distance delta.")]
    [SerializeField] private TMP_InputField PinchDistanceInput;
    #endregion

    #region Methods
    #region Unity Events
    protected override void OnEnable()
    {
        InitializeAllInputDetectionEvents();
        InitializeThresholdInputs();
    }

    private void OnDisable()
    {
        ReleaseAllInputDetectionEvents();
        ReleaseThresholdInputs();
    }
    #endregion

    #region Events
    private void InitializeAllInputDetectionEvents()
    {
        EventsManager.Drag += PrintDetectedDrag;
        EventsManager.Swipe += PrintDetectedSwipe;
        EventsManager.PinchOut += PrintDetectedPinchOut;
        EventsManager.PinchIn += PrintDetectedPinchIn;
        EventsManager.HoldBegan += PrintDetectedHoldBegan;
        EventsManager.HoldEnded += PrintDetectedHoldEnded;
        EventsManager.Tap += PrintDetectedTap;
    }

    private void ReleaseAllInputDetectionEvents()
    {
        EventsManager.Drag -= PrintDetectedDrag;
        EventsManager.Swipe -= PrintDetectedSwipe;
        EventsManager.PinchOut -= PrintDetectedPinchOut;
        EventsManager.PinchIn -= PrintDetectedPinchIn;
        EventsManager.HoldBegan -= PrintDetectedHoldBegan;
        EventsManager.HoldEnded -= PrintDetectedHoldEnded;
        EventsManager.Tap -= PrintDetectedTap;
    }

    private void InitializeThresholdInputs()
    {
        InputManager inputManager = InputManager.Instance;
        if (inputManager == null) return;

        if (SwipeDistanceInput != null)
        {
            SwipeDistanceInput.text = inputManager.SwipeDistanceThreshold.ToString(CultureInfo.InvariantCulture);
            SwipeDistanceInput.onEndEdit.AddListener(OnSwipeDistanceChanged);
        }

        if (SwipeTimeInput != null)
        {
            SwipeTimeInput.text = inputManager.SwipeTimeThreshold.ToString(CultureInfo.InvariantCulture);
            SwipeTimeInput.onEndEdit.AddListener(OnSwipeTimeChanged);
        }

        if (DragStartDistanceInput != null)
        {
            DragStartDistanceInput.text = inputManager.DragStartDistanceThreshold.ToString(CultureInfo.InvariantCulture);
            DragStartDistanceInput.onEndEdit.AddListener(OnDragStartDistanceChanged);
        }

        if (PinchDistanceInput != null)
        {
            PinchDistanceInput.text = inputManager.PinchDistanceThreshold.ToString(CultureInfo.InvariantCulture);
            PinchDistanceInput.onEndEdit.AddListener(OnPinchDistanceChanged);
        }
    }

    private void ReleaseThresholdInputs()
    {
        if (SwipeDistanceInput != null)
            SwipeDistanceInput.onEndEdit.RemoveListener(OnSwipeDistanceChanged);

        if (SwipeTimeInput != null)
            SwipeTimeInput.onEndEdit.RemoveListener(OnSwipeTimeChanged);

        if (DragStartDistanceInput != null)
            DragStartDistanceInput.onEndEdit.RemoveListener(OnDragStartDistanceChanged);

        if (PinchDistanceInput != null)
            PinchDistanceInput.onEndEdit.RemoveListener(OnPinchDistanceChanged);
    }
    #region Print Input Detection
    private void PrintDetectedSwipe(Vector2 delta)
    {
        if (InputDetectionText != null)
        {
            InputDetectionText.text = $"Detected Swipe for {delta}";
            Debug.Log(InputDetectionText.text);
        }
    }

    private void PrintDetectedDrag(Vector2 delta)
    {
        if (InputDetectionText != null)
        {
            InputDetectionText.text = $"Detected Drag for {delta}";
            Debug.Log(InputDetectionText.text);
        }
    }

    private void PrintDetectedHoldBegan(Vector2 screenPosition)
    {
        if (InputDetectionText != null)
        {
            InputDetectionText.text = $"Hold began at {screenPosition}";
            Debug.Log(InputDetectionText.text);
        }
    }

    private void PrintDetectedHoldEnded(Vector2 screenPosition)
    {
        if (InputDetectionText != null)
        {
            InputDetectionText.text = $"Hold ended at {screenPosition}";
            Debug.Log(InputDetectionText.text);
        }
    }

    private void PrintDetectedTap(Vector2 screenPosition)
    {
        if (InputDetectionText != null)
        {
            InputDetectionText.text = $"Tap at {screenPosition}";
            Debug.Log(InputDetectionText.text);
        }
    }

    private void PrintDetectedPinchIn(Vector2 delta)
    {
        if (InputDetectionText != null)
            InputDetectionText.text = $"Detected PinchIn for {delta}";
    }

    private void PrintDetectedPinchOut(Vector2 delta)
    {
        if (InputDetectionText != null)
            InputDetectionText.text = $"Detected PinchOut for {delta}";
    }
    #endregion

    #region Threshold callbacks
    private void OnSwipeDistanceChanged(string value)
    {
        if (!TryParseInvariant(value, out float parsed)) return;
        InputManager.Instance?.SetSwipeDistanceThreshold(parsed);
    }

    private void OnSwipeTimeChanged(string value)
    {
        if (!TryParseInvariant(value, out float parsed)) return;
        InputManager.Instance?.SetSwipeTimeThreshold(parsed);
    }

    private void OnDragStartDistanceChanged(string value)
    {
        if (!TryParseInvariant(value, out float parsed)) return;
        InputManager.Instance?.SetDragStartDistanceThreshold(parsed);
    }

    private void OnPinchDistanceChanged(string value)
    {
        if (!TryParseInvariant(value, out float parsed)) return;
        InputManager.Instance?.SetPinchDistanceThreshold(parsed);
    }

    #endregion
    #endregion

    #region Helpers
    private static bool TryParseInvariant(string value, out float parsed) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    #endregion
    #endregion
}
