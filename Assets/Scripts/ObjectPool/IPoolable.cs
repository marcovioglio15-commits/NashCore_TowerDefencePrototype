using System;

public interface IPoolable<T>
{
    public Action<T> Despawn { get; set; }
    public void OnDespawn();//rimetti l oggeto in pool
    public T OnSpawn();
    public void ResetPoolable();//resetti l oggeto
}
public interface IPoolable<T, T1> : IPoolable<T>
{
    public T OnSpawn(T1 param1);
}
public interface IPoolable<T, T1, T2> : IPoolable<T, T1>
{
    public T OnSpawn(T1 param1, T2 param2);
}