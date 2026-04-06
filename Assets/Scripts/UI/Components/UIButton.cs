using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class UIButton : Button
{
    private const string DefaultClickSfx = "UI_Button_Click";

    [SerializeField] private float scaleX = 0.9f;
    [SerializeField] private float scaleY = 0.9f;
    [SerializeField] private float duration = 0.1f;
    [SerializeField] private string clickSfx = "UI_Button_Click";

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);

        if (!IsInteractable())
            return;

        var sfx = string.IsNullOrEmpty(clickSfx) ? DefaultClickSfx : clickSfx;
        if (AudioManager.Instance != null)
        {
            float vol = AudioManager.Instance.GetEventVolume("uiClick");
            AudioManager.Instance.PlaySfxWithVolume("Audio/" + sfx, vol);
        }

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
