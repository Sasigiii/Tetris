using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WordGrid : MonoBehaviour
{
    [SerializeField] private RectTransform[] rows = new RectTransform[4];

    public const int ColumnsPerRow = 7;

    private WordRowData[] _rowData;
    private TextMeshProUGUI[][] _cellTexts;
    private Image[][] _cellImages;

    private void CacheComponents()
    {
        _cellTexts = new TextMeshProUGUI[4][];
        _cellImages = new Image[4][];

        for (int r = 0; r < 4; r++)
        {
            _cellTexts[r] = new TextMeshProUGUI[ColumnsPerRow];
            _cellImages[r] = new Image[ColumnsPerRow];

            if (rows[r] == null) continue;

            for (int c = 0; c < ColumnsPerRow; c++)
            {
                if (c >= rows[r].childCount) break;
                var cellObj = rows[r].GetChild(c);
                _cellImages[r][c] = cellObj.GetComponent<Image>();
                _cellTexts[r][c] = cellObj.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
    }

    public void SetupGroup(List<WordRowData> group)
    {
        if (_cellTexts == null)
            CacheComponents();

        ShowAllRows();
        _rowData = new WordRowData[4];

        for (int r = 0; r < 4; r++)
        {
            if (r < group.Count)
            {
                _rowData[r] = group[r];
                _rowData[r].rowIndex = r;

                for (int c = 0; c < ColumnsPerRow; c++)
                {
                    var cell = _rowData[r].cells[c];
                    if (_cellTexts[r][c] == null) continue;

                    if (cell.letter == '\0')
                    {
                        _cellTexts[r][c].text = "";
                        if (_cellImages[r][c] != null)
                            _cellImages[r][c].enabled = false;
                    }
                    else if (cell.isBlank)
                    {
                        _cellTexts[r][c].text = "_";
                        if (_cellImages[r][c] != null)
                            _cellImages[r][c].enabled = true;
                    }
                    else
                    {
                        _cellTexts[r][c].text = cell.letter.ToString();
                        if (_cellImages[r][c] != null)
                            _cellImages[r][c].enabled = true;
                    }
                }
            }
            else
            {
                _rowData[r] = null;
                for (int c = 0; c < ColumnsPerRow; c++)
                {
                    if (_cellTexts[r] != null && _cellTexts[r][c] != null)
                    {
                        _cellTexts[r][c].text = "";
                        if (_cellImages[r][c] != null)
                            _cellImages[r][c].enabled = false;
                    }
                }
            }
        }
    }

    public void FillCell(int row, int col, char letter)
    {
        if (_rowData == null || row < 0 || row >= 4) return;
        var rd = _rowData[row];
        if (rd == null || col < 0 || col >= ColumnsPerRow) return;

        rd.cells[col].isFilled = true;
        rd.cells[col].letter = letter;

        if (_cellTexts[row][col] != null)
            _cellTexts[row][col].text = letter.ToString();
    }

    public void HideRow(int row)
    {
        if (rows != null && row >= 0 && row < 4 && rows[row] != null)
            rows[row].gameObject.SetActive(false);
    }

    public void ShowAllRows()
    {
        for (int r = 0; r < 4; r++)
        {
            if (rows[r] != null)
                rows[r].gameObject.SetActive(true);
        }
    }

    public int GetTargetRow()
    {
        if (_rowData == null) return -1;
        for (int r = 0; r < 4; r++)
        {
            if (_rowData[r] != null && !_rowData[r].IsComplete())
                return r;
        }
        return -1;
    }

    public WordRowData GetRowData(int row)
    {
        if (_rowData == null || row < 0 || row >= 4) return null;
        return _rowData[row];
    }

    public bool IsGroupComplete()
    {
        if (_rowData == null) return true;
        for (int r = 0; r < 4; r++)
        {
            if (_rowData[r] != null && !_rowData[r].IsComplete())
                return false;
        }
        return true;
    }

    public float GetRowWorldY(int row)
    {
        if (rows == null || row < 0 || row >= 4 || rows[row] == null)
            return 0f;
        return rows[row].position.y;
    }

    public Vector3 GetCellWorldPos(int row, int col)
    {
        if (rows == null || row < 0 || row >= 4 || rows[row] == null)
            return Vector3.zero;
        if (col < 0 || col >= ColumnsPerRow || col >= rows[row].childCount)
            return Vector3.zero;
        return rows[row].GetChild(col).position;
    }
}
