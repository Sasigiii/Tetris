using System;
using System.Collections.Generic;

[Serializable]
public class CellData
{
    public char letter;
    public bool isBlank;
    public bool isFilled;
    public int columnIndex;
}

[Serializable]
public class WordRowData
{
    public WordEntry wordEntry;
    public CellData[] cells;
    public List<int> blankIndices;
    public int rowIndex;

    public bool IsComplete()
    {
        foreach (int idx in blankIndices)
        {
            if (!cells[idx].isFilled)
                return false;
        }
        return true;
    }

    public List<int> GetUnfilledBlankIndices()
    {
        var unfilled = new List<int>();
        foreach (int idx in blankIndices)
        {
            if (!cells[idx].isFilled)
                unfilled.Add(idx);
        }
        return unfilled;
    }
}

public enum GameState
{
    Loading,
    Spawning,
    Falling,
    Checking,
    GroupTransition,
    LevelComplete,
    GameOver
}
