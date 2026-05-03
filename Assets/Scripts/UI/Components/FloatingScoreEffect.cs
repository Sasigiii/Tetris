using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class FloatingScoreEffect : MonoBehaviour
{
    public enum PopupStyle
    {
        AutoBySign = 0,
        ComboBonusGold = 1
    }

    private static readonly Color GreenColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    private static readonly Color RedColor = new Color(1f, 0.392f, 0f, 1f);
    private static readonly Color DefaultComboGoldColor = new Color(1f, 0.84f, 0.2f, 1f);

    private const float FloatDistance = 60f;
    private const float FadeInDuration = 0.15f;
    private const float FloatDuration = 0.6f;
    private const float FadeOutDuration = 0.3f;
    private const int PoolCapacity = 5;

    private LRUObjectPool _pool;
    private Vector2 _originPos;
    private bool _initialized;
    private readonly List<GameObject> _playing = new List<GameObject>();
    private Color _comboBonusColor = DefaultComboGoldColor;

    public void SetComboBonusColor(Color color)
    {
        _comboBonusColor = color;
    }

    public void Init(TextMeshProUGUI template)
    {
        if (template == null) return;

        _originPos = template.rectTransform.anchoredPosition;
        template.alpha = 0f;
        template.gameObject.SetActive(false);

        if (template.GetComponent<FloatingScoreItem>() == null)
        {
            var item = template.gameObject.AddComponent<FloatingScoreItem>();
            item.SetOriginPos(_originPos);
        }

        var parent = template.transform.parent;
        _pool = new LRUObjectPool(template.gameObject, parent, PoolCapacity);
        _initialized = true;
    }

    public void Play(int delta)
    {
        Play(delta, PopupStyle.AutoBySign);
    }

    public void Play(int delta, PopupStyle style)
    {
        if (!_initialized || delta == 0) return;

        var go = _pool.Get();
        _playing.Add(go);

        var item = go.GetComponent<FloatingScoreItem>();
        item.SetOriginPos(_originPos);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.alpha = 1f;

        bool isPositive = delta > 0;
        tmp.text = isPositive ? $"+{delta}" : $"{delta}";
        tmp.color = ResolvePopupColor(delta, style);

        var rt = tmp.rectTransform;
        var seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => tmp.alpha, x => tmp.alpha = x, 1f, FadeInDuration));
        seq.Join(rt.DOAnchorPosY(_originPos.y + FloatDistance, FloatDuration).SetEase(Ease.OutCubic));
        seq.Insert(FloatDuration - FadeOutDuration,
            DOTween.To(() => tmp.alpha, x => tmp.alpha = x, 0f, FadeOutDuration));
        seq.OnComplete(() =>
        {
            _playing.Remove(go);
            _pool.Release(go);
        });
    }

    private Color ResolvePopupColor(int delta, PopupStyle style)
    {
        if (style == PopupStyle.ComboBonusGold)
            return _comboBonusColor;
        return delta >= 0 ? GreenColor : RedColor;
    }

    public void Cleanup()
    {
        for (int i = _playing.Count - 1; i >= 0; i--)
        {
            var go = _playing[i];
            if (go != null)
                _pool.Release(go);
        }
        _playing.Clear();
    }

    private void OnDestroy()
    {
        Cleanup();
        _pool?.Clear();
    }
}
