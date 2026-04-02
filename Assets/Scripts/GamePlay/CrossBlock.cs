using TMPro;
using UnityEngine;

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
    private const int MaxCol = 6;

    private char[] _letters = new char[4]; // Up=0, Right=1, Down=2, Left=3
    private int _rotation;
    private int _centerCol;
    private TextMeshProUGUI[] _armTexts = new TextMeshProUGUI[4];
    private TextMeshProUGUI _centerText;

    private bool _isFastFalling;
    private bool _isActive;

    private float[] _colWorldX;
    private float _worldCellSize;

    public bool IsActive => _isActive;

    private void Awake()
    {
        if (armUp != null) _armTexts[0] = armUp.GetComponentInChildren<TextMeshProUGUI>();
        if (armRight != null) _armTexts[1] = armRight.GetComponentInChildren<TextMeshProUGUI>();
        if (armDown != null) _armTexts[2] = armDown.GetComponentInChildren<TextMeshProUGUI>();
        if (armLeft != null) _armTexts[3] = armLeft.GetComponentInChildren<TextMeshProUGUI>();
        if (armCenter != null) _centerText = armCenter.GetComponentInChildren<TextMeshProUGUI>();
    }

    public void SetColumnPositions(float[] colWorldX, float worldCellSize)
    {
        _colWorldX = colWorldX;
        _worldCellSize = worldCellSize;
    }

    public void Initialize(char correctLetter, char[] decoyLetters, int spawnCol, float spawnWorldY)
    {
        _centerCol = spawnCol;
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

    public void MoveLeft()
    {
        if (!_isActive || _colWorldX == null) return;
        if (_centerCol - 1 < MinCol) return;

        _centerCol--;
        SetWorldX(_colWorldX[_centerCol]);
        UpdateArmVisibility();
    }

    public void MoveRight()
    {
        if (!_isActive || _colWorldX == null) return;
        if (_centerCol + 1 > MaxCol) return;

        _centerCol++;
        SetWorldX(_colWorldX[_centerCol]);
        UpdateArmVisibility();
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
            armRight.gameObject.SetActive(_centerCol < 6);
    }
}
