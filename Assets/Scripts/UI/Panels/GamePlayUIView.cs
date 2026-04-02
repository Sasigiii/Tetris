using System;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayUIView : BaseView
{
    [System.Serializable]
    public class WordRowBinding
    {
        public Image container;
        public Text[] slotTexts;
    }

    public UIButton returnBtn;
    public RectTransform playArea;
    public RectTransform blockItem;
    public Text[] blockLetters;
    public Text scoreText;
    public Text levelText;
    public Text remainText;
    public WordRowBinding[] wordRows;
    public Action onUpdate;

    private void Update()
    {
        if (onUpdate != null) onUpdate();
    }

    public int GetSlotCount()
    {
        if (wordRows == null || wordRows.Length == 0)
            return 0;

        int max = 0;
        for (int i = 0; i < wordRows.Length; i++)
        {
            var row = wordRows[i];
            if (row == null || row.slotTexts == null) continue;
            if (row.slotTexts.Length > max) max = row.slotTexts.Length;
        }
        return max;
    }

    public int GetRowCount()
    {
        return wordRows == null ? 0 : wordRows.Length;
    }

    public void SetHud(int level, int score, int remain)
    {
        if (levelText != null) levelText.text = $"LEVEL {level:00}";
        if (scoreText != null) scoreText.text = $"SCORE {score}";
        if (remainText != null) remainText.text = $"LEFT {remain}";
    }

    public void RenderRow(int rowIndex, GamePlayUIModel.WordRowState row)
    {
        if (wordRows == null || rowIndex < 0 || rowIndex >= wordRows.Length) return;
        var binding = wordRows[rowIndex];
        if (binding == null || binding.slotTexts == null) return;
        if (binding.container != null) binding.container.gameObject.SetActive(true);

        for (int i = 0; i < binding.slotTexts.Length; i++)
        {
            var txt = binding.slotTexts[i];
            if (txt == null) continue;
            if (row == null || !row.active || i >= row.displayChars.Length)
            {
                txt.text = "";
                continue;
            }
            txt.text = row.displayChars[i].ToString();
        }
    }

    public void SetPieceLetters(char[] letters)
    {
        if (blockLetters == null) return;
        for (int i = 0; i < blockLetters.Length; i++)
        {
            if (blockLetters[i] == null) continue;
            blockLetters[i].text = (letters != null && i < letters.Length) ? letters[i].ToString() : "";
        }
    }

    public void SetPiecePosition(Vector2 anchoredPos)
    {
        if (blockItem == null) return;
        blockItem.anchoredPosition = anchoredPos;
    }

    public void SetPieceVisible(bool visible)
    {
        if (blockItem != null) blockItem.gameObject.SetActive(visible);
    }
}
