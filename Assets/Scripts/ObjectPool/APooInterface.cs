using UnityEngine;

/// <summary>
/// Provides pooled spawn helpers that bind callbacks and enforce activation order.
/// </summary>
public abstract class APooInterface<T> : APoolDataStructure<T> where T : MonoBehaviour, IPoolable<T>
{
    #region Overrides
    public override void BindPoolable(T poolable) => poolable.Despawn += Despawn;
    public override void ResetPoolable(T poolable) => poolable.ResetPoolable();
    #endregion

    #region Spawning
    /// <summary>
    /// Activates the instance before invoking OnSpawn to allow coroutines and Unity callbacks.
    /// </summary>
    public T Spawn()
    {
        T poolable = GetPoolable;
        poolable.gameObject.SetActive(true);
        poolable.OnSpawn();
        return poolable;
    }
    #endregion
}

/// <summary>
/// Pooled spawn helper with one contextual parameter.
/// </summary>
public abstract class APooInterface<T, T1> : APooInterface<T> where T : MonoBehaviour, IPoolable<T, T1>
{
    #region Spawning
    /// <summary>
    /// Activates the instance before invoking OnSpawn with a single parameter.
    /// </summary>
    public T Spawn(T1 param1)
    {
        T poolable = GetPoolable;
        poolable.gameObject.SetActive(true);
        poolable.OnSpawn(param1);
        return poolable;
    }
    #endregion
}

/// <summary>
/// Pooled spawn helper with two contextual parameters.
/// </summary>
public abstract class APooInterface<T, T1, T2> : APooInterface<T, T1> where T : MonoBehaviour, IPoolable<T, T1, T2>
{
    #region Spawning
    /// <summary>
    /// Activates the instance before invoking OnSpawn with two parameters.
    /// </summary>
    public T Spawn(T1 param1, T2 param2)
    {
        T poolable = GetPoolable;
        poolable.gameObject.SetActive(true);
        poolable.OnSpawn(param1, param2);
        return poolable;
    }
    #endregion
}
