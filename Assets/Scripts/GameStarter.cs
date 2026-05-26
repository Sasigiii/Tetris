using UnityEngine;

public class GameStarter : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        var _ = GameContext.Database;
        WrongWordManager.Init();
        UIManager.Instance.PushPanel<MainUIController, MainUIView, MainUIModel>("MainUI");
        AudioManager.Instance?.PlayBGM();
    }

    private void OnApplicationQuit()
    {
        GameContext.Shutdown();
    }
}
