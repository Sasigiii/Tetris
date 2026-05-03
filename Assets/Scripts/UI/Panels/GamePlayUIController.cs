using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class GamePlayUIController : BaseController<GamePlayUIView, GamePlayUIModel>
{
    private FloatingScoreEffect _floatingEffect;
    private int _lastScore;
    private bool _hintActive;
    private bool _hasPendingAwardBreakdown;
    private int _pendingBaseScore;
    private int _pendingComboBonus;
    private int _pendingTotalGain;
    private float _nextComboPopupReadyTime;
    private readonly List<Tween> _delayedTweens = new List<Tween>();

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
            _floatingEffect.SetComboBonusColor(View.comboPopupColor);
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
        _nextComboPopupReadyTime = 0f;
        UpdateScoreDisplay(ScoreManager.InitialScore);
        scoreManager.OnScoreChanged += OnScoreChanged;
        scoreManager.OnCorrectAwarded += OnCorrectAwarded;
        scoreManager.OnComboChanged += OnComboChanged;
        View.gamePlayManager.OnTimerChanged += OnTimerChanged;
        UpdateComboDisplay(0);

        View.gamePlayManager.StartLevel();
    }

    public override void OnExit()
    {
        base.OnExit();

        View.gamePlayManager.ScoreManager.OnScoreChanged -= OnScoreChanged;
        View.gamePlayManager.ScoreManager.OnCorrectAwarded -= OnCorrectAwarded;
        View.gamePlayManager.ScoreManager.OnComboChanged -= OnComboChanged;
        View.gamePlayManager.OnTimerChanged -= OnTimerChanged;
        ClearDelayedTweens();
        _floatingEffect?.Cleanup();
        View.gamePlayManager.Cleanup();
        View.gamePlayManager.OnGameOver -= HandleGameOver;
    }

    private void OnCorrectAwarded(int baseScore, int comboBonus, int totalGain)
    {
        _pendingBaseScore = Mathf.Max(0, baseScore);
        _pendingComboBonus = Mathf.Max(0, comboBonus);
        _pendingTotalGain = Mathf.Max(0, totalGain);
        _hasPendingAwardBreakdown = true;
    }

    private void OnScoreChanged(int newScore)
    {
        int delta = newScore - _lastScore;
        _lastScore = newScore;

        UpdateScoreDisplay(newScore);
        if (_floatingEffect == null || delta == 0)
            return;

        if (_hasPendingAwardBreakdown && delta > 0 && delta == _pendingTotalGain)
        {
            if (_pendingBaseScore > 0)
                _floatingEffect.Play(_pendingBaseScore);
            if (_pendingComboBonus > 0)
                PlayDelayedComboPopup(_pendingComboBonus);
            _hasPendingAwardBreakdown = false;
            return;
        }

        _hasPendingAwardBreakdown = false;
        _floatingEffect.Play(delta);
    }

    private void PlayDelayedComboPopup(int comboBonus)
    {
        float baseDelay = Mathf.Max(0f, View.comboPopupDelaySeconds);
        float now = Time.unscaledTime;
        float targetTime = Mathf.Max(now + baseDelay, _nextComboPopupReadyTime);
        float delay = Mathf.Max(0f, targetTime - now);
        _nextComboPopupReadyTime = targetTime + 0.05f;
        Tween tween = DOVirtual.DelayedCall(delay, () =>
        {
            _floatingEffect?.Play(comboBonus, FloatingScoreEffect.PopupStyle.ComboBonusGold);
        }, true);
        _delayedTweens.Add(tween);
        tween.OnComplete(() => _delayedTweens.Remove(tween));
        tween.OnKill(() => _delayedTweens.Remove(tween));
    }

    private void ClearDelayedTweens()
    {
        for (int i = _delayedTweens.Count - 1; i >= 0; i--)
        {
            _delayedTweens[i]?.Kill();
        }
        _delayedTweens.Clear();
        _nextComboPopupReadyTime = 0f;
    }

    private void UpdateScoreDisplay(int score)
    {
        if (View.scoreTMP != null)
            View.scoreTMP.text = score.ToString();
    }

    private void OnComboChanged(int combo)
    {
        UpdateComboDisplay(combo);
    }

    private void UpdateComboDisplay(int combo)
    {
        if (View.comboTMP != null)
            View.comboTMP.text = $"Combo x{Mathf.Max(0, combo)}";
    }

    private void OnTimerChanged(float remainingSeconds)
    {
        if (View.timerTMP != null)
            View.timerTMP.text = $"倒计时: {Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds))}";
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
