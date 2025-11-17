using UnityEngine;

namespace UnityExtensions
{
    // Extension methods for UnityEngine.GameObject.
    public static class GameObjectExtensions
    {
        // Gets a component attached to the given game object.
        // If one isn't found, a new one is attached and returned.
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (!gameObject.TryGetComponent<T>(out var attachedComponent))
            {
                attachedComponent = gameObject.AddComponent<T>();
            }

            return attachedComponent;
        }

        // Checks whether a game object has a component of type T attached.
        public static bool HasComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out _);
        }

        // Checks if a game object's layer is part of a camera's culling mask.
        public static bool IsInCullingMask(this GameObject gameObject, LayerMask cullingMask)
        {
            return (cullingMask & (1 << gameObject.layer)) != 0;
        }
    }
}
