using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class InfiniteScrollView : MonoBehaviour
{
    [SerializeField] private GameObject _itemPrefab;
    [SerializeField] private float _itemHeight = 100f;
    [SerializeField] private int _bufferCount = 2;
    [SerializeField] private int _poolCapacity = 30;

    private ScrollRect _scrollRect;
    private RectTransform _content;
    private float _viewportHeight;

    private LRUObjectPool _pool;
    private int _totalCount;
    private Action<int, GameObject> _onItemRender;
    private readonly Dictionary<int, GameObject> _activeItems = new Dictionary<int, GameObject>();

    private int _lastStartIndex = -1;
    private int _lastEndIndex = -1;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        _content = _scrollRect.content;
    }

    public void Initialize(int totalCount, Action<int, GameObject> onItemRender)
    {
        _totalCount = totalCount;
        _onItemRender = onItemRender;
        _viewportHeight = ((RectTransform)_scrollRect.viewport ?? (RectTransform)transform).rect.height;

        _pool?.Clear();
        _activeItems.Clear();
        _lastStartIndex = -1;
        _lastEndIndex = -1;

        int visibleCount = Mathf.CeilToInt(_viewportHeight / _itemHeight);
        int poolCap = Mathf.Max(_poolCapacity, visibleCount + 2 * _bufferCount + 10);
        _pool = new LRUObjectPool(_itemPrefab, _content, poolCap);

        var sizeDelta = _content.sizeDelta;
        sizeDelta.y = _totalCount * _itemHeight;
        _content.sizeDelta = sizeDelta;

        RefreshVisibleItems();
    }

    public void Refresh(int newTotalCount)
    {
        var keys = new List<int>(_activeItems.Keys);
        foreach (int idx in keys)
        {
            _pool.Release(_activeItems[idx]);
        }
        _activeItems.Clear();
        _lastStartIndex = -1;
        _lastEndIndex = -1;

        _totalCount = newTotalCount;

        var sizeDelta = _content.sizeDelta;
        sizeDelta.y = _totalCount * _itemHeight;
        _content.sizeDelta = sizeDelta;

        RefreshVisibleItems();
    }

    private void RefreshVisibleItems()
    {
        if (_pool == null || _totalCount <= 0)
            return;

        float scrollOffset = _content.anchoredPosition.y;
        int startIndex = Mathf.FloorToInt(scrollOffset / _itemHeight) - _bufferCount;
        int endIndex = startIndex + Mathf.CeilToInt(_viewportHeight / _itemHeight) + 2 * _bufferCount;

        startIndex = Mathf.Clamp(startIndex, 0, _totalCount - 1);
        endIndex = Mathf.Clamp(endIndex, 0, _totalCount - 1);

        if (startIndex == _lastStartIndex && endIndex == _lastEndIndex)
            return;

        _lastStartIndex = startIndex;
        _lastEndIndex = endIndex;

        var toRecycle = new List<int>();
        foreach (var kvp in _activeItems)
        {
            if (kvp.Key < startIndex || kvp.Key > endIndex)
                toRecycle.Add(kvp.Key);
        }
        foreach (int idx in toRecycle)
        {
            _pool.Release(_activeItems[idx]);
            _activeItems.Remove(idx);
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (_activeItems.ContainsKey(i))
                continue;

            var go = _pool.Get();
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -i * _itemHeight);
            }

            _onItemRender?.Invoke(i, go);
            _activeItems[i] = go;
        }
    }

    private void OnScrollValueChanged(Vector2 _)
    {
        RefreshVisibleItems();
    }

    private void OnEnable()
    {
        if (_scrollRect != null)
            _scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
    }

    private void OnDisable()
    {
        if (_scrollRect != null)
            _scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    private void OnDestroy()
    {
        _pool?.Clear();
    }
}
