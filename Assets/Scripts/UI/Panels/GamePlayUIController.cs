using UnityEngine;

public class GamePlayUIController : BaseController<GamePlayUIView, GamePlayUIModel>
{
    protected override void OnInitialize()
    {
        View.returnBtn.onClick.RemoveAllListeners();
        View.returnBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());

        View.gamePlayManager.OnLevelComplete += HandleLevelComplete;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        View.gamePlayManager.StartLevel();
    }

    public override void OnExit()
    {
        base.OnExit();
        View.gamePlayManager.Cleanup();
        View.gamePlayManager.OnLevelComplete -= HandleLevelComplete;
    }

    private void HandleLevelComplete()
    {
        Model.isLevelCleared = true;

        int currentLevel = GameContext.CurrentLevel;
        ProgressManager.SetMaxLevel(GameContext.CurrentLexicon, currentLevel);

        Debug.Log($"[GamePlay] Level {currentLevel} complete!");

        UIManager.Instance.PopPanel();
    }
}
