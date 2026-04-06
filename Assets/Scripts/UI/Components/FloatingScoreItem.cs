using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FloatingScoreItem : MonoBehaviour, IPoolable
{
    private TextMeshProUGUI _tmp;
    private Vector2 _originPos;
    private bool _posInitialized;

    public void SetOriginPos(Vector2 pos)
    {
        _originPos = pos;
        _posInitialized = true;
    }

    public void OnPoolGet()
    {
        if (_tmp == null)
            _tmp = GetComponent<TextMeshProUGUI>();

        _tmp.alpha = 0f;
        if (_posInitialized)
            _tmp.rectTransform.anchoredPosition = _originPos;
    }

    public void OnPoolRelease()
    {
        transform.DOKill();
    }
}
