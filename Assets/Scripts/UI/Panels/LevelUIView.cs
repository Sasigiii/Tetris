using TMPro;
using UnityEngine;

public class LevelUIView : BaseView, IPoolable
{
    public string PoolKey => "level-item";

    public UIButton confirmBtn;
    public GameObject icon;
    public TextMeshProUGUI title;
    public GameObject star;
    public TextMeshProUGUI countTMP;

    public void OnPoolGet()
    {
        confirmBtn.onClick.RemoveAllListeners();
        confirmBtn.interactable = true;
        icon.SetActive(false);
        title.text = string.Empty;
        if (star != null) star.SetActive(false);
        if (countTMP != null) countTMP.text = string.Empty;
    }

    public void OnPoolRelease()
    {
        confirmBtn.onClick.RemoveAllListeners();
    }
}
