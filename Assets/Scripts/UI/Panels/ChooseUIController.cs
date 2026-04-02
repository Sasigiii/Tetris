using UnityEngine;

public class ChooseUIController : BaseController<ChooseUIView, ChooseUIModel>
{
    private LexiconConfig _config;
    private int _totalLevels;
    private bool _initialized;

    protected override void OnInitialize()
    {
        _config = Resources.Load<LexiconConfig>("LexiconConfig");
        View.returnBtn.onClick.RemoveAllListeners();
        View.returnBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());
    }

    public override void OnEnter()
    {
        base.OnEnter();
        _initialized = false;
        GenerateLevels();
    }

    public override void OnResume()
    {
        base.OnResume();
        GenerateLevels();
    }

    private void GenerateLevels()
    {
        var lexicon = GameContext.CurrentLexicon;
        var entry = _config.GetEntry(lexicon);
        if (entry == null)
        {
            Debug.LogError($"[ChooseUI] No config entry for {lexicon}");
            return;
        }

        int filteredCount = GameContext.Database.GetWordCountByMaxLength(lexicon, entry.maxWordLength);
        _totalLevels = Mathf.Max(1, filteredCount / entry.wordsPerLevel);

        if (!_initialized)
        {
            View.scrollView.Initialize(_totalLevels, RenderLevelItem);
            _initialized = true;
        }
        else
        {
            View.scrollView.Refresh(_totalLevels);
        }
    }

    private void RenderLevelItem(int index, GameObject go)
    {
        var levelView = go.GetComponent<LevelUIView>();
        if (levelView == null) return;

        int levelNum = index + 1;
        levelView.title.text = $"Level {levelNum}";

        int maxCleared = ProgressManager.GetMaxLevel(GameContext.CurrentLexicon);
        levelView.icon.SetActive(levelNum <= maxCleared);

        levelView.confirmBtn.onClick.RemoveAllListeners();
        levelView.confirmBtn.onClick.AddListener(() =>
        {
            GameContext.CurrentLevel = levelNum;
            // TODO: Push GameplayUI
            Debug.Log($"[ChooseUI] Enter level {levelNum}");
        });
    }
}
