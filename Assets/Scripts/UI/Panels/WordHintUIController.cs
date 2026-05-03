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
    private readonly List<GameObject> _spawnedItems = new List<GameObject>();

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

        ClearItems();

        var config = Resources.Load<LexiconConfig>("LexiconConfig");
        var entry = config?.GetEntry(GameContext.CurrentLexicon);
        int wordsPerLevel = entry?.wordsPerLevel ?? 10;
        int maxWordLength = entry?.maxWordLength ?? 18;
        bool orderByLengthDesc = entry != null && entry.lengthPriorityForTesting;

        _words = GameContext.Database.GetPlayableWordsForLevel(
            GameContext.CurrentLexicon,
            GameContext.CurrentLevel,
            wordsPerLevel,
            maxWordLength,
            orderByLengthDesc);

        for (int i = 0; i < _words.Count; i++)
        {
            var go = UnityEngine.Object.Instantiate(View.itemPrefab, View.content);
            go.SetActive(true);
            var item = go.GetComponent<WordHintItemView>();
            if (item != null)
                item.wordText.text = $"{_words[i].headWord}  {_words[i].tranCn}";
            _spawnedItems.Add(go);
        }

        View.scrollRect.verticalNormalizedPosition = 1f;

        _countdownCoroutine = View.StartCoroutine(CountdownRoutine());
    }

    public override void OnExit()
    {
        base.OnExit();
        View.onEscPressed = null;
        StopCountdown();
        ClearItems();
    }

    private void ClearItems()
    {
        foreach (var go in _spawnedItems)
        {
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }
        _spawnedItems.Clear();
    }

    private IEnumerator CountdownRoutine()
    {
        float remaining = CountdownDuration;

        while (remaining > 0f)
        {
            View.countdownTMP.text = $"提示倒计时:{Mathf.CeilToInt(remaining)}";
            yield return null;
            remaining -= Time.deltaTime;
        }

        View.countdownTMP.text = "提示倒计时:0";
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
