using System.Collections.Generic;
using UnityEngine;

public class LRUObjectPool
{
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly int _capacity;
    private readonly LinkedList<GameObject> _pool = new LinkedList<GameObject>();
    private readonly HashSet<GameObject> _active = new HashSet<GameObject>();

    public LRUObjectPool(GameObject prefab, Transform parent, int capacity = 20)
    {
        _prefab = prefab;
        _parent = parent;
        _capacity = capacity;
    }

    public GameObject Get()
    {
        GameObject go;

        if (_pool.Count > 0)
        {
            go = _pool.First.Value;
            _pool.RemoveFirst();
        }
        else
        {
            go = Object.Instantiate(_prefab, _parent);
        }

        go.SetActive(true);
        _active.Add(go);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null || !_active.Remove(go))
            return;

        go.SetActive(false);
        _pool.AddFirst(go);

        while (_pool.Count > _capacity)
        {
            var last = _pool.Last.Value;
            _pool.RemoveLast();
            Object.Destroy(last);
        }
    }

    public void Clear()
    {
        foreach (var go in _pool)
        {
            if (go != null)
                Object.Destroy(go);
        }
        _pool.Clear();

        foreach (var go in _active)
        {
            if (go != null)
                Object.Destroy(go);
        }
        _active.Clear();
    }
}
