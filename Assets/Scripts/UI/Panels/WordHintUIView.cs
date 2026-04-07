using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WordHintUIView : BaseView
{
    public ScrollRect scrollRect;
    public RectTransform content;
    public GameObject itemPrefab;
    public TextMeshProUGUI countdownTMP;

    public Action onEscPressed;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            onEscPressed?.Invoke();
    }
}
