using System;
using System.Collections.Generic;
using UnityEngine;

public class GamePlayUIController : BaseController<GamePlayUIView, GamePlayUIModel>
{
    private const bool EnableDebugLog = true;

    private struct RowYInfo
    {
        public int uiRowIndex;
        public float y;
    }

    private readonly Vector2Int[] _defaultOffsets = new Vector2Int[]
    {
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1)
    };

    private readonly Vector2Int[] _pieceOffsets = new Vector2Int[5];
    private readonly char[] _pieceLetters = new char[5];
    private readonly List<float> _columnXs = new List<float>();
    private readonly List<float> _rowYByGrid = new List<float>();
    private readonly Dictionary<int, float> _uiRowYMap = new Dictionary<int, float>();

    private int[] _gridToUiRow;
    private int _slotCount;
    private int _rowCount;
    private int _totalRows;
    private int _minGridY;
    private int _pivotX;
    private int _pivotY;
    private float _fallTimer;
    private float _nextHorizontalMoveTime;
    private float _rowStep = 80f;
    private bool _running;
    private int _wordsPerLevel = 10;
    private LexiconConfig _config;

    private const float NormalFallInterval = 0.8f;
    private const float SoftFallInterval = 0.08f;
    private const float HorizontalRepeatDelay = 0.16f;
    private const float HorizontalRepeatInterval = 0.06f;

    protected override void OnInitialize()
    {
        _config = Resources.Load<LexiconConfig>("LexiconConfig");
        View.returnBtn.onClick.RemoveAllListeners();
        View.returnBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());
        View.onUpdate = Tick;

        BuildGridMetrics();
        BuildLevelWords();
        RenderAllRows();
        UpdateHud();
        SpawnNextPiece();
    }

    public override void OnEnter()
    {
        base.OnEnter();
        _running = true;
    }

    public override void OnPause()
    {
        base.OnPause();
        _running = false;
    }

    public override void OnResume()
    {
        base.OnResume();
        _running = true;
    }

    public override void OnExit()
    {
        base.OnExit();
        _running = false;
        View.onUpdate = null;
        View.SetPieceVisible(false);
    }

    private void Tick()
    {
        if (!_running || Model.levelFinished) return;

        HandleInput();

        float interval = IsSoftDropPressed() ? SoftFallInterval : NormalFallInterval;
        _fallTimer += Time.deltaTime;
        if (_fallTimer >= interval)
        {
            _fallTimer = 0f;
            StepDown();
        }
    }

    private void HandleInput()
    {
        int horizontal = GetHorizontalInput();
        if (horizontal != 0)
        {
            bool firstPress = IsHorizontalFirstPress(horizontal);
            if (firstPress || Time.time >= _nextHorizontalMoveTime)
            {
                TryMove(horizontal);
                _nextHorizontalMoveTime = Time.time + (firstPress ? HorizontalRepeatDelay : HorizontalRepeatInterval);
            }
        }
        else
        {
            _nextHorizontalMoveTime = 0f;
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad8))
        {
            TryRotate();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HardDrop();
        }
    }

    private int GetHorizontalInput()
    {
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Keypad4))
            return -1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Keypad6))
            return 1;
        float axis = Input.GetAxisRaw("Horizontal");
        if (axis < -0.1f) return -1;
        if (axis > 0.1f) return 1;
        return 0;
    }

    private bool IsHorizontalFirstPress(int horizontal)
    {
        if (horizontal < 0)
            return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Keypad4);
        return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Keypad6);
    }

    private bool IsSoftDropPressed()
    {
        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Keypad2);
    }

    private void BuildGridMetrics()
    {
        _slotCount = View.GetSlotCount();
        _rowCount = View.GetRowCount();
        _totalRows = _rowCount + 6;
        _columnXs.Clear();
        _rowYByGrid.Clear();
        _uiRowYMap.Clear();

        if (_slotCount <= 0 || _rowCount <= 0 || View.wordRows == null || View.wordRows.Length == 0) return;

        GamePlayUIView.WordRowBinding firstValidRow = null;
        for (int i = 0; i < View.wordRows.Length; i++)
        {
            var row = View.wordRows[i];
            if (row != null && row.slotTexts != null && row.slotTexts.Length > 0)
            {
                firstValidRow = row;
                break;
            }
        }

        if (firstValidRow == null) return;

        for (int i = 0; i < firstValidRow.slotTexts.Length; i++)
        {
            var slotRect = GetSlotReferenceRect(firstValidRow, i);
            if (slotRect == null) continue;
            _columnXs.Add(ToPlayAreaLocal(slotRect).x);
        }

        if (_columnXs.Count <= 1 || GetDistinctCount(_columnXs) <= 1)
        {
            _columnXs.Clear();
            BuildColumnsByContainer(firstValidRow, _slotCount);
        }

        var yList = new List<RowYInfo>();
        for (int i = 0; i < _rowCount; i++)
        {
            if (View.wordRows[i] == null || View.wordRows[i].slotTexts == null || View.wordRows[i].slotTexts.Length == 0) continue;
            var first = View.wordRows[i].slotTexts[0];
            if (first == null) continue;
            var info = new RowYInfo();
            info.uiRowIndex = i;
            info.y = ToPlayAreaLocal((RectTransform)first.transform).y;
            yList.Add(info);
        }

        yList.Sort((a, b) => a.y.CompareTo(b.y));
        _gridToUiRow = new int[yList.Count];
        for (int i = 0; i < yList.Count; i++)
        {
            _rowYByGrid.Add(yList[i].y);
            _gridToUiRow[i] = yList[i].uiRowIndex;
            _uiRowYMap[yList[i].uiRowIndex] = yList[i].y;
        }

        if (_rowYByGrid.Count > 1)
            _rowStep = Mathf.Abs(_rowYByGrid[1] - _rowYByGrid[0]);

        RectTransform area = View.playArea != null ? View.playArea : (RectTransform)View.transform;
        if (_rowYByGrid.Count > 0)
        {
            float lowestRowY = _rowYByGrid[0];
            float bottomLimitY = area.rect.yMin + _rowStep * 0.5f;
            int extraSteps = Mathf.Max(0, Mathf.CeilToInt((lowestRowY - bottomLimitY) / Mathf.Max(1f, _rowStep)));
            _minGridY = -extraSteps;
        }
        else
        {
            _minGridY = 0;
        }

    }

    private Vector2 ToPlayAreaLocal(RectTransform target)
    {
        RectTransform area = View.playArea != null ? View.playArea : (RectTransform)View.transform;
        Vector3 local = area.InverseTransformPoint(target.position);
        return new Vector2(local.x, local.y);
    }

    private RectTransform GetSlotReferenceRect(GamePlayUIView.WordRowBinding row, int slotIndex)
    {
        if (row == null || row.slotTexts == null || slotIndex < 0 || slotIndex >= row.slotTexts.Length) return null;
        var txt = row.slotTexts[slotIndex];
        if (txt == null) return null;
        var parentRect = txt.transform.parent as RectTransform;
        if (parentRect != null) return parentRect;
        return txt.transform as RectTransform;
    }

    private void BuildColumnsByContainer(GamePlayUIView.WordRowBinding row, int count)
    {
        if (count <= 0) return;
        RectTransform area = View.playArea != null ? View.playArea : (RectTransform)View.transform;
        RectTransform containerRect = row != null ? row.container != null ? row.container.rectTransform : null : null;
        if (containerRect == null)
        {
            for (int i = 0; i < count; i++) _columnXs.Add(i * 40f);
            return;
        }

        Vector2 centerLocal = ToPlayAreaLocal(containerRect);
        float width = containerRect.rect.width;
        if (width <= 0f) width = 40f * count;

        float left = centerLocal.x - width * 0.5f;
        float step = width / Mathf.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            _columnXs.Add(left + (i + 0.5f) * step);
        }
    }

    private int GetDistinctCount(List<float> values)
    {
        const float eps = 0.5f;
        var distinct = new List<float>();
        for (int i = 0; i < values.Count; i++)
        {
            bool exists = false;
            for (int j = 0; j < distinct.Count; j++)
            {
                if (Mathf.Abs(values[i] - distinct[j]) <= eps)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists) distinct.Add(values[i]);
        }
        return distinct.Count;
    }

    private void BuildLevelWords()
    {
        Model.levelWords.Clear();
        Model.rows.Clear();
        Model.nextWordIndex = 0;
        Model.completedWords = 0;
        Model.score = 0;
        Model.levelFinished = false;

        if (_slotCount <= 0 || _rowCount <= 0) return;

        var entry = _config != null ? _config.GetEntry(GameContext.CurrentLexicon) : null;
        int maxLen = entry != null ? entry.maxWordLength : _slotCount;
        _wordsPerLevel = entry != null ? Mathf.Max(1, entry.wordsPerLevel) : 10;
        int effectiveMaxLen = Mathf.Min(maxLen, _slotCount);

        List<WordEntry> all = GameContext.Database.GetAllWords(GameContext.CurrentLexicon);
        var filtered = new List<WordEntry>();
        for (int i = 0; i < all.Count; i++)
        {
            var w = all[i];
            if (w == null || string.IsNullOrEmpty(w.headWord)) continue;
            string word = w.headWord.Trim().ToUpperInvariant();
            if (word.Length < 2 || word.Length > effectiveMaxLen) continue;
            if (!IsPureAlphabet(word)) continue;
            filtered.Add(w);
        }

        if (filtered.Count == 0)
        {
            Model.levelFinished = true;
            return;
        }

        int level = Mathf.Max(1, GameContext.CurrentLevel);
        int start = (level - 1) * _wordsPerLevel;
        for (int i = 0; i < _wordsPerLevel; i++)
        {
            int index = (start + i) % filtered.Count;
            Model.levelWords.Add(filtered[index]);
        }

        for (int i = 0; i < _rowCount; i++)
        {
            if (Model.nextWordIndex < Model.levelWords.Count)
            {
                Model.rows.Add(CreateRowState(Model.levelWords[Model.nextWordIndex]));
                Model.nextWordIndex++;
            }
            else
            {
                Model.rows.Add(CreateInactiveRow());
            }
        }
    }

    private bool IsPureAlphabet(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c < 'A' || c > 'Z') return false;
        }
        return true;
    }

    private GamePlayUIModel.WordRowState CreateInactiveRow()
    {
        var row = new GamePlayUIModel.WordRowState();
        row.answer = "";
        row.displayChars = new char[_slotCount];
        row.holeOpen = new bool[_slotCount];
        row.active = false;
        for (int i = 0; i < _slotCount; i++) row.displayChars[i] = ' ';
        return row;
    }

    private GamePlayUIModel.WordRowState CreateRowState(WordEntry entry)
    {
        var row = new GamePlayUIModel.WordRowState();
        row.answer = entry.headWord.Trim().ToUpperInvariant();
        row.displayChars = new char[_slotCount];
        row.holeOpen = new bool[_slotCount];
        row.active = true;

        for (int i = 0; i < _slotCount; i++)
        {
            if (i < row.answer.Length)
                row.displayChars[i] = row.answer[i];
            else
                row.displayChars[i] = ' ';
            row.holeOpen[i] = false;
        }

        int holeCount = UnityEngine.Random.Range(1, Mathf.Min(3, row.answer.Length) + 1);
        var chosen = new HashSet<int>();
        while (chosen.Count < holeCount)
        {
            chosen.Add(UnityEngine.Random.Range(0, row.answer.Length));
        }

        foreach (int idx in chosen)
        {
            row.displayChars[idx] = '_';
            row.holeOpen[idx] = true;
        }

        return row;
    }

    private void RenderAllRows()
    {
        for (int i = 0; i < _rowCount; i++)
        {
            GamePlayUIModel.WordRowState row = i < Model.rows.Count ? Model.rows[i] : null;
            View.RenderRow(i, row);
        }
    }

    private void UpdateHud()
    {
        int remain = Mathf.Max(0, Model.levelWords.Count - Model.completedWords);
        View.SetHud(GameContext.CurrentLevel, Model.score, remain);
    }

    private void SpawnNextPiece()
    {
        if (Model.levelFinished)
        {
            View.SetPieceVisible(false);
            return;
        }

        if (!TryBuildPieceLetters(_pieceLetters))
        {
            MarkLevelFinished();
            return;
        }

        for (int i = 0; i < _defaultOffsets.Length; i++)
            _pieceOffsets[i] = _defaultOffsets[i];

        _pivotX = Mathf.Clamp(_slotCount / 2, 0, _slotCount - 1);
        _pivotY = _totalRows - 2;
        _fallTimer = 0f;

        if (WouldCollide(_pivotX, _pivotY, _pieceOffsets))
        {
            MarkLevelFinished();
            return;
        }

        View.SetPieceLetters(_pieceLetters);
        View.SetPieceVisible(true);
        SyncPiecePosition();
    }

    private bool TryBuildPieceLetters(char[] letters)
    {
        var candidateLetters = new List<char>();
        for (int i = 0; i < Model.rows.Count; i++)
        {
            var row = Model.rows[i];
            if (row == null || !row.active) continue;
            for (int c = 0; c < row.answer.Length; c++)
            {
                if (row.holeOpen[c]) candidateLetters.Add(row.answer[c]);
            }
        }

        if (candidateLetters.Count == 0)
        {
            bool hasAnyActive = false;
            for (int i = 0; i < Model.rows.Count; i++)
            {
                if (Model.rows[i] != null && Model.rows[i].active)
                {
                    hasAnyActive = true;
                    break;
                }
            }

            if (!hasAnyActive) return false;

            for (int i = 0; i < letters.Length; i++) letters[i] = RandomLetter();
            return true;
        }

        for (int i = 0; i < letters.Length; i++) letters[i] = RandomLetter();
        char target = candidateLetters[UnityEngine.Random.Range(0, candidateLetters.Count)];
        int slot = UnityEngine.Random.Range(0, letters.Length);
        letters[slot] = target;
        return true;
    }

    private char RandomLetter()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
    }

    private void TryMove(int dx)
    {
        int nextX = _pivotX + dx;
        string reason;
        if (TryGetCollisionReason(nextX, _pivotY, _pieceOffsets, out reason))
            return;
        _pivotX = nextX;
        SyncPiecePosition();
    }

    private void TryRotate()
    {
        var rotated = new Vector2Int[_pieceOffsets.Length];
        for (int i = 0; i < _pieceOffsets.Length; i++)
            rotated[i] = new Vector2Int(_pieceOffsets[i].y, -_pieceOffsets[i].x);

        if (WouldCollide(_pivotX, _pivotY, rotated)) return;

        for (int i = 0; i < _pieceOffsets.Length; i++)
            _pieceOffsets[i] = rotated[i];

        SyncPiecePosition();
    }

    private void StepDown()
    {
        int nextY = _pivotY - 1;
        if (WouldCollide(_pivotX, nextY, _pieceOffsets))
        {
            LockPiece();
            return;
        }
        _pivotY = nextY;
        SyncPiecePosition();
        TryApplyMatchesDuringFall();
    }

    private void HardDrop()
    {
        while (!WouldCollide(_pivotX, _pivotY - 1, _pieceOffsets))
        {
            _pivotY--;
            SyncPiecePosition();
            TryApplyMatchesDuringFall();
        }

        LockPiece();
    }

    private void TryApplyMatchesDuringFall()
    {
        int matched = ApplyPieceMatches();
        if (matched <= 0) return;
        RefreshCompletedRows();
        UpdateHud();
        LogDebug($"MatchFlow FallingMatched matched={matched}, score={Model.score}, completedWords={Model.completedWords}");
    }

    private bool WouldCollide(int pivotX, int pivotY, Vector2Int[] offsets)
    {
        string _;
        return TryGetCollisionReason(pivotX, pivotY, offsets, out _);
    }

    private bool TryGetCollisionReason(int pivotX, int pivotY, Vector2Int[] offsets, out string reason)
    {
        bool hasAnyCellInHorizontalRange = false;
        for (int i = 0; i < offsets.Length; i++)
        {
            int x = pivotX + offsets[i].x;
            int y = pivotY + offsets[i].y;

            if (y < _minGridY)
            {
                reason = $"bottom-boundary at cell={i}, y={y}, minGridY={_minGridY}";
                return true;
            }
            if (x >= 0 && x < _slotCount)
                hasAnyCellInHorizontalRange = true;
        }

        if (!hasAnyCellInHorizontalRange)
        {
            reason = $"horizontal-outside-all-cells, pivot=({pivotX},{pivotY}), slotCount={_slotCount}";
            return true;
        }
        reason = "";
        return false;
    }

    private void LockPiece()
    {
        LogDebug($"MatchFlow LockPiece start pivot=({_pivotX},{_pivotY}), letters={new string(_pieceLetters)}");
        int matched = ApplyPieceMatches();
        LogDebug($"MatchFlow ApplyPieceMatches end matched={matched}, score={Model.score}");
        if (matched > 0)
        {
            RefreshCompletedRows();
            LogDebug($"MatchFlow RefreshCompletedRows end completedWords={Model.completedWords}, nextWordIndex={Model.nextWordIndex}, score={Model.score}");
        }
        else
        {
            LogDebug("MatchFlow No matched cell, skip RefreshCompletedRows");
        }

        UpdateHud();
        LogDebug($"MatchFlow UpdateHud level={GameContext.CurrentLevel}, score={Model.score}, remain={Mathf.Max(0, Model.levelWords.Count - Model.completedWords)}");
        SpawnNextPiece();
        LogDebug("MatchFlow SpawnNextPiece done");
    }

    private int ApplyPieceMatches()
    {
        int matched = 0;
        for (int i = 0; i < _pieceOffsets.Length; i++)
        {
            int x = _pivotX + _pieceOffsets[i].x;
            int y = _pivotY + _pieceOffsets[i].y;
            int uiRow = ResolveUiRowByGridY(y);
            if (uiRow < 0 || uiRow >= Model.rows.Count)
            {
                LogDebug($"MatchFlow Cell[{i}] skip: grid=({x},{y}), uiRow={uiRow}");
                continue;
            }
            var row = Model.rows[uiRow];
            if (row == null || !row.active)
            {
                LogDebug($"MatchFlow Cell[{i}] skip: uiRow={uiRow}, rowNullOrInactive");
                continue;
            }
            if (x < 0 || x >= row.answer.Length)
            {
                LogDebug($"MatchFlow Cell[{i}] skip: uiRow={uiRow}, x={x}, answerLen={row.answer.Length}");
                continue;
            }
            if (!row.holeOpen[x])
            {
                LogDebug($"MatchFlow Cell[{i}] skip: uiRow={uiRow}, x={x}, holeClosed");
                continue;
            }

            char expected = char.ToUpperInvariant(row.answer[x]);
            char input = char.ToUpperInvariant(_pieceLetters[i]);
            if (input != expected)
            {
                LogDebug($"MatchFlow Cell[{i}] mismatch: uiRow={uiRow}, x={x}, input={input}, expected={expected}");
                continue;
            }

            row.holeOpen[x] = false;
            row.displayChars[x] = expected;
            matched++;
            Model.score += 10;
            View.RenderRow(uiRow, row);
            LogDebug($"MatchFlow Cell[{i}] matched: uiRow={uiRow}, x={x}, letter={expected}, score={Model.score}, renderRowCalled=true");
        }
        return matched;
    }

    private int ResolveUiRowByGridY(int gridY)
    {
        if (_uiRowYMap.Count == 0) return -1;

        float y = GridToLocal(0, gridY).y;
        float threshold = Mathf.Max(4f, _rowStep * 0.45f);

        int bestRow = -1;
        float bestDist = float.MaxValue;
        foreach (var pair in _uiRowYMap)
        {
            float dist = Mathf.Abs(pair.Value - y);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestRow = pair.Key;
            }
        }

        if (bestDist <= threshold) return bestRow;
        LogDebug($"MatchFlow ResolveUiRow miss: gridY={gridY}, localY={y:F1}, bestRow={bestRow}, bestDist={bestDist:F1}, threshold={threshold:F1}");
        return -1;
    }

    private void RefreshCompletedRows()
    {
        for (int i = 0; i < Model.rows.Count; i++)
        {
            var row = Model.rows[i];
            if (row == null || !row.active) continue;
            if (!IsRowCompleted(row)) continue;

            LogDebug($"MatchFlow RowComplete uiRow={i}, answer={row.answer}, scoreBeforeBonus={Model.score}");
            Model.completedWords++;
            Model.score += 100;

            if (Model.nextWordIndex < Model.levelWords.Count)
            {
                Model.rows[i] = CreateRowState(Model.levelWords[Model.nextWordIndex]);
                Model.nextWordIndex++;
            }
            else
            {
                Model.rows[i] = CreateInactiveRow();
            }

            View.RenderRow(i, Model.rows[i]);
            var newRow = Model.rows[i];
            string nextAnswer = newRow == null ? "" : newRow.answer;
            bool nextActive = newRow != null && newRow.active;
            LogDebug($"MatchFlow RowRefresh uiRow={i}, nextActive={nextActive}, nextAnswer={nextAnswer}, completedWords={Model.completedWords}, score={Model.score}");
        }

        if (Model.completedWords >= Model.levelWords.Count)
        {
            ProgressManager.SetMaxLevel(GameContext.CurrentLexicon, GameContext.CurrentLevel + 1);
            MarkLevelFinished();
        }
    }

    private bool IsRowCompleted(GamePlayUIModel.WordRowState row)
    {
        for (int i = 0; i < row.answer.Length; i++)
        {
            if (row.holeOpen[i]) return false;
        }
        return true;
    }

    private void MarkLevelFinished()
    {
        Model.levelFinished = true;
        View.SetPieceVisible(false);
    }

    private void SyncPiecePosition()
    {
        Vector2 pos = GridToLocal(_pivotX, _pivotY);
        View.SetPiecePosition(pos);
    }

    private Vector2 GridToLocal(int gridX, int gridY)
    {
        if (_columnXs.Count == 0 || _rowYByGrid.Count == 0)
            return Vector2.zero;

        int xIndex = Mathf.Clamp(gridX, 0, _columnXs.Count - 1);
        float x = _columnXs[xIndex];

        float y;
        if (gridY < _rowYByGrid.Count)
        {
            int yIndex = Mathf.Clamp(gridY, 0, _rowYByGrid.Count - 1);
            y = _rowYByGrid[yIndex];
        }
        else
        {
            float topY = _rowYByGrid[_rowYByGrid.Count - 1];
            y = topY + (gridY - (_rowYByGrid.Count - 1)) * _rowStep;
        }

        return new Vector2(x, y);
    }

    private void LogDebug(string message)
    {
        if (!EnableDebugLog) return;
        Debug.Log($"[GamePlayUI] {message}");
    }
}
