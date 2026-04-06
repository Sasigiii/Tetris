using UnityEngine;
using DG.Tweening;

public class GameOverUIController : BaseController<GameOverUIView, GameOverUIModel>
{
    private const float EffectDelay = 0.5f;
    private const float StarInterval = 0.15f;
    private const float StarDuration = 0.3f;

    private Sequence _starSequence;

    protected override void OnInitialize()
    {
        if (GameOverUIModel.Pending != null)
        {
            Model.isCleared = GameOverUIModel.Pending.isCleared;
            Model.finalScore = GameOverUIModel.Pending.finalScore;
            Model.starRating = GameOverUIModel.Pending.starRating;
            GameOverUIModel.Pending = null;
        }

        View.restartBtn.onClick.RemoveAllListeners();
        View.restartBtn.onClick.AddListener(OnRestartClicked);

        View.backBtn.onClick.RemoveAllListeners();
        View.backBtn.onClick.AddListener(OnBackClicked);
    }

    public override void OnEnter()
    {
        base.OnEnter();

        View.levelNameTMP.text = $"Level {GameContext.CurrentLevel}";
        View.scoreTMP.text = $"分数:{Model.finalScore}";

        for (int i = 0; i < View.starImages.Length; i++)
        {
            View.starImages[i].transform.localScale = Vector3.zero;
        }

        if (Model.isCleared)
        {
            AudioManager.Instance?.PlayEvent("gameOverWin");

            if (View.effectObj != null)
            {
                View.effectObj.SetActive(true);
                var ps = View.effectObj.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
            }

            ProgressManager.SetMaxLevel(GameContext.CurrentLexicon, GameContext.CurrentLevel);

            PlayStarAnimation(Model.starRating);
        }
        else
        {
            AudioManager.Instance?.PlayEvent("gameOverLose");

            if (View.effectObj != null)
                View.effectObj.SetActive(false);
        }
    }

    public override void OnExit()
    {
        base.OnExit();

        _starSequence?.Kill();
        _starSequence = null;

        for (int i = 0; i < View.starImages.Length; i++)
        {
            View.starImages[i].transform.DOKill();
        }

        if (View.effectObj != null)
            View.effectObj.SetActive(false);
    }

    private void PlayStarAnimation(int starCount)
    {
        _starSequence?.Kill();
        _starSequence = DOTween.Sequence();
        _starSequence.AppendInterval(EffectDelay);

        for (int i = 0; i < starCount && i < View.starImages.Length; i++)
        {
            var star = View.starImages[i].transform;
            _starSequence.AppendCallback(() => AudioManager.Instance?.PlayEvent("starPop"));
            _starSequence.Append(star.DOScale(1f, StarDuration).SetEase(Ease.OutBack));
            if (i < starCount - 1)
                _starSequence.AppendInterval(StarInterval);
        }
    }

    private void OnRestartClicked()
    {
        UIManager.Instance.PopPanel();
        UIManager.Instance.PopPanel();
        UIManager.Instance.PushPanel<GamePlayUIController, GamePlayUIView, GamePlayUIModel>("GamePlayUI");
    }

    private void OnBackClicked()
    {
        UIManager.Instance.PopPanel();
        UIManager.Instance.PopPanel();
    }
}
