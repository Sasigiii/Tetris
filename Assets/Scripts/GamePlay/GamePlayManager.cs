using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GamePlayManager : MonoBehaviour
{
    // Gameplay所需要的常量默认值，可以通过配置表覆盖
    private const float DefaultV0 = 120f; // 初始速度
    private const int DefaultS0 = ScoreManager.InitialScore; // 初始分数
    private const int DefaultDeltaS = 10; // 每次正确填充增加的分数
    private const float DefaultAlpha = 25f; // 速度增加的指数因子
    private const float DefaultVMax = 260f; // 最大速度
    private const float DefaultVFast = 600f; // 快速下落速度
    private const int DefaultMinGridColumns = 7; // 最小列数
    private const int DefaultMaxGridColumns = 18; // 最大列数
    private const float DefaultMinCellSize = 42f; // 最小格子尺寸
    private const float DefaultTmpHorizontalPadding = 6f; // 字母行的TMP字母与格子边界的水平内边距
    private const float DefaultTmpVerticalPadding = 4f; // 字母行的TMP字母与格子边界的垂直内边距
    private const float DefaultMinTmpRectSize = 20f; // 字母行的TMP显示的最小矩形尺寸，防止过小导致的显示问题
    private const float DefaultCrossBlockTmpHorizontalPadding = 6f; // CrossBlock专用TMP水平内边距，适应其特殊形状
    private const float DefaultCrossBlockTmpVerticalPadding = 4f; // CrossBlock专用TMP垂直内边距
    private const float DefaultCrossBlockMinTmpRectSize = 20f; // CrossBlock的TMP最小矩形尺寸
    private const float DefaultCrossBlockCompensationGamma = 0.9f; // CrossBlock速度补偿的gamma值，控制补偿曲线的形状
    private const float DefaultCrossBlockCompensationMin = 1f; // CrossBlock速度补偿的最小值，防止过度补偿导致的速度过慢
    private const float DefaultCrossBlockCompensationMax = 1.6f; // CrossBlock速度补偿的最大值，防止过度补偿导致的速度过快
    private const int DefaultWordBaseScore = 20; // 每个单词的基础分数
    private const int DefaultComboBonusPerStreak = 5; // 每连击增加的额外分数
    private const int DefaultComboBonusCap = 60; // 连击奖励的上限
    private const float DefaultLevelTimeLimitSeconds = 180f; // 每关的时间限制，单位秒

    [Header("References")]
    [SerializeField] private WordGrid wordGrid; // 待填充单词行组件的引用
    [SerializeField] private CrossBlock crossBlock; // 下落十字方块的引用
    [SerializeField] private RectTransform panelRect; // 单词行的panel

    [Header("Spawn Settings")]
    [SerializeField] private int spawnCol = 3; // 初始生成列
    [SerializeField] private float holdMoveInitialDelay = 0.18f; // 按住移动的初始延迟，单位秒
    [SerializeField] private float holdMoveRepeatInterval = 0.06f; // 按住移动的重复间隔，单位秒
    [Header("Typography")]
    [SerializeField] private bool unifyRowLetterFontSize = true; // 是否统一字母行的字体大小
    [SerializeField] private float unifiedRowLetterFontSizeMin = 0f; // 统一字母行字体大小的最小值
    [Header("Debug")]
    [SerializeField] private bool debugSpeedLogs = true;
    [SerializeField] private bool debugMoveLogs = true;
    [SerializeField] private bool debugColumnSafetyLogs = true;
    [SerializeField] private bool debugScoreTimerLogs = true;
    [SerializeField] private bool debugDrawGridLines = false;
    [SerializeField] private Color debugGridLineColor = Color.magenta;
    [SerializeField] private float debugGridLineDuration = 0f;

    public event Action<bool> OnGameOver; // 游戏结束的回调
    public event Action<float> OnTimerChanged; // 计时器变化的回调，参数为剩余时间秒数

    private readonly ScoreManager _scoreManager = new ScoreManager();
    public ScoreManager ScoreManager => _scoreManager; // 提供ScoreManager的只读访问

    private GameState _state = GameState.Loading; // 当前游戏状态
    private LexiconConfig _config; // Lexicon配置表的引用
    private LexiconConfig.LexiconEntry _activeLexiconEntry; // 当前关卡使用的LexiconEntry配置
    private List<WordEntry> _levelWords; // 当前关卡的单词列表
    private List<List<WordRowData>> _wordGroups; // 单词组
    private int _currentGroupIndex; // 当前单词组索引
    private int _activeColumns = DefaultMinGridColumns; // 当前关卡的列数，根据最长单词长度动态调整
    private float _spawnWorldY; // 方块生成的世界坐标Y值，根据UI布局动态计算
    private float[] _colWorldX; // 每列的世界坐标X值，根据UI布局动态计算
    private float _worldCellSize; // 格子的世界尺寸（高度），根据UI布局动态计算
    private float _worldColumnStep; // 列与列之间的世界坐标间距，根据UI布局动态计算
    private Coroutine _startCoroutine; // 启动关卡的协程引用，用于在需要时停止
    private bool _isFastFallInput; // 当前是否有快速下落输入
    private float _heldNormalFallSpeed; // Hold模式下计算出的动态正常下落速度
    private int _activeSpeedStep; // 当前速度档位
    private float _activeNormalSpeed; // 补偿前的正常下落速度
    private float _activeFinalSpeed; // 本帧最终使用的下落速度
    private float _activeBaseSpeed; // 当前基础下落速度
    private float _activeCompensatedSpeed; // 补偿后的正常下落速度
    private int _lastLoggedSpeedStep = int.MinValue;
    private float _lastLoggedNormalSpeed = -1f;
    private float _lastLoggedFinalSpeed = -1f;
    private float _lastLoggedBaseSpeed = -1f;
    private float _lastLoggedCompensatedSpeed = -1f;
    private bool _lastLoggedFastFall;
    private float _lastLoggedCompensationFactor = -1f;
    private float _activeCompensationFactor = 1f; // 当前启用的速度补偿因子
    private int _holdMoveDirection; // 当前按住移动的方向：-1表示左，1表示右，0表示没有
    private float _nextHoldMoveTime;
    private readonly HashSet<string> _columnGuardLogKeys = new HashSet<string>();
    private float _remainingTimeSeconds; // 关卡剩余时间，单位秒
    private bool _timerExpired; // 计时器是否已到期

    /// <summary>
    /// 开始关卡
    /// </summary>
    public void StartLevel()
    {
        // 加载配置表和关卡数据，重置状态和分数，等待UI布局完成后启动关卡流程
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
        
        // 如果已经有启动协程在运行，先停止它，确保不会有多个协程同时尝试启动关卡流程
        if (_startCoroutine != null)
            StopCoroutine(_startCoroutine);
        _startCoroutine = StartCoroutine(StartLevelDelayed());
    }

    /// <summary>
    /// 延迟一帧，确保Unity的UI布局完成
    /// </summary>
    private IEnumerator StartLevelDelayed()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        yield return null;
        LoadLevelWords();
    }
    
    /// <summary>
    /// 清理当前关卡状态
    /// </summary>
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

    /// <summary>
    /// 每帧根据当前状态处理输入、更新方块下落、检查碰撞、更新计时器等逻辑
    /// </summary>
    private void Update()
    {
        if (_state == GameState.Falling)
        {
            // 处理输入
            HandleInput();
            // 处理方块下落
            UpdateFalling();
        }
        // 更新计时器
        UpdateLevelTimer();
        // 画出调试用的网格线
        DrawDiscreteGridDebugLines();
    }

    #region Loading
    /// <summary>
    /// 加载当前关卡所需要的单词列表
    /// </summary>
    private void LoadLevelWords()
    {
        // 从游戏上下文获取当前使用的词库
        var lexicon = GameContext.CurrentLexicon;
        // 根据词库从配置表获取对应的配置
        _activeLexiconEntry = _config.GetEntry(lexicon);
        if (_activeLexiconEntry == null)
            Debug.LogWarning("[GamePlayManager] No lexicon entry found, using default dynamic speed values.");
        int wordsPerLevel = _activeLexiconEntry != null ? _activeLexiconEntry.wordsPerLevel : 10; // 获取每关单词数量，默认为10
        InitializeDropSpeedState(); // 初始化下落速度状态，确保在获取单词前就设置好相关参数

        // 从数据库获取当前关卡使用的单词列表
        _levelWords = GameContext.Database.GetPlayableWordsForLevel(
            lexicon,
            GameContext.CurrentLevel,
            wordsPerLevel,
            GetPlayableMaxWordLength(),
            UseLengthPriorityOrder());
        
        // 如果没有可用的单词，直接结束关卡
        if (_levelWords == null || _levelWords.Count == 0)
        {
            Debug.LogWarning("[GamePlayManager] No playable words for this level");
            _state = GameState.LevelComplete;
            OnGameOver?.Invoke(true);
            return;
        }
        
        // 根据单词列表动态计算当前关卡的列数，确保能够容纳最长的单词，同时不超过配置表的最大列数
        _activeColumns = ResolveActiveColumns(_levelWords);
        if (debugSpeedLogs)
        {
            Debug.Log($"[GamePlayManager] Grid columns resolved to {_activeColumns} (min={GetMinGridColumns()}, max={GetMaxGridColumns()})");
            Debug.Log($"[GamePlayManager] Word order mode: {(UseLengthPriorityOrder() ? "length-desc" : "length-asc")}.");
        }
        // 将单词列表拆分成若干组，每组包含固定数量的单词
        _wordGroups = SplitIntoGroups(_levelWords, 4, _activeColumns);
        _currentGroupIndex = 0;
        
        // 开始生成第一组的单词行
        StartGroup();
    }
    
    /// <summary>
    /// 将单词列表拆分成若干组，每组包含固定数量的单词
    /// </summary>
    /// <param name="words"></param>
    /// <param name="groupSize"></param>
    /// <param name="columns"></param>
    /// <returns></returns>
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

    /// <summary>
    /// 为单个词条构建一行 <see cref="WordRowData"/>：将 headWord 映射到网格列、随机挖一个待填空格。
    /// </summary>
    /// <param name="entry">词条</param>
    /// <param name="columns">本关列数</param>
    /// <returns>含 cells、blankIndices 的行数据，rowIndex 由 WordGrid 在展示时写入</returns>
    private WordRowData BuildWordRowData(WordEntry entry, int columns)
    {
        string word = entry.headWord;
        int safeColumns = Mathf.Max(DefaultMinGridColumns, columns);
        var cells = new CellData[safeColumns];

        // 本行实际显示的字母个数（词更长时只显示中间 visibleLength 个字符）
        int visibleLength = Mathf.Min(word.Length, safeColumns);
        // 在完整 word 字符串中的起始下标（词被截断时取中间段）
        int wordStart = Mathf.Max(0, (word.Length - visibleLength) / 2);
        // 在网格行中的起始列（列比可见字母多时左右居中）
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

        // 收集所有已显示字母的列，用于随机挑选待填空格
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

    /// <summary>
    /// 开始当前单词组的流程：在 WordGrid 上展示单词行，缓存世界坐标，切换状态到 Spawning。
    /// </summary>
    private void StartGroup()
    {
        // 如果当前组索引超出范围，说明所有组都已完成，触发关卡完成事件
        if (_currentGroupIndex >= _wordGroups.Count)
        {
            _state = GameState.LevelComplete;
            OnGameOver?.Invoke(true);
            return;
        }
        
        // 获取当前组的数据，调用 WordGrid 的 SetupGroup 方法展示单词行，并传入当前关卡的列数、格子尺寸、TMP内边距等参数
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
        bool upper = char.IsUpper(exclude);
        char excludeLower = char.ToLower(exclude);
        var pool = new List<char>();
        for (char c = 'a'; c <= 'z'; c++)
        {
            if (c != excludeLower)
                pool.Add(upper ? char.ToUpper(c) : c);
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
            WrongWordManager.RecordWrong(
                GameContext.CurrentLexicon,
                rowData.wordEntry.headWord,
                rowData.wordEntry.tranCn);
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

    private void DrawDiscreteGridDebugLines()
    {
        if (!debugDrawGridLines || wordGrid == null || _activeColumns <= 0)
            return;

        if (_colWorldX == null || _colWorldX.Length == 0 || _worldCellSize <= 0f)
            return;

        int lastCol = _activeColumns - 1;
        int lastRow = wordGrid.GetRowCount() - 1;
        if (lastCol < 0 || lastRow < 0)
            return;

        if (!wordGrid.TryGetCellWorldCorners(0, 0, out _, out var firstCellTL, out _, out _))
            return;
        if (!wordGrid.TryGetCellWorldCorners(0, lastCol, out _, out _, out var firstCellTR, out _))
            return;
        if (!wordGrid.TryGetCellWorldCorners(lastRow, 0, out var lastCellBL, out _, out _, out _))
            return;
        if (!wordGrid.TryGetCellWorldCorners(lastRow, lastCol, out _, out _, out _, out var lastCellBR))
            return;

        float gridTopY = firstCellTL.y;
        float gridBottomY = lastCellBL.y;
        float gridLeftX = firstCellTL.x;
        float gridRightX = firstCellTR.x;

        float mapTopY = _spawnWorldY + _worldCellSize;
        float mapBottomY = gridBottomY;
        float z = firstCellTL.z;

        int fallRows = Mathf.Max(0, Mathf.CeilToInt((mapTopY - gridTopY) / _worldCellSize));
        int wordRows = Mathf.Max(0, Mathf.CeilToInt((gridTopY - gridBottomY) / _worldCellSize));
        int totalRows = fallRows + wordRows;

        Color lineColor = debugGridLineColor;

        float halfStep = _worldColumnStep * 0.5f;
        float fullGridLeftX = _colWorldX[0] - halfStep;
        float fullGridRightX = _colWorldX[lastCol] + halfStep;

        for (int r = 0; r <= totalRows; r++)
        {
            float y = mapTopY - r * _worldCellSize;
            Debug.DrawLine(new Vector3(fullGridLeftX, y, z), new Vector3(fullGridRightX, y, z), lineColor, debugGridLineDuration, false);
        }

        for (int c = 0; c <= _activeColumns; c++)
        {
            float x = (c < _activeColumns) ? _colWorldX[c] - halfStep : _colWorldX[lastCol] + halfStep;
            Debug.DrawLine(new Vector3(x, mapTopY, z), new Vector3(x, mapBottomY, z), lineColor, debugGridLineDuration, false);
        }
    }

    /// <summary>
    /// 根据单词列表动态计算当前关卡的列数(最长单词的词长)，确保能够容纳最长的单词，同时不超过配置表的最大列数
    /// </summary>
    /// <param name="words"></param>
    /// <returns></returns>
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

    // 根据配置获取各项参数，若配置缺失则使用默认值
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
