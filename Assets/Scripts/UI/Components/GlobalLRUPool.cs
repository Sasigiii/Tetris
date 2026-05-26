using System.Collections.Generic;
using UnityEngine;

public class GlobalLRUPool : MonoBehaviour
{
    private static GlobalLRUPool _instance;
    private static bool _appQuitting;

    [SerializeField] private int _globalCapacity = 50;

    public static GlobalLRUPool Instance
    {
        get
        {
            if (_appQuitting) return null;
            if (_instance == null)
            {
                var go = new GameObject("[GlobalLRUPool]");
                go.AddComponent<GlobalLRUPool>();
            }
            return _instance;
        }
    }

    public int GlobalCapacity
    {
        get => _globalCapacity;
        set => _globalCapacity = Mathf.Max(0, value);
    }

    #region Internal types

    private class PoolEntry
    {
        public GameObject Go;
        public string Key;
        public LinkedListNode<PoolEntry> GlobalNode;
        public LinkedListNode<PoolEntry> BucketNode;
    }

    private class PoolBucket
    {
        public GameObject Prefab;
        public Transform Parent;
        public int MinRetain;
        public readonly LinkedList<PoolEntry> IdleList = new LinkedList<PoolEntry>();
        public readonly HashSet<GameObject> Active = new HashSet<GameObject>();
    }

    #endregion

    private readonly LinkedList<PoolEntry> _globalLru = new LinkedList<PoolEntry>();
    private readonly Dictionary<string, PoolBucket> _buckets = new Dictionary<string, PoolBucket>();
    private int _idleCount;

    public void Register(string key, GameObject prefab, Transform parent, int minRetain = 0)
    {
        if (_buckets.TryGetValue(key, out var existing))
        {
            existing.Prefab = prefab;
            existing.Parent = parent;
            existing.MinRetain = minRetain;
            return;
        }

        _buckets[key] = new PoolBucket
        {
            Prefab = prefab,
            Parent = parent,
            MinRetain = minRetain
        };
    }

    public GameObject Get(string key)
    {
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            Debug.LogError($"[GlobalLRUPool] Key not registered: {key}");
            return null;
        }

        GameObject go;

        if (bucket.IdleList.Count > 0)
        {
            var bucketNode = bucket.IdleList.First;
            var entry = bucketNode.Value;
            go = entry.Go;

            bucket.IdleList.RemoveFirst();
            _globalLru.Remove(entry.GlobalNode);
            _idleCount--;
        }
        else
        {
            go = Object.Instantiate(bucket.Prefab, bucket.Parent);
        }

        go.SetActive(true);
        bucket.Active.Add(go);

        var poolable = go.GetComponent<IPoolable>();
        poolable?.OnPoolGet();

        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        var poolable = go.GetComponent<IPoolable>();
        if (poolable == null) return;

        string key = poolable.PoolKey;
        if (!_buckets.TryGetValue(key, out var bucket)) return;
        if (!bucket.Active.Remove(go)) return;

        poolable.OnPoolRelease();
        go.SetActive(false);

        var entry = new PoolEntry { Go = go, Key = key };
        entry.GlobalNode = _globalLru.AddFirst(entry);
        entry.BucketNode = bucket.IdleList.AddFirst(entry);
        _idleCount++;

        TrimExcess();
    }

    public void ClearKey(string key)
    {
        if (!_buckets.TryGetValue(key, out var bucket)) return;

        foreach (var entry in bucket.IdleList)
        {
            _globalLru.Remove(entry.GlobalNode);
            if (entry.Go != null)
                Object.Destroy(entry.Go);
        }
        _idleCount -= bucket.IdleList.Count;
        bucket.IdleList.Clear();

        foreach (var go in bucket.Active)
        {
            if (go != null)
                Object.Destroy(go);
        }
        bucket.Active.Clear();
    }

    public void ClearAll()
    {
        foreach (var kvp in _buckets)
        {
            var bucket = kvp.Value;
            foreach (var entry in bucket.IdleList)
            {
                if (entry.Go != null)
                    Object.Destroy(entry.Go);
            }
            foreach (var go in bucket.Active)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            bucket.IdleList.Clear();
            bucket.Active.Clear();
        }

        _globalLru.Clear();
        _idleCount = 0;
    }

    private void TrimExcess()
    {
        var node = _globalLru.Last;
        while (_idleCount > _globalCapacity && node != null)
        {
            var entry = node.Value;
            var prev = node.Previous;

            if (!_buckets.TryGetValue(entry.Key, out var bucket))
            {
                _globalLru.Remove(node);
                _idleCount--;
                node = prev;
                continue;
            }

            if (bucket.IdleList.Count <= bucket.MinRetain)
            {
                node = prev;
                continue;
            }

            _globalLru.Remove(node);
            bucket.IdleList.Remove(entry.BucketNode);
            Object.Destroy(entry.Go);
            _idleCount--;

            node = prev;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        ClearAll();
        if (_instance == this)
            _instance = null;
    }

    private void OnApplicationQuit()
    {
        _appQuitting = true;
    }
}
