using TMPro;
using UnityEngine;

public class WordHintItemView : MonoBehaviour, IPoolable
{
    public TextMeshProUGUI wordTMP;

    public void OnPoolGet()
    {
        if (wordTMP != null)
            wordTMP.text = string.Empty;
    }

    public void OnPoolRelease()
    {
    }
}
