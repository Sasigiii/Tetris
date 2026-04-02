using UnityEngine;

public class GameStarter : MonoBehaviour
{
    private void Start()
    {
        var _ = GameContext.Database;
        UIManager.Instance.PushPanel<MainUIController, MainUIView, MainUIModel>("MainUI");
    }

    private void OnApplicationQuit()
    {
        GameContext.Shutdown();
    }
}
