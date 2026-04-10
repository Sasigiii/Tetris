using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GamePlayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGrid wordGrid;
    [SerializeField] private CrossBlock crossBlock;
    [SerializeField] private RectTransform panelRect;

    [Header("Spawn Settings")]
    [SerializeField] private int spawnCol = 3;

    public event Action<bool> OnGameOver;

    private readonly ScoreManager _scoreManager = new ScoreManager();
    public ScoreManager ScoreManager => _scoreManager;

    private GameState _state = GameState.Loading;
    private LexiconConfig _config;
    private List<WordEntry> _levelWords;
    private List<List<WordRowData>> _wordGroups;
    private int _currentGroupIndex;
    private float _spawnWorldY;
    private float[] _colWorldX;
    private float _worldCellSize;
    private Coroutine _startCoroutine;

    public void StartLevel()
    {
        _config = Resources.Load<LexiconConfig>("LexiconConfig");
        _state = GameState.Loading;

        if (crossBlock != null)
            crossBlock.gameObject.SetActive(false);

        _scoreManager.Reset();

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
    }

    #region Loading

    private void LoadLevelWords()
    {
        var lexicon = GameContext.CurrentLexicon;
        var entry = _config.GetEntry(lexicon);
        int wordsPerLevel = entry != null ? entry.wordsPerLevel : 10;

        _levelWords = GameContext.Database.GetPlayableWordsForLevel(
            lexicon, GameContext.CurrentLevel, wordsPerLevel);

        if (_levelWords == null || _levelWords.Count == 0)
        {
            Debug.LogWarning("[GamePlayManager] No playable words for this level");
            _state = GameState.LevelComplete;
            OnGameOver?.Invoke(true);
            return;
        }

        _wordGroups = SplitIntoGroups(_levelWords, 4);
        _currentGroupIndex = 0;

        StartGroup();
    }

    private List<List<WordRowData>> SplitIntoGroups(List<WordEntry> words, int groupSize)
    {
        var groups = new List<List<WordRowData>>();
        var currentGroup = new List<WordRowData>();

        for (int i = 0; i < words.Count; i++)
        {
            currentGroup.Add(BuildWordRowData(words[i]));
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

    private WordRowData BuildWordRowData(WordEntry entry)
    {
        string word = entry.headWord.ToLower();
        int offset = (7 - word.Length) / 2;
        var cells = new CellData[7];

        for (int c = 0; c < 7; c++)
        {
            int wordIdx = c - offset;
            cells[c] = new CellData
            {
                columnIndex = c,
                letter = (wordIdx >= 0 && wordIdx < word.Length) ? word[wordIdx] : '\0',
                isBlank = false,
                isFilled = false
            };
        }

        var allIndices = new List<int>();
        for (int i = 0; i < word.Length; i++)
            allIndices.Add(i + offset);

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
        wordGrid.SetupGroup(group);

        CacheWorldPositions();

        _state = GameState.Spawning;
        SpawnBlock();
    }

    private void NextGroup()
    {
        _currentGroupIndex++;
        _state = GameState.GroupTransition;
        StartGroup();
    }

    private void CacheWorldPositions()
    {
        _colWorldX = new float[WordGrid.ColumnsPerRow];
        for (int c = 0; c < WordGrid.ColumnsPerRow; c++)
        {
            _colWorldX[c] = wordGrid.GetCellWorldPos(0, c).x;
        }

        if (WordGrid.ColumnsPerRow >= 2)
            _worldCellSize = Mathf.Abs(_colWorldX[1] - _colWorldX[0]);
        else
            _worldCellSize = 1f;

        float panelTopWorldY = panelRect.TransformPoint(
            new Vector3(0, panelRect.rect.height * (1f - panelRect.pivot.y), 0)).y;
        _spawnWorldY = panelTopWorldY - _worldCellSize;

        crossBlock.SetColumnPositions(_colWorldX, _worldCellSize);
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
        var unfilled = rowData.GetUnfilledBlankIndices();
        if (unfilled.Count == 0)
        {
            if (wordGrid.IsGroupComplete())
                NextGroup();
            return;
        }

        int pickIdx = unfilled[Random.Range(0, unfilled.Count)];
        char correctLetter = rowData.cells[pickIdx].letter;

        char[] decoys = GenerateDecoys(correctLetter, 3);
        crossBlock.Initialize(correctLetter, decoys, spawnCol, _spawnWorldY);
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
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            crossBlock.MoveLeft();
            AudioManager.Instance?.PlayEvent("blockMove");
        }

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            crossBlock.MoveRight();
            AudioManager.Instance?.PlayEvent("blockMove");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            crossBlock.Rotate();
            AudioManager.Instance?.PlayEvent("blockMove");
        }

        bool fastFall = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        crossBlock.SetFastFall(fastFall);
    }

    #endregion

    #region Falling & Collision

    private void UpdateFalling()
    {
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
        if (rowData == null || bottomCol < 0 || bottomCol >= 7)
        {
            ResetBlock();
            return;
        }

        var cell = rowData.cells[bottomCol];

        if (cell.isBlank && !cell.isFilled && cell.letter == bottomLetter)
        {
            _scoreManager.AddCorrect();
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

    private void ResetBlock()
    {
        crossBlock.ResetToTop(_spawnWorldY);
        _state = GameState.Falling;
    }

    #endregion
}
