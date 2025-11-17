using System.Collections;
using UnityEngine;

namespace UnityExtensions
{
    // Extension methods for UnityEngine.Transform.
    public static class TransformExtensions
    {
        #region Position

        // Sets the position of a transform's children to zero.
        public static void ResetChildPositions(this Transform transform, bool recursive = false)
        {
            foreach (Transform child in transform)
            {
                child.position = Vector3.zero;

                if (recursive)
                {
                    child.ResetChildPositions(true);
                }
            }
        }

        // Sets the x component of the transform's position.
        public static void SetX(this Transform transform, float x)
        {
            var position = transform.position;
            transform.position = new Vector3(x, position.y, position.z);
        }

        // Sets the y component of the transform's position.
        public static void SetY(this Transform transform, float y)
        {
            var position = transform.position;
            transform.position = new Vector3(position.x, y, position.z);
        }

        // Sets the z component of the transform's position.
        public static void SetZ(this Transform transform, float z)
        {
            var position = transform.position;
            transform.position = new Vector3(position.x, position.y, z);
        }
        #endregion

        #region Layers
        // Sets the layer of the transform's children.
        public static void SetChildLayers(this Transform transform, string layerName, bool recursive = false)
        {
            var layer = LayerMask.NameToLayer(layerName);
            SetChildLayersHelper(transform, layer, recursive);
        }

        static void SetChildLayersHelper(Transform transform, int layer, bool recursive)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.layer = layer;

                if (recursive)
                {
                    SetChildLayersHelper(child, layer, true);
                }
            }
        }
        #endregion
    }
}
