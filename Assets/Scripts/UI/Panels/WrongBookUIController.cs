using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WrongBookUIController : BaseController<WrongBookUIView, WrongBookUIModel>
{
    public override bool IsPopup => true;

    private readonly List<GameObject> _spawnedItems = new List<GameObject>();

    protected override void OnInitialize()
    {
        View.returnBtn.onClick.RemoveAllListeners();
        View.returnBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());
    }

    public override void OnEnter()
    {
        base.OnEnter();
        ClearItems();

        Model.wrongWords = WrongWordManager.GetWrongWords(GameContext.CurrentLexicon);
        int total = Model.wrongWords.Count;

        bool hasData = total > 0;
        if (View.emptyHintText != null)
            View.emptyHintText.SetActive(!hasData);

        for (int i = 0; i < total; i++)
        {
            var go = Object.Instantiate(View.itemPrefab, View.content);
            go.SetActive(true);
            go.transform.SetAsLastSibling();

            var itemView = go.GetComponent<WordHintItemView>();
            if (itemView != null)
            {
                var entry = Model.wrongWords[i];
                itemView.wordText.text = $"<b><size=28>{entry.headWord}  ×{entry.count}</size></b>\n<size=24>{entry.tranCn}</size>";
            }

            _spawnedItems.Add(go);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(View.content);

        if (View.scrollRect != null)
            View.scrollRect.verticalNormalizedPosition = 1f;
    }

    public override void OnExit()
    {
        base.OnExit();
        ClearItems();
    }

    private void ClearItems()
    {
        foreach (var go in _spawnedItems)
        {
            if (go != null)
                Object.Destroy(go);
        }
        _spawnedItems.Clear();
    }
}
