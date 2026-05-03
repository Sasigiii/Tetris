using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class CrossBlock : MonoBehaviour
{
    [Header("Arm References (each has Image + TMP child)")]
    [SerializeField] private RectTransform armUp;
    [SerializeField] private RectTransform armRight;
    [SerializeField] private RectTransform armDown;
    [SerializeField] private RectTransform armLeft;
    [SerializeField] private RectTransform armCenter;

    [Header("Settings")]
    [SerializeField] private float fallSpeed = 120f;
    [SerializeField] private float fastFallSpeed = 600f;

    private const int MinCol = 0;

    private char[] _letters = new char[4]; // Up=0, Right=1, Down=2, Left=3
    private int _rotation;
    private int _centerCol;
    private TextMeshProUGUI[] _armTexts = new TextMeshProUGUI[4];
    private TextMeshProUGUI _centerText;
    private RectTransform[] _arms = new RectTransform[5];
    private RectTransform[] _textRects = new RectTransform[5];
    private Vector2[] _baseArmSizes = new Vector2[5];
    private Vector2[] _baseArmAnchoredPositions = new Vector2[5];
    private bool[] _warnedMissingTMP = new bool[5];

    private bool _isFastFalling;
    private bool _isActive;

    private float[] _colWorldX;
    private float _worldCellSize;
    private int _maxCol;
    private float _currentScaleRatio = 1f;
    private float _lastAppliedScaleRatio = -1f;
    private bool _lastSyncTMPRect;
    private float _lastTMPPaddingX = -1f;
    private float _lastTMPPaddingY = -1f;
    private float _lastMinTMPRectSize = -1f;

    public bool IsActive => _isActive;
    public float CurrentScaleRatio => _currentScaleRatio;
    public int CurrentCenterColumn => _centerCol;

    private void Awake()
    {
        if (armUp != null) _armTexts[0] = armUp.GetComponentInChildren<TextMeshProUGUI>();
        if (armRight != null) _armTexts[1] = armRight.GetComponentInChildren<TextMeshProUGUI>();
        if (armDown != null) _armTexts[2] = armDown.GetComponentInChildren<TextMeshProUGUI>();
        if (armLeft != null) _armTexts[3] = armLeft.GetComponentInChildren<TextMeshProUGUI>();
        if (armCenter != null) _centerText = armCenter.GetComponentInChildren<TextMeshProUGUI>();

        _arms[0] = armUp;
        _arms[1] = armRight;
        _arms[2] = armDown;
        _arms[3] = armLeft;
        _arms[4] = armCenter;
        for (int i = 0; i < _arms.Length; i++)
        {
            if (_arms[i] != null)
            {
                _baseArmSizes[i] = _arms[i].sizeDelta;
                _baseArmAnchoredPositions[i] = _arms[i].anchoredPosition;
            }
        }

        _textRects[0] = _armTexts[0] != null ? _armTexts[0].rectTransform : null;
        _textRects[1] = _armTexts[1] != null ? _armTexts[1].rectTransform : null;
        _textRects[2] = _armTexts[2] != null ? _armTexts[2].rectTransform : null;
        _textRects[3] = _armTexts[3] != null ? _armTexts[3].rectTransform : null;
        _textRects[4] = _centerText != null ? _centerText.rectTransform : null;
    }

    public void SetColumnPositions(float[] colWorldX, float worldCellSize)
    {
        _colWorldX = colWorldX;
        _worldCellSize = worldCellSize;
        _maxCol = _colWorldX != null && _colWorldX.Length > 0 ? _colWorldX.Length - 1 : 0;
        _centerCol = Mathf.Clamp(_centerCol, MinCol, _maxCol);
    }

    public void ApplyAdaptiveSizing(
        float worldCellSize,
        float baselineWorldCellSize,
        bool syncTMPRect,
        float tmpPaddingX,
        float tmpPaddingY,
        float minTMPRectSize)
    {
        if (worldCellSize <= 0f || baselineWorldCellSize <= 0f)
            return;

        _currentScaleRatio = Mathf.Clamp(worldCellSize / baselineWorldCellSize, 0.25f, 2f);
        bool noChange = Mathf.Approximately(_lastAppliedScaleRatio, _currentScaleRatio)
                        && _lastSyncTMPRect == syncTMPRect
                        && Mathf.Approximately(_lastTMPPaddingX, tmpPaddingX)
                        && Mathf.Approximately(_lastTMPPaddingY, tmpPaddingY)
                        && Mathf.Approximately(_lastMinTMPRectSize, minTMPRectSize);
        if (noChange)
            return;

        for (int i = 0; i < _arms.Length; i++)
        {
            var arm = _arms[i];
            if (arm == null) continue;

            Vector2 targetSize = _baseArmSizes[i] * _currentScaleRatio;
            arm.sizeDelta = targetSize;
            arm.anchoredPosition = _baseArmAnchoredPositions[i] * _currentScaleRatio;

            if (!syncTMPRect) continue;

            var textRect = _textRects[i];
            if (textRect == null)
            {
                if (!_warnedMissingTMP[i])
                {
                    Debug.LogWarning($"[CrossBlock] Missing TMP rect at arm index {i}, skip TMP rect sync.");
                    _warnedMissingTMP[i] = true;
                }
                continue;
            }

            float width = Mathf.Max(minTMPRectSize, targetSize.x - tmpPaddingX * 2f);
            float height = Mathf.Max(minTMPRectSize, targetSize.y - tmpPaddingY * 2f);
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(width, height);
        }

        _lastAppliedScaleRatio = _currentScaleRatio;
        _lastSyncTMPRect = syncTMPRect;
        _lastTMPPaddingX = tmpPaddingX;
        _lastTMPPaddingY = tmpPaddingY;
        _lastMinTMPRectSize = minTMPRectSize;
    }

    public void Initialize(char correctLetter, char[] decoyLetters, int spawnCol, float spawnWorldY)
    {
        _centerCol = Mathf.Clamp(spawnCol, MinCol, _maxCol);
        _rotation = 0;
        _isActive = true;
        _isFastFalling = false;

        int correctArm = Random.Range(0, 4);
        int decoyIdx = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i == correctArm)
                _letters[i] = correctLetter;
            else
                _letters[i] = decoyLetters[decoyIdx++];
        }

        UpdateVisuals();
        UpdateArmVisibility();

        if (_centerText != null)
            _centerText.text = "";

        gameObject.SetActive(true);
        SetWorldPosition(_colWorldX[_centerCol], spawnWorldY);
    }

    public bool MoveLeft()
    {
        if (!_isActive || _colWorldX == null) return false;
        if (_centerCol - 1 < MinCol) return false;

        _centerCol--;
        SetWorldX(_colWorldX[_centerCol]);
        UpdateArmVisibility();
        return true;
    }

    public bool MoveRight()
    {
        if (!_isActive || _colWorldX == null) return false;
        if (_centerCol + 1 > _maxCol) return false;

        _centerCol++;
        SetWorldX(_colWorldX[_centerCol]);
        UpdateArmVisibility();
        return true;
    }

    public void Rotate()
    {
        if (!_isActive) return;
        _rotation = (_rotation + 1) % 4;
        UpdateVisuals();
    }

    public void SetFastFall(bool fast)
    {
        _isFastFalling = fast;
    }

    public void SetFallSpeeds(float normalFallSpeed, float fastFallSpeedValue)
    {
        fallSpeed = Mathf.Max(1f, normalFallSpeed);
        fastFallSpeed = Mathf.Max(fallSpeed, fastFallSpeedValue);
    }

    public void ApplyFall()
    {
        if (!_isActive) return;
        float speed = _isFastFalling ? fastFallSpeed : fallSpeed;
        float worldDelta = speed * _worldCellSize / 85f * Time.deltaTime;
        var pos = transform.position;
        pos.y -= worldDelta;
        transform.position = pos;
    }

    public float GetBottomWorldY()
    {
        return transform.position.y - _worldCellSize;
    }

    public int GetBottomCellColumn()
    {
        return _centerCol;
    }

    public float GetCenterWorldX()
    {
        if (armCenter != null)
            return armCenter.position.x;

        return transform.position.x;
    }

    /// <summary>
    /// Returns the letter currently displayed at the Down arm (physical bottom).
    /// The player must rotate to bring the correct letter to this position.
    /// </summary>
    public char GetBottomLetter()
    {
        int idx = (2 + 4 - _rotation) % 4;
        return _letters[idx];
    }

    public void ResetToTop(float spawnWorldY)
    {
        if (!_isActive) return;
        SetWorldPosition(_colWorldX[_centerCol], spawnWorldY);
        _isFastFalling = false;
    }

    public void Deactivate()
    {
        _isActive = false;
        gameObject.SetActive(false);
    }

    private void SetWorldPosition(float worldX, float worldY)
    {
        transform.position = new Vector3(worldX, worldY, transform.position.z);
    }

    private void SetWorldX(float worldX)
    {
        var pos = transform.position;
        pos.x = worldX;
        transform.position = pos;
    }

    private void UpdateVisuals()
    {
        for (int arm = 0; arm < 4; arm++)
        {
            int letterIdx = (arm - _rotation + 4) % 4;
            if (_armTexts[arm] != null)
                _armTexts[arm].text = _letters[letterIdx].ToString();
        }
    }

    private void UpdateArmVisibility()
    {
        if (armLeft != null)
            armLeft.gameObject.SetActive(_centerCol > 0);
        if (armRight != null)
            armRight.gameObject.SetActive(_centerCol < _maxCol);
    }
}
