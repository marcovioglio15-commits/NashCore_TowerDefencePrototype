using UnityEngine;

namespace UnityExtensions
{
    // Extension methods for UnityEngine.Component.
    public static class ComponentExtensions
    {
        // Attaches a component to the given component's game object.
        public static T AddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.AddComponent<T>();
        }

        // Gets a component attached to the given component's game object.
        // If one isn't found, a new one is attached and returned.
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            if (!component.TryGetComponent<T>(out var attachedComponent))
            {
                attachedComponent = component.AddComponent<T>();
            }

            return attachedComponent;
        }

        // Checks whether a component's game object has a component of type T attached.
        public static bool HasComponent<T>(this Component component) where T : Component
        {
            return component.TryGetComponent<T>(out _);
        }
    }
}