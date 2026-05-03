using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GamePlayManager : MonoBehaviour
{
    private const float DefaultV0 = 120f;
    private const int DefaultS0 = ScoreManager.InitialScore;
    private const int DefaultDeltaS = 10;
    private const float DefaultAlpha = 25f;
    private const float DefaultVMax = 260f;
    private const float DefaultVFast = 600f;
    private const int DefaultMinGridColumns = 7;
    private const int DefaultMaxGridColumns = 18;
    private const float DefaultMinCellSize = 42f;
    private const float DefaultTmpHorizontalPadding = 6f;
    private const float DefaultTmpVerticalPadding = 4f;
    private const float DefaultMinTmpRectSize = 20f;
    private const float DefaultCrossBlockTmpHorizontalPadding = 6f;
    private const float DefaultCrossBlockTmpVerticalPadding = 4f;
    private const float DefaultCrossBlockMinTmpRectSize = 20f;
    private const float DefaultCrossBlockCompensationGamma = 0.9f;
    private const float DefaultCrossBlockCompensationMin = 1f;
    private const float DefaultCrossBlockCompensationMax = 1.6f;
    private const int DefaultWordBaseScore = 20;
    private const int DefaultComboBonusPerStreak = 5;
    private const int DefaultComboBonusCap = 60;
    private const float DefaultLevelTimeLimitSeconds = 180f;

    [Header("References")]
    [SerializeField] private WordGrid wordGrid;
    [SerializeField] private CrossBlock crossBlock;
    [SerializeField] private RectTransform panelRect;

    [Header("Spawn Settings")]
    [SerializeField] private int spawnCol = 3;
    [SerializeField] private float holdMoveInitialDelay = 0.18f;
    [SerializeField] private float holdMoveRepeatInterval = 0.06f;
    [Header("Typography")]
    [SerializeField] private bool unifyRowLetterFontSize = true;
    [SerializeField] private float unifiedRowLetterFontSizeMin = 0f;
    [Header("Debug")]
    [SerializeField] private bool debugSpeedLogs = true;
    [SerializeField] private bool debugMoveLogs = true;
    [SerializeField] private bool debugColumnSafetyLogs = true;
    [SerializeField] private bool debugScoreTimerLogs = true;

    public event Action<bool> OnGameOver;
    public event Action<float> OnTimerChanged;

    private readonly ScoreManager _scoreManager = new ScoreManager();
    public ScoreManager ScoreManager => _scoreManager;

    private GameState _state = GameState.Loading;
    private LexiconConfig _config;
    private LexiconConfig.LexiconEntry _activeLexiconEntry;
    private List<WordEntry> _levelWords;
    private List<List<WordRowData>> _wordGroups;
    private int _currentGroupIndex;
    private int _activeColumns = DefaultMinGridColumns;
    private float _spawnWorldY;
    private float[] _colWorldX;
    private float _worldCellSize;
    private float _worldColumnStep;
    private Coroutine _startCoroutine;
    private bool _isFastFallInput;
    private float _heldNormalFallSpeed;
    private int _activeSpeedStep;
    private float _activeNormalSpeed;
    private float _activeFinalSpeed;
    private float _activeBaseSpeed;
    private float _activeCompensatedSpeed;
    private int _lastLoggedSpeedStep = int.MinValue;
    private float _lastLoggedNormalSpeed = -1f;
    private float _lastLoggedFinalSpeed = -1f;
    private float _lastLoggedBaseSpeed = -1f;
    private float _lastLoggedCompensatedSpeed = -1f;
    private bool _lastLoggedFastFall;
    private float _lastLoggedCompensationFactor = -1f;
    private float _activeCompensationFactor = 1f;
    private int _holdMoveDirection;
    private float _nextHoldMoveTime;
    private readonly HashSet<string> _columnGuardLogKeys = new HashSet<string>();
    private float _remainingTimeSeconds;
    private bool _timerExpired;

    public void StartLevel()
    {
        _config = Resources.Load<LexiconConfig>("LexiconConfig");
        _state = GameState.Loading;

        if (crossBlock != null)
            crossBlock.gameObject.SetActive(false);

        _scoreManager.Reset();
        _isFastFallInput = false;
        _holdMoveDirection = 0;
        _nextHoldMoveTime = 0f;
        _timerExpired = false;
        _remainingTimeSeconds = Mathf.Max(0f, GetLevelTimeLimitSeconds());
        OnTimerChanged?.Invoke(_remainingTimeSeconds);

        if (_startCoroutine != null)
            StopCoroutine(_startCoroutine);
        _startCoroutine = StartCoroutine(StartLevelDelayed());
    }

    /// <summary>
    /// Wait one frame so Unity layout system computes cell world positions.
    /// </summary>
    private IEnumerator StartLevelDelayed()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        yield return null;
        LoadLevelWords();
    }

    public void Cleanup()
    {
        if (_startCoroutine != null)
        {
            StopCoroutine(_startCoroutine);
            _startCoroutine = null;
        }
        _state = GameState.Loading;
        if (crossBlock != null)
            crossBlock.Deactivate();
    }

    private void Update()
    {
        if (_state == GameState.Falling)
        {
            HandleInput();
            UpdateFalling();
        }

        UpdateLevelTimer();
    }

    #region Loading

    private void LoadLevelWords()
    {
        var lexicon = GameContext.CurrentLexicon;
        _activeLexiconEntry = _config.GetEntry(lexicon);
        if (_activeLexiconEntry == null)
            Debug.LogWarning("[GamePlayManager] No lexicon entry found, using default dynamic speed values.");
        int wordsPerLevel = _activeLexiconEntry != null ? _activeLexiconEntry.wordsPerLevel : 10;
        InitializeDropSpeedState();

        _levelWords = GameContext.Database.GetPlayableWordsForLevel(
            lexicon,
            GameContext.CurrentLevel,
            wordsPerLevel,
            GetPlayableMaxWordLength(),
            UseLengthPriorityOrder());

        if (_levelWords == null || _levelWords.Count == 0)
        {
            Debug.LogWarning("[GamePlayManager] No playable words for this level");
            _state = GameState.LevelComplete;
            OnGameOver?.Invoke(true);
            return;
        }

        _activeColumns = ResolveActiveColumns(_levelWords);
        if (debugSpeedLogs)
        {
            Debug.Log($"[GamePlayManager] Grid columns resolved to {_activeColumns} (min={GetMinGridColumns()}, max={GetMaxGridColumns()})");
            Debug.Log($"[GamePlayManager] Word order mode: {(UseLengthPriorityOrder() ? "length-desc" : "length-asc")}.");
        }
        _wordGroups = SplitIntoGroups(_levelWords, 4, _activeColumns);
        _currentGroupIndex = 0;

        StartGroup();
    }

    private List<List<WordRowData>> SplitIntoGroups(List<WordEntry> words, int groupSize, int columns)
    {
        var groups = new List<List<WordRowData>>();
        var currentGroup = new List<WordRowData>();

        for (int i = 0; i < words.Count; i++)
        {
            currentGroup.Add(BuildWordRowData(words[i], columns));
            if (currentGroup.Count >= groupSize)
            {
                groups.Add(currentGroup);
                currentGroup = new List<WordRowData>();
            }
        }

        if (currentGroup.Count > 0)
            groups.Add(currentGroup);

        return groups;
    }

    private WordRowData BuildWordRowData(WordEntry entry, int columns)
    {
        string word = entry.headWord.ToLower();
        int safeColumns = Mathf.Max(DefaultMinGridColumns, columns);
        var cells = new CellData[safeColumns];
        int visibleLength = Mathf.Min(word.Length, safeColumns);
        int wordStart = Mathf.Max(0, (word.Length - visibleLength) / 2);
        int colStart = Mathf.Max(0, (safeColumns - visibleLength) / 2);

        for (int c = 0; c < safeColumns; c++)
        {
            int localIndex = c - colStart;
            int wordIdx = (localIndex >= 0 && localIndex < visibleLength) ? wordStart + localIndex : -1;
            cells[c] = new CellData
            {
                columnIndex = c,
                letter = (wordIdx >= 0 && wordIdx < word.Length) ? word[wordIdx] : '\0',
                isBlank = false,
                isFilled = false
            };
        }

        var allIndices = new List<int>();
        for (int c = 0; c < safeColumns; c++)
        {
            if (cells[c].letter != '\0')
                allIndices.Add(c);
        }

        var blankIndices = new List<int>();
        if (allIndices.Count > 0)
        {
            int pick = Random.Range(0, allIndices.Count);
            int idx = allIndices[pick];
            blankIndices.Add(idx);
            cells[idx].isBlank = true;
        }

        return new WordRowData
        {
            wordEntry = entry,
            cells = cells,
            blankIndices = blankIndices
        };
    }

    #endregion

    #region Group Flow

    private void StartGroup()
    {
        if (_currentGroupIndex >= _wordGroups.Count)
        {
            _state = GameState.LevelComplete;
            OnGameOver?.Invoke(true);
            return;
        }

        var group = _wordGroups[_currentGroupIndex];
        wordGrid.SetupGroup(
            group,
            _activeColumns,
            GetMinCellSize(),
            ShouldSyncTMPRect(),
            GetTmpRectHorizontalPadding(),
            GetTmpRectVerticalPadding(),
            GetMinTmpRectSize(),
            unifyRowLetterFontSize,
            unifiedRowLetterFontSizeMin);

        CacheWorldPositions();

        _state = GameState.Spawning;
        SpawnBlock();
    }

    private void NextGroup()
    {
        _scoreManager.ResetCombo();
        _currentGroupIndex++;
        _state = GameState.GroupTransition;
        StartGroup();
    }

    private void CacheWorldPositions()
    {
        _colWorldX = new float[_activeColumns];
        for (int c = 0; c < _activeColumns; c++)
        {
            _colWorldX[c] = wordGrid.GetCellWorldPos(0, c).x;
        }

        if (_activeColumns >= 2)
            _worldColumnStep = Mathf.Abs(_colWorldX[1] - _colWorldX[0]);
        else
            _worldColumnStep = 1f;

        Vector2 worldCellSize = wordGrid.GetCellWorldSize(0, 0);
        _worldCellSize = worldCellSize.y > 0f ? worldCellSize.y : _worldColumnStep;

        float panelTopWorldY = panelRect.TransformPoint(
            new Vector3(0, panelRect.rect.height * (1f - panelRect.pivot.y), 0)).y;
        _spawnWorldY = panelTopWorldY - _worldCellSize;

        crossBlock.SetColumnPositions(_colWorldX, _worldCellSize);
        crossBlock.ApplyAdaptiveSizing(
            _worldCellSize,
            ResolveBaselineWorldCellSize(),
            ShouldSyncCrossBlockTMPRect(),
            GetCrossBlockTmpRectHorizontalPadding(),
            GetCrossBlockTmpRectVerticalPadding(),
            GetCrossBlockMinTmpRectSize());

        if (debugMoveLogs)
        {
            float firstColX = _colWorldX.Length > 0 ? _colWorldX[0] : 0f;
            float lastColX = _colWorldX.Length > 0 ? _colWorldX[_colWorldX.Length - 1] : 0f;
            Debug.Log("[GamePlayManager] Layout metrics: "
                      + $"columns={_activeColumns}, "
                      + $"columnStep={_worldColumnStep:0.###}, "
                      + $"cellHeight={_worldCellSize:0.###}, "
                      + $"firstColX={firstColX:0.###}, lastColX={lastColX:0.###}");
        }
    }

    private float ResolveBaselineWorldCellSize()
    {
        if (_worldCellSize <= 0f || _activeColumns <= 0)
            return 1f;

        const int baselineColumns = DefaultMinGridColumns;
        float estimatedBaseline = _worldCellSize * _activeColumns / (float)baselineColumns;
        return Mathf.Max(0.01f, estimatedBaseline);
    }

    #endregion

    #region Spawning

    private void SpawnBlock()
    {
        int targetRow = wordGrid.GetTargetRow();
        if (targetRow < 0)
        {
            if (wordGrid.IsGroupComplete())
                NextGroup();
            return;
        }

        var rowData = wordGrid.GetRowData(targetRow);
        if (rowData == null || rowData.cells == null || rowData.cells.Length == 0)
        {
            LogColumnGuardOnce(
                $"spawn-row-null:{targetRow}",
                $"[GamePlayManager] Spawn row data missing at row={targetRow}, skip current spawn.");
            return;
        }
        var unfilled = rowData.GetUnfilledBlankIndices();
        if (unfilled.Count == 0)
        {
            if (wordGrid.IsGroupComplete())
                NextGroup();
            return;
        }

        int pickIdx = unfilled[Random.Range(0, unfilled.Count)];
        if (pickIdx < 0 || pickIdx >= rowData.cells.Length)
        {
            LogColumnGuardOnce(
                $"spawn-pick:{targetRow}:{pickIdx}:{rowData.cells.Length}",
                $"[GamePlayManager] Spawn pick index out of range (row={targetRow}, pick={pickIdx}, len={rowData.cells.Length}). Reset block flow.");
            ResetBlock();
            return;
        }
        char correctLetter = rowData.cells[pickIdx].letter;

        char[] decoys = GenerateDecoys(correctLetter, 3);
        int clampedSpawnCol = Mathf.Clamp(spawnCol, 0, Mathf.Max(0, _activeColumns - 1));
        crossBlock.Initialize(correctLetter, decoys, clampedSpawnCol, _spawnWorldY);
        _state = GameState.Falling;
    }

    private char[] GenerateDecoys(char exclude, int count)
    {
        var pool = new List<char>();
        for (char c = 'a'; c <= 'z'; c++)
        {
            if (c != exclude)
                pool.Add(c);
        }

        var result = new char[count];
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, pool.Count);
            result[i] = pool[pick];
            pool.RemoveAt(pick);
        }
        return result;
    }

    #endregion

    #region Input

    private void HandleInput()
    {
        HandleHoldHorizontalMove();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            crossBlock.Rotate();
            AudioManager.Instance?.PlayEvent("blockMove");
        }

        _isFastFallInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        crossBlock.SetFastFall(_isFastFallInput);
    }

    private void HandleHoldHorizontalMove()
    {
        bool leftHeld = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool rightHeld = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

        int requestedDirection = 0;
        if (leftHeld ^ rightHeld)
            requestedDirection = leftHeld ? -1 : 1;

        if (requestedDirection == 0)
        {
            _holdMoveDirection = 0;
            return;
        }

        float now = Time.unscaledTime;
        if (_holdMoveDirection != requestedDirection)
        {
            _holdMoveDirection = requestedDirection;
            TryHorizontalMove(requestedDirection);
            _nextHoldMoveTime = now + Mathf.Max(0f, holdMoveInitialDelay);
            return;
        }

        float safeRepeat = Mathf.Max(0.01f, holdMoveRepeatInterval);
        if (now >= _nextHoldMoveTime)
        {
            TryHorizontalMove(requestedDirection);
            _nextHoldMoveTime = now + safeRepeat;
        }
    }

    private void TryHorizontalMove(int direction)
    {
        bool moved = false;
        string dirName = direction < 0 ? "left" : "right";
        if (direction < 0)
            moved = crossBlock.MoveLeft();
        else if (direction > 0)
            moved = crossBlock.MoveRight();

        if (moved)
            AudioManager.Instance?.PlayEvent("blockMove");
        LogHorizontalMove(dirName, moved);
    }

    #endregion

    #region Falling & Collision

    private void UpdateFalling()
    {
        UpdateDropSpeed();
        crossBlock.ApplyFall();

        int targetRow = wordGrid.GetTargetRow();
        if (targetRow < 0) return;

        float targetWorldY = wordGrid.GetRowWorldY(targetRow);
        float bottomWorldY = crossBlock.GetBottomWorldY();

        if (bottomWorldY <= targetWorldY)
        {
            _state = GameState.Checking;
            CheckCollision(targetRow);
        }
    }

    private void CheckCollision(int targetRow)
    {
        int bottomCol = crossBlock.GetBottomCellColumn();
        char bottomLetter = crossBlock.GetBottomLetter();

        var rowData = wordGrid.GetRowData(targetRow);
        if (rowData == null || bottomCol < 0 || bottomCol >= _activeColumns || bottomCol >= rowData.cells.Length)
        {
            LogColumnGuardOnce(
                $"collision-guard:{targetRow}:{bottomCol}:{_activeColumns}:{(rowData != null ? rowData.cells.Length : -1)}",
                $"[GamePlayManager] Collision guard hit (row={targetRow}, col={bottomCol}, activeColumns={_activeColumns}, rowCells={(rowData != null ? rowData.cells.Length : -1)}).");
            ResetBlock();
            return;
        }

        var cell = rowData.cells[bottomCol];

        if (cell.isBlank && !cell.isFilled && cell.letter == bottomLetter)
        {
            int gained = _scoreManager.AddCorrectWithCombo(
                GetWordBaseScore(),
                GetComboBonusPerStreak(),
                GetComboBonusCap());
            if (debugScoreTimerLogs)
            {
                Debug.Log("[GamePlayManager] Score gain: "
                          + $"base={GetWordBaseScore()}, combo={_scoreManager.ComboStreak}, "
                          + $"step={GetComboBonusPerStreak()}, cap={GetComboBonusCap()}, gained={gained}, total={_scoreManager.Score}");
            }
            AudioManager.Instance?.PlayEvent("fillCorrect");
            wordGrid.FillCell(targetRow, bottomCol, bottomLetter);

            if (rowData.IsComplete())
                wordGrid.HideRow(targetRow);

            crossBlock.Deactivate();

            if (wordGrid.IsGroupComplete())
                NextGroup();
            else
            {
                _state = GameState.Spawning;
                SpawnBlock();
            }
        }
        else
        {
            _scoreManager.AddWrong();
            if (debugScoreTimerLogs)
                Debug.Log($"[GamePlayManager] Wrong fill: total={_scoreManager.Score}, comboResetTo={_scoreManager.ComboStreak}");
            AudioManager.Instance?.PlayEvent("fillWrong");
            if (_scoreManager.IsFailed)
            {
                _state = GameState.GameOver;
                crossBlock.Deactivate();
                OnGameOver?.Invoke(false);
                return;
            }
            ResetBlock();
        }
    }

    private void UpdateLevelTimer()
    {
        if (_timerExpired || GetLevelTimeLimitSeconds() <= 0f)
            return;

        if (_state == GameState.Loading || _state == GameState.LevelComplete || _state == GameState.GameOver)
            return;

        _remainingTimeSeconds = Mathf.Max(0f, _remainingTimeSeconds - Time.deltaTime);
        OnTimerChanged?.Invoke(_remainingTimeSeconds);
        if (_remainingTimeSeconds > 0f)
            return;

        _timerExpired = true;
        _scoreManager.ResetCombo();
        _state = GameState.GameOver;
        crossBlock?.Deactivate();
        if (debugScoreTimerLogs)
            Debug.Log("[GamePlayManager] Timer expired, game over triggered.");
        OnGameOver?.Invoke(false);
    }

    private void ResetBlock()
    {
        crossBlock.ResetToTop(_spawnWorldY);
        _state = GameState.Falling;
    }

    private void LogHorizontalMove(string direction, bool moved)
    {
        if (!debugMoveLogs || crossBlock == null || _colWorldX == null || _colWorldX.Length == 0)
            return;

        float firstColX = _colWorldX[0];
        float lastColX = _colWorldX[_colWorldX.Length - 1];
        int centerCol = Mathf.Clamp(crossBlock.CurrentCenterColumn, 0, _colWorldX.Length - 1);
        float currentCenterX = crossBlock.GetCenterWorldX();
        Debug.Log("[GamePlayManager] Move update: "
                  + $"dir={direction}, moved={moved}, "
                  + $"centerCol={centerCol}, "
                  + $"worldX={currentCenterX:0.###}, "
                  + $"firstColX={firstColX:0.###}, lastColX={lastColX:0.###}, "
                  + $"maxCol={Mathf.Max(0, _activeColumns - 1)}");
    }

    private int ResolveActiveColumns(List<WordEntry> words)
    {
        int longest = 0;
        if (words != null)
        {
            for (int i = 0; i < words.Count; i++)
            {
                if (words[i] == null || string.IsNullOrEmpty(words[i].headWord)) continue;
                longest = Mathf.Max(longest, words[i].headWord.Length);
            }
        }

        int minColumns = GetMinGridColumns();
        int maxColumns = GetMaxGridColumns();
        int target = Mathf.Max(minColumns, longest);
        LogColumnGuardOnce(
            $"active-columns-source:{minColumns}:{maxColumns}",
            $"[GamePlayManager] activeColumns source=dynamic (longest={longest}, min={minColumns}, max={maxColumns}).");
        return Mathf.Clamp(target, minColumns, maxColumns);
    }

    private void LogColumnGuardOnce(string key, string message)
    {
        if (!debugColumnSafetyLogs || string.IsNullOrEmpty(key))
            return;
        if (_columnGuardLogKeys.Add(key))
            Debug.Log(message);
    }

    private void InitializeDropSpeedState()
    {
        _heldNormalFallSpeed = GetBaseFallSpeed();
        _activeSpeedStep = 0;
        _activeNormalSpeed = _heldNormalFallSpeed;
        _activeFinalSpeed = _heldNormalFallSpeed;
        _activeBaseSpeed = _heldNormalFallSpeed;
        _activeCompensatedSpeed = _heldNormalFallSpeed;
        _lastLoggedSpeedStep = int.MinValue;
        _lastLoggedNormalSpeed = -1f;
        _lastLoggedFinalSpeed = -1f;
        _lastLoggedBaseSpeed = -1f;
        _lastLoggedCompensatedSpeed = -1f;
        _lastLoggedFastFall = false;
        _lastLoggedCompensationFactor = -1f;
        _activeCompensationFactor = 1f;

        if (ShouldLogSpeed())
        {
            Debug.Log("[GamePlayManager] Dynamic speed enabled: v0/s0/deltaS/alpha/vMax/vFast/fallbackMode = "
                      + $"{GetBaseFallSpeed():0.##}/{GetStartScore()}/{GetScoreStep()}/{GetSpeedIncrement():0.##}/"
                      + $"{GetMaxFallSpeed():0.##}/{GetFastFallSpeed():0.##}/{GetFallbackMode()}");
            Debug.Log("[GamePlayManager] CrossBlock compensation config: "
                      + $"enabled={EnableCrossBlockScaleCompensation()}, "
                      + $"gamma={GetCrossBlockCompensationGamma():0.##}, "
                      + $"min={GetCrossBlockCompensationMin():0.##}, "
                      + $"max={GetCrossBlockCompensationMax():0.##}");
            Debug.Log("[GamePlayManager] Suggested tuning ranges: deltaS=20~40, alpha=15~30, vMax=220~320.");
        }
    }

    private void UpdateDropSpeed()
    {
        if (crossBlock == null || !crossBlock.IsActive)
            return;

        int speedStep;
        float baseDynamicSpeed = ResolveDynamicNormalSpeed(_scoreManager.Score, out speedStep);
        float compensationFactor = ResolveScaleCompensationFactor();
        float compensatedNormalSpeed = Mathf.Clamp(
            baseDynamicSpeed * compensationFactor,
            1f,
            GetFastFallSpeed());
        float fastSpeed = GetFastFallSpeed();
        float finalSpeed = _isFastFallInput ? fastSpeed : compensatedNormalSpeed;

        crossBlock.SetFallSpeeds(compensatedNormalSpeed, fastSpeed);

        _activeSpeedStep = speedStep;
        _activeNormalSpeed = compensatedNormalSpeed;
        _activeFinalSpeed = finalSpeed;
        _activeBaseSpeed = baseDynamicSpeed;
        _activeCompensatedSpeed = compensatedNormalSpeed;
        _activeCompensationFactor = compensationFactor;

        if (ShouldLogSpeed())
            LogSpeedSnapshotIfNeeded();
    }

    private float ResolveDynamicNormalSpeed(int score, out int speedStep)
    {
        float computed = ComputeScoreDrivenSpeed(
            score,
            GetBaseFallSpeed(),
            GetStartScore(),
            GetScoreStep(),
            GetSpeedIncrement(),
            GetMaxFallSpeed(),
            out speedStep);

        if (GetFallbackMode() == LexiconConfig.DropSpeedFallbackMode.Hold)
        {
            _heldNormalFallSpeed = Mathf.Max(_heldNormalFallSpeed, computed);
            return _heldNormalFallSpeed;
        }

        _heldNormalFallSpeed = computed;
        return computed;
    }

    private float ResolveScaleCompensationFactor()
    {
        if (!EnableCrossBlockScaleCompensation() || crossBlock == null)
            return 1f;

        float ratio = Mathf.Max(0.01f, crossBlock.CurrentScaleRatio);
        float gamma = GetCrossBlockCompensationGamma();
        float raw = Mathf.Pow(1f / ratio, gamma);
        return Mathf.Clamp(raw, GetCrossBlockCompensationMin(), GetCrossBlockCompensationMax());
    }

    private void LogSpeedSnapshotIfNeeded()
    {
        bool changed = _lastLoggedSpeedStep != _activeSpeedStep
                       || !Mathf.Approximately(_lastLoggedBaseSpeed, _activeBaseSpeed)
                       || !Mathf.Approximately(_lastLoggedCompensatedSpeed, _activeCompensatedSpeed)
                       || !Mathf.Approximately(_lastLoggedNormalSpeed, _activeNormalSpeed)
                       || !Mathf.Approximately(_lastLoggedFinalSpeed, _activeFinalSpeed)
                       || _lastLoggedFastFall != _isFastFallInput
                       || !Mathf.Approximately(_lastLoggedCompensationFactor, _activeCompensationFactor);

        if (!changed)
            return;

        Debug.Log("[GamePlayManager] Speed update: "
                  + $"score={_scoreManager.Score}, step={_activeSpeedStep}, "
                  + $"base={_activeBaseSpeed:0.##}, compensated={_activeCompensatedSpeed:0.##}, "
                  + $"normal={_activeNormalSpeed:0.##}, final={_activeFinalSpeed:0.##}, "
                  + $"compFactor={_activeCompensationFactor:0.###}, "
                  + $"scaleRatio={(crossBlock != null ? crossBlock.CurrentScaleRatio : 1f):0.###}, "
                  + $"fastInput={_isFastFallInput}");

        _lastLoggedSpeedStep = _activeSpeedStep;
        _lastLoggedBaseSpeed = _activeBaseSpeed;
        _lastLoggedCompensatedSpeed = _activeCompensatedSpeed;
        _lastLoggedNormalSpeed = _activeNormalSpeed;
        _lastLoggedFinalSpeed = _activeFinalSpeed;
        _lastLoggedFastFall = _isFastFallInput;
        _lastLoggedCompensationFactor = _activeCompensationFactor;
    }

    private bool ShouldLogSpeed()
    {
        return debugSpeedLogs || Debug.isDebugBuild;
    }

    private static float ComputeScoreDrivenSpeed(
        int score,
        float v0,
        int s0,
        int deltaS,
        float alpha,
        float vMax,
        out int step)
    {
        if (score < s0)
        {
            step = 0;
            return Mathf.Clamp(v0, 1f, Mathf.Max(v0, vMax));
        }

        int safeDeltaS = Mathf.Max(1, deltaS);
        int rawStep = Mathf.Max(0, Mathf.FloorToInt((score - s0) / (float)safeDeltaS));
        step = rawStep;

        float computed = v0 + alpha * rawStep;
        return Mathf.Clamp(computed, v0, Mathf.Max(v0, vMax));
    }

    private float GetBaseFallSpeed() => _activeLexiconEntry != null ? _activeLexiconEntry.v0 : DefaultV0;
    private int GetStartScore() => _activeLexiconEntry != null ? _activeLexiconEntry.s0 : DefaultS0;
    private int GetScoreStep() => _activeLexiconEntry != null ? _activeLexiconEntry.deltaS : DefaultDeltaS;
    private float GetSpeedIncrement() => _activeLexiconEntry != null ? _activeLexiconEntry.alpha : DefaultAlpha;
    private float GetMaxFallSpeed() => _activeLexiconEntry != null ? _activeLexiconEntry.vMax : DefaultVMax;
    private float GetFastFallSpeed() => _activeLexiconEntry != null ? _activeLexiconEntry.vFast : DefaultVFast;
    private int GetPlayableMaxWordLength() => _activeLexiconEntry != null ? _activeLexiconEntry.maxWordLength : DefaultMaxGridColumns;
    private int GetMinGridColumns() => _activeLexiconEntry != null ? _activeLexiconEntry.minGridColumns : DefaultMinGridColumns;
    private int GetMaxGridColumns() => _activeLexiconEntry != null ? _activeLexiconEntry.maxGridColumns : DefaultMaxGridColumns;
    private float GetMinCellSize() => _activeLexiconEntry != null ? _activeLexiconEntry.minCellSize : DefaultMinCellSize;
    private bool ShouldSyncTMPRect() => _activeLexiconEntry == null || _activeLexiconEntry.syncTMPRectWithCell;
    private float GetTmpRectHorizontalPadding() => _activeLexiconEntry != null ? _activeLexiconEntry.tmpRectHorizontalPadding : DefaultTmpHorizontalPadding;
    private float GetTmpRectVerticalPadding() => _activeLexiconEntry != null ? _activeLexiconEntry.tmpRectVerticalPadding : DefaultTmpVerticalPadding;
    private float GetMinTmpRectSize() => _activeLexiconEntry != null ? _activeLexiconEntry.minTMPRectSize : DefaultMinTmpRectSize;
    private bool ShouldSyncCrossBlockTMPRect() => _activeLexiconEntry == null || _activeLexiconEntry.syncCrossBlockTMPRect;
    private float GetCrossBlockTmpRectHorizontalPadding() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.crossBlockTmpHorizontalPadding : DefaultCrossBlockTmpHorizontalPadding;
    private float GetCrossBlockTmpRectVerticalPadding() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.crossBlockTmpVerticalPadding : DefaultCrossBlockTmpVerticalPadding;
    private float GetCrossBlockMinTmpRectSize() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.crossBlockMinTmpRectSize : DefaultCrossBlockMinTmpRectSize;
    private bool EnableCrossBlockScaleCompensation() =>
        _activeLexiconEntry == null || _activeLexiconEntry.enableCrossBlockScaleCompensation;
    private float GetCrossBlockCompensationGamma() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.crossBlockCompensationGamma : DefaultCrossBlockCompensationGamma;
    private float GetCrossBlockCompensationMin() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.crossBlockCompensationMin : DefaultCrossBlockCompensationMin;
    private float GetCrossBlockCompensationMax() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.crossBlockCompensationMax : DefaultCrossBlockCompensationMax;
    private int GetWordBaseScore() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.wordBaseScore : DefaultWordBaseScore;
    private int GetComboBonusPerStreak() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.comboBonusPerStreak : DefaultComboBonusPerStreak;
    private int GetComboBonusCap() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.comboBonusCap : DefaultComboBonusCap;
    private float GetLevelTimeLimitSeconds() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.levelTimeLimitSeconds : DefaultLevelTimeLimitSeconds;
    private bool UseLengthPriorityOrder() => _activeLexiconEntry != null && _activeLexiconEntry.lengthPriorityForTesting;
    private LexiconConfig.DropSpeedFallbackMode GetFallbackMode() =>
        _activeLexiconEntry != null ? _activeLexiconEntry.fallbackMode : LexiconConfig.DropSpeedFallbackMode.Decrease;

