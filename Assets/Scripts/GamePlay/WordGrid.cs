using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WordGrid : MonoBehaviour
{
    [SerializeField] private RectTransform[] rows = new RectTransform[4];
    [SerializeField] private bool debugCellSizingLogs = true;
    [SerializeField] private bool debugFontNormalizationLogs = true;

    private const int DefaultColumns = 7;
    private const int CellSizeThresholdColumns = 7;
    private const float BaselineCellSize = 85f;

    private WordRowData[] _rowData;
    private List<RectTransform>[] _cellRects;
    private List<TextMeshProUGUI>[] _cellTexts;
    private List<RectTransform>[] _cellTextRects;
    private List<Image>[] _cellImages;
    private List<LayoutElement>[] _cellLayoutElements;
    private HorizontalLayoutGroup[] _rowLayouts;
    private Vector2[] _baseCellSizes;
    private Vector2[] _lastCellSizes;
    private int _lastAppliedColumns = -1;
    private bool _lastSyncTMPRect;
    private float _lastTMPPaddingX = -1f;
    private float _lastTMPPaddingY = -1f;
    private float _lastMinTMPRectSize = -1f;
    private readonly HashSet<string> _missingTmpWarnings = new HashSet<string>();

    public int ActiveColumns { get; private set; } = DefaultColumns;

    private void CacheComponents()
    {
        _cellRects = new List<RectTransform>[rows.Length];
        _cellTexts = new List<TextMeshProUGUI>[rows.Length];
        _cellTextRects = new List<RectTransform>[rows.Length];
        _cellImages = new List<Image>[rows.Length];
        _cellLayoutElements = new List<LayoutElement>[rows.Length];
        _rowLayouts = new HorizontalLayoutGroup[rows.Length];
        _baseCellSizes = new Vector2[rows.Length];
        _lastCellSizes = new Vector2[rows.Length];

        for (int r = 0; r < rows.Length; r++)
        {
            _cellRects[r] = new List<RectTransform>();
            _cellTexts[r] = new List<TextMeshProUGUI>();
            _cellTextRects[r] = new List<RectTransform>();
            _cellImages[r] = new List<Image>();
            _cellLayoutElements[r] = new List<LayoutElement>();

            if (rows[r] == null) continue;

            _rowLayouts[r] = rows[r].GetComponent<HorizontalLayoutGroup>();
            for (int c = 0; c < rows[r].childCount; c++)
            {
                var cellRect = rows[r].GetChild(c) as RectTransform;
                if (cellRect == null) continue;

                _cellRects[r].Add(cellRect);
                _cellImages[r].Add(cellRect.GetComponent<Image>());
                _cellLayoutElements[r].Add(EnsureLayoutElement(cellRect.gameObject));
                var tmp = cellRect.GetComponentInChildren<TextMeshProUGUI>();
                _cellTexts[r].Add(tmp);
                _cellTextRects[r].Add(tmp != null ? tmp.rectTransform : null);

                if (tmp == null)
                {
                    string warningKey = $"r{r}c{c}";
                    if (_missingTmpWarnings.Add(warningKey))
                        Debug.LogWarning($"[WordGrid] Missing TMP child at row {r}, column {c}. TMP Rect sync will be skipped for this cell.");
                }
            }

            if (_cellRects[r].Count > 0)
            {
                _baseCellSizes[r] = _cellRects[r][0].sizeDelta;
            }
            else
            {
                _baseCellSizes[r] = new Vector2(85f, 85f);
            }
        }
    }

    public void SetupGroup(
        List<WordRowData> group,
        int activeColumns,
        float minCellSize,
        bool syncTMPRectWithCell,
        float tmpHorizontalPadding,
        float tmpVerticalPadding,
        float minTMPRectSize,
        bool unifyRowLetterFontSize,
        float unifiedRowLetterFontSizeMin)
    {
        if (_cellTexts == null)
            CacheComponents();

        ActiveColumns = Mathf.Max(1, activeColumns);
        EnsureRowCapacity(ActiveColumns);
        bool sizeChanged = ApplyCellSizing(
            minCellSize,
            syncTMPRectWithCell,
            tmpHorizontalPadding,
            tmpVerticalPadding,
            minTMPRectSize);

        if (sizeChanged)
            RebuildRowLayouts();

        ShowAllRows();
        _rowData = new WordRowData[rows.Length];

        for (int r = 0; r < rows.Length; r++)
        {
            if (r < group.Count)
            {
                _rowData[r] = group[r];
                _rowData[r].rowIndex = r;

                for (int c = 0; c < ActiveColumns; c++)
                {
                    SetCellActive(r, c, true);
                    if (c >= _rowData[r].cells.Length) continue;

                    var cell = _rowData[r].cells[c];
                    if (_cellTexts[r][c] == null) continue;

                    if (cell.letter == '\0')
                    {
                        _cellTexts[r][c].text = "";
                        EnableCellImage(r, c, false);
                    }
                    else if (cell.isBlank)
                    {
                        _cellTexts[r][c].text = "_";
                        EnableCellImage(r, c, true);
                    }
                    else
                    {
                        _cellTexts[r][c].text = cell.letter.ToString();
                        EnableCellImage(r, c, true);
                    }
                }
            }
            else
            {
                _rowData[r] = null;
                for (int c = 0; c < ActiveColumns; c++)
                {
                    SetCellActive(r, c, true);
                    if (_cellTexts[r] != null && _cellTexts[r][c] != null)
                        _cellTexts[r][c].text = "";
                    EnableCellImage(r, c, false);
                }
            }

            HideCellsOutOfRange(r, ActiveColumns);
        }

        if (unifyRowLetterFontSize)
            NormalizeRowLetterFontSize(unifiedRowLetterFontSizeMin);
    }

    public void FillCell(int row, int col, char letter)
    {
        if (_rowData == null || row < 0 || row >= rows.Length) return;
        var rd = _rowData[row];
        if (rd == null || col < 0 || col >= ActiveColumns || col >= rd.cells.Length) return;

        rd.cells[col].isFilled = true;
        rd.cells[col].letter = letter;

        if (_cellTexts[row][col] != null)
            _cellTexts[row][col].text = letter.ToString();
    }

    public void HideRow(int row)
    {
        if (rows != null && row >= 0 && row < rows.Length && rows[row] != null)
            rows[row].gameObject.SetActive(false);
    }

    public void ShowAllRows()
    {
        for (int r = 0; r < rows.Length; r++)
        {
            if (rows[r] != null)
                rows[r].gameObject.SetActive(true);
        }
    }

    public void ClearAll()
    {
        if (_cellTexts == null)
            CacheComponents();

        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < _cellTexts[r].Count; c++)
            {
                if (_cellTexts[r] != null && _cellTexts[r][c] != null)
                    _cellTexts[r][c].text = "";
                EnableCellImage(r, c, false);
                SetCellActive(r, c, c < ActiveColumns);
            }
        }

        ShowAllRows();
        _rowData = null;
    }

    public int GetTargetRow()
    {
        if (_rowData == null) return -1;
        for (int r = 0; r < rows.Length; r++)
        {
            if (_rowData[r] != null && !_rowData[r].IsComplete())
                return r;
        }
        return -1;
    }

    public WordRowData GetRowData(int row)
    {
        if (_rowData == null || row < 0 || row >= rows.Length) return null;
        return _rowData[row];
    }

    public bool IsGroupComplete()
    {
        if (_rowData == null) return true;
        for (int r = 0; r < rows.Length; r++)
        {
            if (_rowData[r] != null && !_rowData[r].IsComplete())
                return false;
        }
        return true;
    }

    public float GetRowWorldY(int row)
    {
        if (rows == null || row < 0 || row >= rows.Length || rows[row] == null)
            return 0f;
        return rows[row].position.y;
    }

    public Vector3 GetCellWorldPos(int row, int col)
    {
        if (rows == null || row < 0 || row >= rows.Length || rows[row] == null)
            return Vector3.zero;
        if (col < 0 || col >= ActiveColumns || col >= rows[row].childCount)
            return Vector3.zero;

        var cell = rows[row].GetChild(col) as RectTransform;
        if (cell == null)
            return rows[row].GetChild(col).position;

        // Under Control Child Size/Stretch anchors, transform.position may not represent
        // the rendered center. Use world-corner center for stable column mapping.
        var corners = new Vector3[4];
        cell.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    public Vector2 GetCellWorldSize(int row, int col)
    {
        if (rows == null || row < 0 || row >= rows.Length || rows[row] == null)
            return Vector2.zero;
        if (col < 0 || col >= ActiveColumns || col >= rows[row].childCount)
            return Vector2.zero;

        var cell = rows[row].GetChild(col) as RectTransform;
        if (cell == null)
            return Vector2.zero;

        var corners = new Vector3[4];
        cell.GetWorldCorners(corners);
        float width = Vector3.Distance(corners[0], corners[3]);
        float height = Vector3.Distance(corners[0], corners[1]);
        return new Vector2(width, height);
    }

    private void EnsureRowCapacity(int requiredColumns)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            if (rows[r] == null || _cellRects[r].Count == 0) continue;

            var template = _cellRects[r][0];
            while (_cellRects[r].Count < requiredColumns)
            {
                var clone = Instantiate(template.gameObject, rows[r]);
                clone.name = $"{template.gameObject.name}_{_cellRects[r].Count}";
                var cloneRect = clone.transform as RectTransform;
                _cellRects[r].Add(cloneRect);
                _cellImages[r].Add(clone.GetComponent<Image>());
                _cellLayoutElements[r].Add(EnsureLayoutElement(clone));
                var tmp = clone.GetComponentInChildren<TextMeshProUGUI>();
                _cellTexts[r].Add(tmp);
                _cellTextRects[r].Add(tmp != null ? tmp.rectTransform : null);

                if (tmp == null)
                {
                    string warningKey = $"r{r}c{_cellRects[r].Count - 1}";
                    if (_missingTmpWarnings.Add(warningKey))
                        Debug.LogWarning($"[WordGrid] Missing TMP child at row {r}, column {_cellRects[r].Count - 1}. TMP Rect sync will be skipped for this cell.");
                }
            }
        }
    }

    private bool ApplyCellSizing(
        float minCellSize,
        bool syncTMPRectWithCell,
        float tmpHorizontalPadding,
        float tmpVerticalPadding,
        float minTMPRectSize)
    {
        bool settingsChanged = _lastAppliedColumns != ActiveColumns
                               || _lastSyncTMPRect != syncTMPRectWithCell
                               || !Mathf.Approximately(_lastTMPPaddingX, tmpHorizontalPadding)
                               || !Mathf.Approximately(_lastTMPPaddingY, tmpVerticalPadding)
                               || !Mathf.Approximately(_lastMinTMPRectSize, minTMPRectSize);
        bool changed = settingsChanged;

        for (int r = 0; r < rows.Length; r++)
        {
            if (rows[r] == null || _cellRects[r].Count == 0) continue;

            float baseWidth = BaselineCellSize;
            float baseHeight = BaselineCellSize;
            float spacing = _rowLayouts[r] != null ? _rowLayouts[r].spacing : 0f;
            float padding = _rowLayouts[r] != null ? _rowLayouts[r].padding.left + _rowLayouts[r].padding.right : 0f;
            float availableWidth = Mathf.Max(1f, rows[r].rect.width - padding);
            float targetWidth = baseWidth;
            float maxWidthToFit = Mathf.Max(1f, (availableWidth - Mathf.Max(0, ActiveColumns - 1) * spacing) / ActiveColumns);

            if (ActiveColumns > CellSizeThresholdColumns)
            {
                float desiredWidth = baseWidth * ActiveColumns + Mathf.Max(0, ActiveColumns - 1) * spacing;
                float scale = desiredWidth > 0f ? Mathf.Min(1f, availableWidth / desiredWidth) : 1f;
                targetWidth = Mathf.Max(minCellSize, baseWidth * scale);
            }

            // Always keep width inside row bounds, but do not shrink <=7 unless required by container.
            targetWidth = Mathf.Min(targetWidth, maxWidthToFit);

            float widthScale = baseWidth > 0f ? targetWidth / baseWidth : 1f;
            float targetHeight = Mathf.Max(1f, baseHeight * widthScale);
            var targetSize = new Vector2(targetWidth, targetHeight);

            if (debugCellSizingLogs && r == 0 && (ActiveColumns == 7 || ActiveColumns == 8 || ActiveColumns == 12))
            {
                Debug.Log("[WordGrid] Cell sizing snapshot: "
                          + $"columns={ActiveColumns}, target=({targetSize.x:0.##},{targetSize.y:0.##}), "
                          + $"base={BaselineCellSize:0.##}, min={minCellSize:0.##}, "
                          + $"availableWidth={availableWidth:0.##}, maxWidthToFit={maxWidthToFit:0.##}, spacing={spacing:0.##}");
            }

            if (!Approximately(_lastCellSizes[r], targetSize))
                changed = true;
            _lastCellSizes[r] = targetSize;

            for (int c = 0; c < _cellRects[r].Count; c++)
            {
                if (_cellRects[r][c] != null)
                    _cellRects[r][c].sizeDelta = targetSize;

                if (_cellLayoutElements[r] != null && c < _cellLayoutElements[r].Count)
                    ApplyLayoutElementSizing(_cellLayoutElements[r][c], targetSize);

                if (syncTMPRectWithCell)
                {
                    SyncTMPRectSize(r, c, targetSize, tmpHorizontalPadding, tmpVerticalPadding, minTMPRectSize);
                }
            }
        }

        _lastAppliedColumns = ActiveColumns;
        _lastSyncTMPRect = syncTMPRectWithCell;
        _lastTMPPaddingX = tmpHorizontalPadding;
        _lastTMPPaddingY = tmpVerticalPadding;
        _lastMinTMPRectSize = minTMPRectSize;
        return changed;
    }

    private void HideCellsOutOfRange(int row, int activeColumns)
    {
        if (_cellRects[row] == null) return;
        for (int c = 0; c < _cellRects[row].Count; c++)
        {
            SetCellActive(row, c, c < activeColumns);
        }
    }

    private void SetCellActive(int row, int col, bool active)
    {
        if (_cellRects[row] == null || col < 0 || col >= _cellRects[row].Count) return;
        if (_cellRects[row][col] != null && _cellRects[row][col].gameObject.activeSelf != active)
            _cellRects[row][col].gameObject.SetActive(active);
    }

    private void EnableCellImage(int row, int col, bool enabled)
    {
        if (_cellImages[row] == null || col < 0 || col >= _cellImages[row].Count) return;
        if (_cellImages[row][col] != null)
            _cellImages[row][col].enabled = enabled;
    }

    private void SyncTMPRectSize(
        int row,
        int col,
        Vector2 cellSize,
        float horizontalPadding,
        float verticalPadding,
        float minTMPRectSize)
    {
        if (_cellTextRects[row] == null || col < 0 || col >= _cellTextRects[row].Count) return;
        var textRect = _cellTextRects[row][col];
        if (textRect == null) return;

        float paddedWidth = Mathf.Max(1f, cellSize.x - horizontalPadding * 2f);
        float paddedHeight = Mathf.Max(1f, cellSize.y - verticalPadding * 2f);

        // When columns are dense, prioritize fitting inside the cell over minimum TMP rect.
        float targetWidth = paddedWidth >= minTMPRectSize ? paddedWidth : Mathf.Min(minTMPRectSize, cellSize.x);
        float targetHeight = paddedHeight >= minTMPRectSize ? paddedHeight : Mathf.Min(minTMPRectSize, cellSize.y);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(targetWidth, targetHeight);
    }

    private void RebuildRowLayouts()
    {
        Canvas.ForceUpdateCanvases();
        for (int r = 0; r < rows.Length; r++)
        {
            if (rows[r] != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rows[r]);
        }
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }

    private void NormalizeRowLetterFontSize(float minFontSizeLowerBound)
    {
        if (_rowData == null || _cellTexts == null)
            return;

        // Ensure TMP rect/cell updates are applied before autosize sampling.
        Canvas.ForceUpdateCanvases();
        for (int r = 0; r < rows.Length; r++)
        {
            if (rows[r] != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rows[r]);
        }

        for (int r = 0; r < rows.Length; r++)
        {
            if (_rowData[r] == null || _cellTexts[r] == null)
                continue;

            float rowMinAutoSize = float.MaxValue;
            int sampledCount = 0;
            for (int c = 0; c < ActiveColumns && c < _cellTexts[r].Count; c++)
            {
                var tmp = _cellTexts[r][c];
                if (tmp == null || string.IsNullOrEmpty(tmp.text))
                    continue;

                tmp.enableAutoSizing = true;
                tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                float autoSize = tmp.fontSize;
                if (autoSize <= 0f)
                    continue;

                sampledCount++;
                rowMinAutoSize = Mathf.Min(rowMinAutoSize, autoSize);
                if (debugFontNormalizationLogs)
                {
                    Debug.Log($"[WordGrid] AutoSize sample row={r}, col={c}, text='{tmp.text}', autoSize={autoSize:0.##}");
                }
            }

            if (rowMinAutoSize == float.MaxValue || sampledCount == 0)
                continue;

            float appliedFontSize = minFontSizeLowerBound > 0f
                ? Mathf.Max(minFontSizeLowerBound, rowMinAutoSize)
                : rowMinAutoSize;

            for (int c = 0; c < ActiveColumns && c < _cellTexts[r].Count; c++)
            {
                var tmp = _cellTexts[r][c];
                if (tmp == null || string.IsNullOrEmpty(tmp.text))
                    continue;

                tmp.enableAutoSizing = false;
                tmp.fontSize = appliedFontSize;
                tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            }

            if (debugFontNormalizationLogs)
            {
                Debug.Log("[WordGrid] Row font normalized: "
                          + $"row={r}, sampled={sampledCount}, "
                          + $"rowMinAutoSize={rowMinAutoSize:0.##}, "
                          + $"applied={appliedFontSize:0.##}, "
                          + $"minBound={minFontSizeLowerBound:0.##}");
            }
        }
    }

    private static LayoutElement EnsureLayoutElement(GameObject cellObject)
    {
        if (cellObject == null) return null;
        var layoutElement = cellObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = cellObject.AddComponent<LayoutElement>();
        return layoutElement;
    }

    private static void ApplyLayoutElementSizing(LayoutElement layoutElement, Vector2 targetSize)
    {
        if (layoutElement == null) return;

        layoutElement.ignoreLayout = false;
        layoutElement.minWidth = targetSize.x;
        layoutElement.preferredWidth = targetSize.x;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = targetSize.y;
        layoutElement.preferredHeight = targetSize.y;
        layoutElement.flexibleHeight = 0f;
    }

}
