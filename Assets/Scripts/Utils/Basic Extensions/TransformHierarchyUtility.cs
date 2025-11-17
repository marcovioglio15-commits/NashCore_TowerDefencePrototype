using UnityEngine;

/// <summary>
/// Provides helper routines for working with transform hierarchies in runtime resolution code.
/// </summary>
public static class TransformHierarchyUtility
{
    /// <summary>
    /// Returns true when the supplied transforms are the same object or share any ancestor/descendant relationship.
    /// </summary>
    public static bool ShareHierarchy(Transform first, Transform second)
    {
        if (first == null || second == null)
            return false;

        if (first == second)
            return true;

        if (first.IsChildOf(second))
            return true;

        if (second.IsChildOf(first))
            return true;

        return false;
    }

    /// <summary>
    /// Traverses ancestors of the provided origin to locate the closest component of type T.
    /// </summary>
    public static T GetAncestorComponent<T>(Transform origin) where T : Component
    {
        if (origin == null)
            return null;

        // Skip the origin itself because the lookup must only consider ancestors.
        Transform current = origin.parent;
        while (current != null)
        {
            T component = current.GetComponent<T>();
            if (component != null)
                return component;

            current = current.parent;
        }

        return null;
    }

    /// <summary>
    /// Returns the top-most parent in the hierarchy for the provided origin, or null if already root.
    /// </summary>
    public static Transform GetRootParent(Transform origin)
    {
        if (origin == null)
            return null;

        Transform current = origin.parent;
        Transform root = null;

        while (current != null)
        {
            root = current;
            current = current.parent;
        }

        return root;
    }
}
