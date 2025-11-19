using UnityEngine;

public abstract class APooInterface<T> : APoolDataStructure<T> where T : MonoBehaviour, IPoolable<T>
{
    public override void BindPoolable(T poolable) => poolable.Despawn += Despawn;
    public override void ResetPoolable(T poolable) => poolable.ResetPoolable();
    public T Spawn()
    {
        T poolable = GetPoolable;
        poolable.OnSpawn();
        poolable.gameObject.SetActive(true);
        return poolable;
    }
}

public abstract class APooInterface<T, T1> : APooInterface<T> where T : MonoBehaviour, IPoolable<T, T1>
{
    public T Spawn(T1 param1)
    {
        T poolable = GetPoolable;
        poolable.OnSpawn(param1);
        poolable.gameObject.SetActive(true);
        return poolable;
    }
}

public abstract class APooInterface<T, T1, T2> : APooInterface<T, T1> where T : MonoBehaviour, IPoolable<T, T1, T2>
{
    public T Spawn(T1 param1, T2 param2)
    {
        T poolable = GetPoolable;
        poolable.OnSpawn(param1, param2);
        poolable.gameObject.SetActive(true);
        return poolable;
    }
}