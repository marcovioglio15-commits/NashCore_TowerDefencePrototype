using UnityEngine;

/// <summary>
/// Defines the reference play area resolution and fitting behavior.
/// This asset is used by PlayAreaCameraFitter to keep gameplay visually identical across devices.
/// </summary>
[CreateAssetMenu(fileName = "PlayAreaSO", menuName = "Scriptables/Screen Scaling/Play Area Definition")]
public class PlayAreaSO : ScriptableObject
{
    #region Variables And Properties
    #region Reference Resolution
    [Tooltip("Reference width of the virtual play area. Every device will adapt to preserve this view.")]
    public float referenceWidth = 1920f; 

    [Tooltip("Reference height of the virtual play area. Every device will adapt to preserve this view.")]
    public float referenceHeight = 1080f; 
    #endregion

    #region Behavior
    [Tooltip("If enabled, the target aspect ratio is preserved using letterbox or pillarbox.")]
    public bool forceStrictPlayArea = true; 

    [Tooltip("If enabled, the camera will re-evaluate fitting on orientation or resolution change.")]
    public bool allowRuntimeRecalculation = true; 

    [Tooltip("Minimum delay (seconds) between runtime checks to avoid unnecessary overhead.")]
    public float resolutionCheckInterval = 0.75f;
    #endregion

    #region Readonly Properties
    /// <summary>
    /// Returns the target aspect ratio (width divided by height).
    /// </summary>
    public float ReferenceAspect
    {
        get
        {
            float aspect = referenceWidth / referenceHeight;
            return aspect;
        }
    }
    #endregion
    #endregion
}
