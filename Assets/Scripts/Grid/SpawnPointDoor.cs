using System.Collections;
using UnityEngine;

namespace Grid
{
    /// <summary>
    /// Animates a sliding door anchored to an enemy spawn point.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpawnPointDoor : MonoBehaviour
    {
        #region Variables And Properties
        #region Serialized Fields
        [Header("Door Parts")]
        [Tooltip("Transform translated during the slide animation; defaults to this transform when unset.")]
        [SerializeField] private Transform slidingTransform;

        [Header("Motion")]
        [Tooltip("Local direction applied while opening the door; normalized internally.")]
        [SerializeField] private Vector3 slideDirection = Vector3.forward;
        [Tooltip("Local distance travelled when the door is fully open.")]
        [SerializeField] private float slideDistance = 1.5f;
        [Tooltip("Seconds required to complete an open or close animation.")]
        [SerializeField] private float slideDurationSeconds = 0.55f;

        [Header("Debug")]
        [Tooltip("Draws gizmos for closed and open positions when selected.")]
        [SerializeField] private bool drawDebugGizmos = true;
        #endregion

        #region Runtime
        private Vector3 closedLocalPosition;
        private Coroutine slideRoutine;
        private bool initialized;
        #endregion
        #endregion

        #region Methods
        #region Unity
        /// <summary>
        /// Caches the closed position once on startup.
        /// </summary>
        private void Awake()
        {
            InitializeClosedPosition();
        }

        /// <summary>
        /// Stops any pending animations when disabled.
        /// </summary>
        private void OnDisable()
        {
            if (slideRoutine != null)
            {
                StopCoroutine(slideRoutine);
                slideRoutine = null;
            }
        }

        /// <summary>
        /// Keeps serialized values in safe ranges while editing.
        /// </summary>
        private void OnValidate()
        {
            NormalizeDirection();
            ClampDurations();
            EnsureSlidingTransform();
            if (initialized)
                closedLocalPosition = ResolveSlidingTransform().localPosition;
        }

        /// <summary>
        /// Draws the closed position and the open offset vector for clarity.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos)
                return;

            Transform targetTransform = ResolveSlidingTransform();
            Vector3 closedPosition = Application.isPlaying && initialized ? closedLocalPosition : targetTransform.localPosition;
            Vector3 openPosition = closedPosition + GetNormalizedDirection() * slideDistance;

            Transform referenceSpace = targetTransform.parent != null ? targetTransform.parent : targetTransform;
            Vector3 closedWorld = referenceSpace.TransformPoint(closedPosition);
            Vector3 openWorld = referenceSpace.TransformPoint(openPosition);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(closedWorld, openWorld);
            Gizmos.DrawWireCube(openWorld, Vector3.one * 0.15f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(closedWorld, Vector3.one * 0.15f);
        }
        #endregion

        #region Public
        /// <summary>
        /// Begins sliding the door towards its open position.
        /// </summary>
        public void OpenDoor()
        {
            InitializeClosedPosition();
            Vector3 target = closedLocalPosition + GetNormalizedDirection() * slideDistance;
            SlideTo(target);
        }

        /// <summary>
        /// Begins sliding the door back to its closed position.
        /// </summary>
        public void CloseDoor()
        {
            InitializeClosedPosition();
            SlideTo(closedLocalPosition);
        }
        #endregion

        #region Internal
        /// <summary>
        /// Stores the current local position of the sliding transform as the closed reference.
        /// </summary>
        private void InitializeClosedPosition()
        {
            if (initialized)
                return;

            EnsureSlidingTransform();
            closedLocalPosition = ResolveSlidingTransform().localPosition;
            initialized = true;
        }

        /// <summary>
        /// Starts an animation towards the provided target.
        /// </summary>
        private void SlideTo(Vector3 targetLocalPosition)
        {
            Transform targetTransform = ResolveSlidingTransform();
            if (slideRoutine != null)
                StopCoroutine(slideRoutine);

            if (slideDurationSeconds <= 0.001f)
            {
                targetTransform.localPosition = targetLocalPosition;
                return;
            }

            if (targetTransform.localPosition == targetLocalPosition)
                return;

            slideRoutine = StartCoroutine(SlideRoutine(targetTransform, targetLocalPosition, slideDurationSeconds));
        }

        /// <summary>
        /// Performs the slide over time without per-frame polling when idle.
        /// </summary>
        private IEnumerator SlideRoutine(Transform targetTransform, Vector3 targetLocalPosition, float duration)
        {
            float elapsed = 0f;
            Vector3 initialPosition = targetTransform.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                targetTransform.localPosition = Vector3.Lerp(initialPosition, targetLocalPosition, t);
                yield return null;
            }

            targetTransform.localPosition = targetLocalPosition;
            slideRoutine = null;
        }

        /// <summary>
        /// Ensures the sliding transform is assigned, defaulting to this transform.
        /// </summary>
        private void EnsureSlidingTransform()
        {
            if (slidingTransform == null)
                slidingTransform = transform;
        }

        /// <summary>
        /// Returns a normalized direction with a safe fallback.
        /// </summary>
        private Vector3 GetNormalizedDirection()
        {
            if (slideDirection == Vector3.zero)
                return Vector3.forward;

            return slideDirection.normalized;
        }

        /// <summary>
        /// Keeps the slide duration above zero to avoid invalid animations.
        /// </summary>
        private void ClampDurations()
        {
            if (slideDurationSeconds < 0.01f)
                slideDurationSeconds = 0.01f;
        }

        /// <summary>
        /// Prevents zero-length directions for the motion vector.
        /// </summary>
        private void NormalizeDirection()
        {
            if (slideDirection == Vector3.zero)
                slideDirection = Vector3.forward;
        }

        /// <summary>
        /// Returns the assigned sliding transform.
        /// </summary>
        private Transform ResolveSlidingTransform()
        {
            return slidingTransform != null ? slidingTransform : transform;
        }
        #endregion
        #endregion
    }
}
