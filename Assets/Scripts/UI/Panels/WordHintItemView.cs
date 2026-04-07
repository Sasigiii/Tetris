using UnityEngine;
using UnityEngine.UI;

public class WordHintItemView : MonoBehaviour, IPoolable
{
    public Text wordText;

    public void OnPoolGet()
    {
        if (wordText != null)
            wordText.text = string.Empty;
    }

    public void OnPoolRelease()
    {
    }
}
