using System.Collections.Generic;
using UnityEngine;

public abstract class APoolDataStructure<T> : ScriptableObject where T : MonoBehaviour
{
    [SerializeField] private T prefab;
    [SerializeField] private Queue<T> pool;

    public T GetPoolable
    {
        get
        {
            if (pool == null)
                Initialize(1);

            T poolable = null;
            while (pool.Count > 0 && poolable == null)
            {
                poolable = pool.Dequeue();
                if (poolable == null)
                    continue;
            }

            if (poolable == null)
            {
                WarmupAdditional(1);
                if (pool.Count > 0)
                    poolable = pool.Dequeue();
                if (poolable == null)
                {
                    poolable = Instantiate(prefab);
                    BindPoolable(poolable);
                    ResetPoolable(poolable);
                    poolable.gameObject.SetActive(false);
                }
            }

            return poolable;
        }
    }

    public void Initialize(int poolSize)
    {
        if (pool != null)
            return;

        int size = Mathf.Max(1, poolSize);
        pool = new Queue<T>(size);
        for (int i = 0; i < size; i++)
        {
            T poolable = Instantiate(prefab);
            BindPoolable(poolable);
            ResetPoolable(poolable);
            poolable.gameObject.SetActive(false);
            pool.Enqueue(poolable);
        }
    }

    public void Despawn(T poolable)
    {
        if (poolable == null)
            return;

        ResetPoolable(poolable);
        poolable.gameObject.SetActive(false);
        pool.Enqueue(poolable);
    }

    // Adds more inactive instances to the pool so runtime spawns avoid allocations.
    protected void WarmupAdditional(int amount)
    {
        if (amount <= 0)
            return;

        if (pool == null)
        {
            Initialize(amount);
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            T poolable = Instantiate(prefab);
            BindPoolable(poolable);
            ResetPoolable(poolable);
            poolable.gameObject.SetActive(false);
            pool.Enqueue(poolable);
        }
    }
    public abstract void BindPoolable(T poolable);
    public abstract void ResetPoolable(T poolable);
}
