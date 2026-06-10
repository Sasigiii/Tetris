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
        public LinkedListNode<PoolEntry> GlobalNode; // 在全局LRU链表中的节点
        public LinkedListNode<PoolEntry> BucketNode; // 在所属Bucket空闲链表中的节点
    }

    private class PoolBucket
    {
        public GameObject Prefab; // 预制体
        public Transform Parent; // 父物体
        public int MinRetain; // 最小保留数量，TrimExcess时不会销毁少于这个数量的空闲对象
        public readonly LinkedList<PoolEntry> IdleList = new LinkedList<PoolEntry>(); // 空闲对象链表，越靠前越新
        public readonly HashSet<GameObject> Active = new HashSet<GameObject>(); // 活跃对象集合
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
    
    /// <summary>
    /// 获取池中物体
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public GameObject Get(string key)
    {
        // 先找对应的桶
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            Debug.LogError($"[GlobalLRUPool] Key not registered: {key}");
            return null;
        }
        
        GameObject go;
        // 桶中空闲列表还有对象，取出最前面（最近使用）的一个
        if (bucket.IdleList.Count > 0)
        {
            var bucketNode = bucket.IdleList.First;
            var entry = bucketNode.Value;
            go = entry.Go;
            // 从桶的空闲列表和全局LRU链表中移除
            bucket.IdleList.RemoveFirst();
            _globalLru.Remove(entry.GlobalNode);
            _idleCount--; // 全局空闲对象数量减少
        }
        else
        {
            // 不够就实例化一个新的
            go = Object.Instantiate(bucket.Prefab, bucket.Parent);
        }

        go.SetActive(true);
        // 加入桶的活跃集合
        bucket.Active.Add(go);

        var poolable = go.GetComponent<IPoolable>();
        poolable?.OnPoolGet();

        return go;
    }

    /// <summary>
    /// 释放物体回池
    /// </summary>
    /// <param name="go"></param>
    public void Release(GameObject go)
    {
        if (go == null) return;
        
        // 先获取物体上的IPoolable组件，拿到池的Key
        var poolable = go.GetComponent<IPoolable>();
        if (poolable == null) return;

        string key = poolable.PoolKey;
        // 根据Key找到对应的桶，检查物体是否在活跃集合中
        if (!_buckets.TryGetValue(key, out var bucket)) return;
        if (!bucket.Active.Remove(go)) return;
        // 如果在活跃集合中，调用IPoolable.OnPoolRelease()，并将物体加入桶的空闲列表和全局LRU链表
        poolable.OnPoolRelease();
        go.SetActive(false);
        
        // 创建一个新的PoolEntry，并同时加入全局LRU链表和桶的空闲链表
        var entry = new PoolEntry { Go = go, Key = key };
        entry.GlobalNode = _globalLru.AddFirst(entry);
        entry.BucketNode = bucket.IdleList.AddFirst(entry);
        _idleCount++; // 全局空闲对象数量增加
        // 执行容量裁剪
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
    
    /// <summary>
    /// 容量裁剪
    /// </summary>
    private void TrimExcess()
    {
        // 从全局LRU链表的末尾开始裁剪，直到空闲对象数量不超过全局容量
        var node = _globalLru.Last;
        while (_idleCount > _globalCapacity && node != null)
        {
            // 获取当前节点的PoolEntry
            var entry = node.Value;
            var prev = node.Previous; // 提前获取前一个节点，因为当前节点可能会被移除
            // 根据PoolEntry中的Key找到对应的桶
            if (!_buckets.TryGetValue(entry.Key, out var bucket))
            {
                // 如果找不到桶，说明数据结构不一致，直接从全局LRU链表中移除这个节点，并继续裁剪
                _globalLru.Remove(node);
                _idleCount--;
                node = prev; // 将当前node设置为前一个节点，继续下一轮裁剪
                continue;
            }
            // 如果找到桶，检查桶的空闲列表数量是否超过最小保留数量，如果没有超过，说明这个桶的空闲对象已经很少了，不适合被裁剪，跳过这个节点继续裁剪
            if (bucket.IdleList.Count <= bucket.MinRetain)
            {
                node = prev; // 将当前node设置为前一个节点，继续下一轮裁剪
                continue;
            }
            // 将当前结点从全局LRU链表和桶的空闲链表中移除，并销毁这个对象，空闲对象数量减少1，然后继续裁剪
            _globalLru.Remove(node);
            bucket.IdleList.Remove(entry.BucketNode);
            Object.Destroy(entry.Go);
            _idleCount--;
            // 将当前node设置为前一个节点，继续下一轮裁剪
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
