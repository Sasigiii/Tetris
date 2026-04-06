using System;
using TMPro;
using UnityEngine;

public class WordHintUIView : BaseView
{
    public InfiniteScrollView scrollView;
    public TextMeshProUGUI countdownTMP;

    public Action onEscPressed;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            onEscPressed?.Invoke();
    }
}
