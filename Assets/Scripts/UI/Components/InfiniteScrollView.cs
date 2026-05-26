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

    private ScrollRect _scrollRect;
    private RectTransform _content;
    private float _viewportHeight;

    private string _poolKey;
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

        var csf = _content.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;
        var vlg = _content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false;
        var hlg = _content.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        _content.anchorMin = new Vector2(_content.anchorMin.x, 1f);
        _content.anchorMax = new Vector2(_content.anchorMax.x, 1f);
        _content.pivot = new Vector2(_content.pivot.x, 1f);

        if (!string.IsNullOrEmpty(_poolKey))
            GlobalLRUPool.Instance?.ClearKey(_poolKey);
        _activeItems.Clear();
        _lastStartIndex = -1;
        _lastEndIndex = -1;

        var poolable = _itemPrefab.GetComponent<IPoolable>();
        _poolKey = poolable != null ? poolable.PoolKey : _itemPrefab.name;
        GlobalLRUPool.Instance.Register(_poolKey, _itemPrefab, _content);

        var sizeDelta = _content.sizeDelta;
        sizeDelta.y = _totalCount * _itemHeight;
        _content.sizeDelta = sizeDelta;

        _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, 0f);
        _scrollRect.verticalNormalizedPosition = 1f;

        RefreshVisibleItems();
    }

    public void Refresh(int newTotalCount)
    {
        var keys = new List<int>(_activeItems.Keys);
        foreach (int idx in keys)
        {
            GlobalLRUPool.Instance.Release(_activeItems[idx]);
        }
        _activeItems.Clear();
        _lastStartIndex = -1;
        _lastEndIndex = -1;

        _totalCount = newTotalCount;

        var sizeDelta = _content.sizeDelta;
        sizeDelta.y = _totalCount * _itemHeight;
        _content.sizeDelta = sizeDelta;

        _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, 0f);
        _scrollRect.verticalNormalizedPosition = 1f;

        RefreshVisibleItems();
    }

    private void RefreshVisibleItems()
    {
        if (string.IsNullOrEmpty(_poolKey) || _totalCount <= 0)
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
        var pool = GlobalLRUPool.Instance;
        foreach (int idx in toRecycle)
        {
            pool.Release(_activeItems[idx]);
            _activeItems.Remove(idx);
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (_activeItems.ContainsKey(i))
                continue;

            var go = pool.Get(_poolKey);
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
                rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
                rt.pivot = new Vector2(rt.pivot.x, 1f);
                rt.anchoredPosition = new Vector2(0f, -i * _itemHeight);
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
        if (!string.IsNullOrEmpty(_poolKey))
            GlobalLRUPool.Instance?.ClearKey(_poolKey);
    }
}
