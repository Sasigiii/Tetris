using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class UIButton : Button
{
    [SerializeField] private float scaleX = 0.9f;
    [SerializeField] private float scaleY = 0.9f;
    [SerializeField] private float duration = 0.1f;

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);

        if (!IsInteractable())
            return;

        transform.DOKill();
        transform.DOScale(new Vector3(scaleX, scaleY, 1f), duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);

        transform.DOKill();
        transform.DOScale(Vector3.one, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }
}