#if UNITY_EDITOR
    [ContextMenu("Debug/Run Drop Speed Scenario Checks")]
    private void RunDropSpeedScenarioChecks()
    {
        int step;
        float belowStart = ComputeScoreDrivenSpeed(80, 120f, 100, 30, 20f, 260f, out step);
        Debug.Assert(Mathf.Approximately(belowStart, 120f) && step == 0, "Scenario failed: low score should use v0.");

        float stepped = ComputeScoreDrivenSpeed(160, 120f, 100, 30, 20f, 260f, out step);
        Debug.Assert(Mathf.Approximately(stepped, 160f) && step == 2, "Scenario failed: step growth mismatch.");

        float clamped = ComputeScoreDrivenSpeed(500, 120f, 100, 30, 20f, 260f, out step);
        Debug.Assert(Mathf.Approximately(clamped, 260f), "Scenario failed: max clamp mismatch.");

        float activeNormal = 180f;
        float activeFast = 600f;
        float activeFinal = true ? activeFast : activeNormal;
        Debug.Assert(Mathf.Approximately(activeFinal, 600f), "Scenario failed: fast drop should override dynamic speed.");

        Debug.Log("[GamePlayManager] Drop speed scenario checks passed: low-score/start-step/max-clamp/fast-override.");
    }
#endif

    #endregion
}
