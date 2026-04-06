using TMPro;
using UnityEngine;

public class LevelUIView : BaseView, IPoolable
{
    public UIButton confirmBtn;
    public GameObject icon;
    public TextMeshProUGUI title;

    public void OnPoolGet()
    {
        confirmBtn.onClick.RemoveAllListeners();
        confirmBtn.interactable = true;
        icon.SetActive(false);
        title.text = string.Empty;
    }

    public void OnPoolRelease()
    {
        confirmBtn.onClick.RemoveAllListeners();
    }
}
