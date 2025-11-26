using UnityEngine;

namespace Scriptables.Enemies
{
    /// <summary>
    /// Configures navigation smoothing, contact pauses, and animation synchronization for enemies.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyMovementSettings", menuName = "Scriptables/Enemies/Enemy Movement Settings")]
    public class EnemyMovementSettings : ScriptableObject
    {
        #region Variables And Properties
        #region Serialized Fields

        [Tooltip("Units per second applied when no movement speed is provided by active stats.")]
        [SerializeField] private float fallbackSpeed = 2.5f;

        [Tooltip("Minimum distance required to consider a waypoint reached.")]
        [SerializeField] private float waypointTolerance = 0.05f;

        [Tooltip("Smoothing factor applied to positional interpolation for clean travel.")]
        [SerializeField] private float positionLerpSpeed = 8f;

        [Tooltip("Degrees per second used to align the orientation target to the travel direction.")]
        [SerializeField] private float rotationLerpSpeed = 480f;

        [Tooltip("Seconds the enemy waits after applying contact effects before resuming travel.")]
        [SerializeField] private float contactStopDuration = 0.6f;

    [Tooltip("Multiplier applied to animator playback speed to match travel velocity.")]
    [SerializeField] private float animationSpeedFactor = 0.35f;

    [Tooltip("Animator float parameter updated with normalized movement speed.")]
    [SerializeField] private string animationSpeedParameter = "Speed";

    [Header("Avoidance")]

    [Tooltip("Radius used to detect another enemy occupying the next waypoint.")]
    [SerializeField] private float occupancyProbeRadius = 0.35f;

    [Tooltip("Layer mask used to detect other enemies while probing occupancy.")]
    [SerializeField] private LayerMask occupancyLayerMask = ~0;

    [Tooltip("Draws the cached navigation path when the enemy is selected.")]
    [SerializeField] private bool drawPathGizmos = true;

    [Tooltip("Color used when rendering the navigation gizmo line.")]
        [SerializeField] private Color pathGizmoColor = new Color(0.25f, 0.8f, 1f, 0.9f);

        #endregion

        #region Properties

        public float FallbackSpeed
        {
            get { return Mathf.Max(0.01f, fallbackSpeed); }
        }

        public float WaypointTolerance
        {
            get { return Mathf.Max(0.01f, waypointTolerance); }
        }

        public float PositionLerpSpeed
        {
            get { return Mathf.Max(0.01f, positionLerpSpeed); }
        }

        public float RotationLerpSpeed
        {
            get { return Mathf.Max(0.01f, rotationLerpSpeed); }
        }

        public float ContactStopDuration
        {
            get { return Mathf.Max(0f, contactStopDuration); }
        }

        public float AnimationSpeedFactor
        {
            get { return Mathf.Max(0f, animationSpeedFactor); }
        }

        public string AnimationSpeedParameter
        {
            get { return animationSpeedParameter; }
        }

    public bool DrawPathGizmos
    {
        get { return drawPathGizmos; }
    }

    public Color PathGizmoColor
    {
        get { return pathGizmoColor; }
    }

    public float OccupancyProbeRadius
    {
        get { return Mathf.Max(0f, occupancyProbeRadius); }
    }

    public int OccupancyLayerMask
    {
        get { return occupancyLayerMask; }
    }

    #endregion
    #endregion
}
}
