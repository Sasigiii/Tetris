using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayUIView : BaseView
{
    public GamePlayManager gamePlayManager;
    public WordGrid wordGrid;
    public Button returnBtn;
    public TextMeshProUGUI scoreTMP;
    public TextMeshProUGUI comboTMP;
    public TextMeshProUGUI timerTMP;
    public TextMeshProUGUI floatingScoreTMP;
    public TextMeshProUGUI levelTMP;
    public float comboPopupDelaySeconds = 0.15f;
    public Color comboPopupColor = new Color(1f, 0.84f, 0.2f, 1f);
}
