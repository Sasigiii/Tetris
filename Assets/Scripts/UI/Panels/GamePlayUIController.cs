using UnityEngine;

public class GamePlayUIController : BaseController<GamePlayUIView, GamePlayUIModel>
{
    private FloatingScoreEffect _floatingEffect;
    private int _lastScore;
    private bool _hintActive;

    protected override void OnInitialize()
    {
        View.returnBtn.onClick.RemoveAllListeners();
        View.returnBtn.onClick.AddListener(OnReturnClicked);

        View.gamePlayManager.OnGameOver += HandleGameOver;

        if (View.floatingScoreTMP != null)
        {
            _floatingEffect = View.floatingScoreTMP.GetComponent<FloatingScoreEffect>();
            if (_floatingEffect == null)
                _floatingEffect = View.floatingScoreTMP.gameObject.AddComponent<FloatingScoreEffect>();
            _floatingEffect.Init(View.floatingScoreTMP);
        }
    }

    public override void OnEnter()
    {
        base.OnEnter();

        View.wordGrid.ClearAll();
        UpdateScoreDisplay(ScoreManager.InitialScore);

        if (View.levelTMP != null)
            View.levelTMP.text = $"Level {GameContext.CurrentLevel}";

        _hintActive = true;
        WordHintUIController.OnHintFinished = OnWordHintFinished;
        UIManager.Instance.PushPanel<WordHintUIController, WordHintUIView, WordHintUIModel>("WordHintUI");
    }

    private void OnReturnClicked()
    {
        if (_hintActive)
        {
            _hintActive = false;
            WordHintUIController.OnHintFinished = null;
            UIManager.Instance.PopPanel();
        }
        UIManager.Instance.PopPanel();
    }

    private void OnWordHintFinished()
    {
        _hintActive = false;
        var scoreManager = View.gamePlayManager.ScoreManager;
        _lastScore = ScoreManager.InitialScore;
        UpdateScoreDisplay(ScoreManager.InitialScore);
        scoreManager.OnScoreChanged += OnScoreChanged;

        View.gamePlayManager.StartLevel();
    }

    public override void OnExit()
    {
        base.OnExit();

        View.gamePlayManager.ScoreManager.OnScoreChanged -= OnScoreChanged;
        _floatingEffect?.Cleanup();
        View.gamePlayManager.Cleanup();
        View.gamePlayManager.OnGameOver -= HandleGameOver;
    }

    private void OnScoreChanged(int newScore)
    {
        int delta = newScore - _lastScore;
        _lastScore = newScore;

        UpdateScoreDisplay(newScore);
        _floatingEffect?.Play(delta);
    }

    private void UpdateScoreDisplay(int score)
    {
        if (View.scoreTMP != null)
            View.scoreTMP.text = score.ToString();
    }

    private void HandleGameOver(bool isCleared)
    {
        var scoreManager = View.gamePlayManager.ScoreManager;

        GameOverUIModel.Pending = new GameOverUIModel
        {
            isCleared = isCleared,
            finalScore = scoreManager.Score,
            starRating = isCleared ? scoreManager.GetStarRating() : 0
        };

        UIManager.Instance.PushPanel<GameOverUIController, GameOverUIView, GameOverUIModel>("GameOverUI");
    }
}
