using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUIView : BaseView
{
    public TextMeshProUGUI levelNameTMP;
    public TextMeshProUGUI scoreTMP;
    public Image[] starImages = new Image[5];
    public GameObject effectObj;
    public Button restartBtn;
    public Button backBtn;
}
