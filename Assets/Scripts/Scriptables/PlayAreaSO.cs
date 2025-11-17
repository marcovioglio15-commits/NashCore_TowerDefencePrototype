using UnityEngine;

/// <summary>
/// 
/// Defines dual reference play area profiles and fitting behavior for desktop and mobile targets.
/// </summary>
[CreateAssetMenu(fileName = "PlayAreaSO", menuName = "Scriptables/Screen Scaling/Play Area Definition")]
public class PlayAreaSO : ScriptableObject
{
    #region Variables And Properties

    #region Reference Profiles
    [Header("Reference Profiles")]
    [Tooltip("Reference width for mobile devices. Used when the active profile is Mobile.")]
    [SerializeField] private int mobileReferenceWidth = 1080;

    [Tooltip("Reference height for mobile devices. Used when the active profile is Mobile.")]
    [SerializeField] private int mobileReferenceHeight = 1920;

    [Tooltip("Reference width for desktop devices. Used when the active profile is Desktop.")]
    [SerializeField] private int desktopReferenceWidth = 1920;

    [Tooltip("Reference height for desktop devices. Used when the active profile is Desktop.")]
    [SerializeField] private int desktopReferenceHeight = 1080;
    #endregion

    #region Profile Selection
    [Header("Profile Selection")]
    [Tooltip("Selects the active profile based on the detected device type (handheld vs desktop). Disable to force a profile.")]
    [SerializeField] private bool autoSelectByDeviceType = true;

    [Tooltip("Profile forced when automatic selection is disabled.")]
    [SerializeField] private DeviceProfile forcedProfile = DeviceProfile.Desktop;

    [Tooltip("Profile used for previews while in the editor when automatic selection is enabled.")]
    [SerializeField] private DeviceProfile editorPreviewProfile = DeviceProfile.Desktop;
    #endregion

    #region Behavior
    [Header("Behaviour")]
    [Tooltip("If enabled, the target aspect ratio is preserved using letterbox or pillarbox.")]
    [field:SerializeField] public bool forceStrictPlayArea { get; private set; } = true;

    [Tooltip("If enabled, the camera will re-evaluate fitting on orientation or resolution change.")]
    [field:SerializeField] public bool allowRuntimeRecalculation { get; private set; } = true;

    [Tooltip("Minimum delay (seconds) between runtime checks to avoid unnecessary overhead.")]
    [field:SerializeField] public float resolutionCheckInterval { get; private set; } = 0.75f;
    #endregion

    #region Access Properties
    /// <summary>
    /// Returns the aspect ratio of the currently active reference resolution.
    /// </summary>
    public float ActiveReferenceAspect
    {
        get
        {
            Vector2Int resolution = ActiveReferenceResolution;
            if (resolution.y <= 0)
                return 1f;

            float aspect = (float)resolution.x / resolution.y;
            return aspect;
        }
    }

    /// <summary>
    /// Returns the active reference resolution chosen for the current platform or forced profile.
    /// </summary>
    public Vector2Int ActiveReferenceResolution
    {
        get
        {
            DeviceProfile profile = ActiveProfile;
            Vector2Int resolution = GetReferenceResolution(profile);
            return resolution;
        }
    }

    /// <summary>
    /// Resolves the profile that should be used based on platform detection and configuration.
    /// </summary>
    public DeviceProfile ActiveProfile
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return autoSelectByDeviceType ? editorPreviewProfile : forcedProfile;
#endif
            if (!autoSelectByDeviceType)
                return forcedProfile;

            if (Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld)
                return DeviceProfile.Mobile;

            return DeviceProfile.Desktop;
        }
    }
    #endregion
    #endregion


    #region Private Methods
    /// <summary>
    /// Returns the configured resolution for the requested profile.
    /// </summary>
    private Vector2Int GetReferenceResolution(DeviceProfile profile)
    {
        if (profile == DeviceProfile.Mobile)
        {
            Vector2Int mobileResolution = new Vector2Int(mobileReferenceWidth, mobileReferenceHeight);
            return mobileResolution;
        }

        Vector2Int desktopResolution = new Vector2Int(desktopReferenceWidth, desktopReferenceHeight);
        return desktopResolution;
    }
    #endregion

    #region Nested Types
    public enum DeviceProfile
    {
        Mobile,
        Desktop
    }
    #endregion

}
