using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WordHintUIController : BaseController<WordHintUIView, WordHintUIModel>
{
    public override bool IsPopup => true;

    public static Action OnHintFinished;

    private const float CountdownDuration = 10f;

    private List<WordEntry> _words;
    private Coroutine _countdownCoroutine;
    private bool _finished;

    protected override void OnInitialize()
    {
        View.onEscPressed = () =>
        {
            if (!_finished)
                FinishHint();
        };
    }

    public override void OnEnter()
    {
        base.OnEnter();
        _finished = false;

        var config = Resources.Load<LexiconConfig>("LexiconConfig");
        var entry = config?.GetEntry(GameContext.CurrentLexicon);
        int wordsPerLevel = entry?.wordsPerLevel ?? 10;

        _words = GameContext.Database.GetPlayableWordsForLevel(
            GameContext.CurrentLexicon, GameContext.CurrentLevel, wordsPerLevel);

        View.scrollView.Initialize(_words.Count, OnRenderItem);

        _countdownCoroutine = View.StartCoroutine(CountdownRoutine());
    }

    public override void OnExit()
    {
        base.OnExit();
        View.onEscPressed = null;
        StopCountdown();
    }

    private void OnRenderItem(int index, GameObject go)
    {
        var item = go.GetComponent<WordHintItemView>();
        if (item == null || index < 0 || index >= _words.Count) return;

        var word = _words[index];
        item.wordTMP.text = $"{word.headWord}  {word.tranCn}";
    }

    private IEnumerator CountdownRoutine()
    {
        float remaining = CountdownDuration;

        while (remaining > 0f)
        {
            View.countdownTMP.text = $"倒计时：{Mathf.CeilToInt(remaining)}";
            yield return null;
            remaining -= Time.deltaTime;
        }

        View.countdownTMP.text = "倒计时：0";
        FinishHint();
    }

    private void FinishHint()
    {
        if (_finished) return;
        _finished = true;
        StopCountdown();
        UIManager.Instance.PopPanel();
        OnHintFinished?.Invoke();
        OnHintFinished = null;
    }

    private void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            View.StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }
}
