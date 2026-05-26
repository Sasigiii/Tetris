using UnityEngine;

public class WrongBookUIController : BaseController<WrongBookUIView, WrongBookUIModel>
{
    public override bool IsPopup => true;

    private bool _initialized;

    protected override void OnInitialize()
    {
        View.returnBtn.onClick.RemoveAllListeners();
        View.returnBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());
    }

    public override void OnEnter()
    {
        base.OnEnter();
        _initialized = false;
        RefreshList();
    }

    private void RefreshList()
    {
        Model.wrongWords = WrongWordManager.GetWrongWords(GameContext.CurrentLexicon);
        int total = Model.wrongWords.Count;

        bool hasData = total > 0;
        if (View.emptyHintText != null)
            View.emptyHintText.SetActive(!hasData);

        if (!_initialized)
        {
            View.scrollView.Initialize(total, RenderItem);
            _initialized = true;
        }
        else
        {
            View.scrollView.Refresh(total);
        }
    }

    private void RenderItem(int index, GameObject go)
    {
        var itemView = go.GetComponent<WordHintItemView>();
        if (itemView == null || index < 0 || index >= Model.wrongWords.Count) return;

        var entry = Model.wrongWords[index];
        itemView.wordText.text = $"<b><size=28>{entry.headWord}  ×{entry.count}</size></b>\n<size=24>{entry.tranCn}</size>";
    }
}
