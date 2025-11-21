using UnityEngine;

namespace CrossDeviceScalability
{
    /// <summary>
    /// Provides reference data used to normalize the play area across devices.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayAreaProfile", menuName = "Scriptables/CrossDeviceScalability/Play Area Profile")]
    public class PlayAreaProfile : ScriptableObject
    {
        #region Nested Types
        public enum OrientationPreference
        {
            Landscape = 0,
            Portrait = 1
        }
        #endregion

        #region Serialized Data
        [Tooltip("Reference resolution used as the canonical play area size.")]
        [SerializeField] private Vector2Int referenceResolution = new Vector2Int(1920, 1080);

        [Tooltip("Orientation enforced when fitting the play area.")]
        [SerializeField] private OrientationPreference orientationPreference = OrientationPreference.Landscape;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the orientation-adjusted reference resolution.
        /// </summary>
        public Vector2Int ReferenceResolution
        {
            get
            {
                Vector2Int normalizedResolution = referenceResolution;

                if (normalizedResolution.x <= 0)
                    normalizedResolution.x = 1;

                if (normalizedResolution.y <= 0)
                    normalizedResolution.y = 1;

                if (orientationPreference == OrientationPreference.Landscape && normalizedResolution.x < normalizedResolution.y)
                    normalizedResolution = new Vector2Int(normalizedResolution.y, normalizedResolution.x);

                if (orientationPreference == OrientationPreference.Portrait && normalizedResolution.x > normalizedResolution.y)
                    normalizedResolution = new Vector2Int(normalizedResolution.y, normalizedResolution.x);

                return normalizedResolution;
            }
        }

        /// <summary>
        /// Gets the target aspect ratio derived from the reference resolution and orientation.
        /// </summary>
        public float TargetAspect
        {
            get
            {
                Vector2Int orientedResolution = ReferenceResolution;
                return (float)orientedResolution.x / orientedResolution.y;
            }
        }

        /// <summary>
        /// Gets the target orientation preference.
        /// </summary>
        public OrientationPreference Orientation
        {
            get
            {
                return orientationPreference;
            }
        }
        #endregion

        #region Unity Events
        /// <summary>
        /// Clamps serialized data to valid ranges.
        /// </summary>
        private void OnValidate()
        {
            if (referenceResolution.x < 1)
                referenceResolution.x = 1;

            if (referenceResolution.y < 1)
                referenceResolution.y = 1;
        }
        #endregion
    }
}
