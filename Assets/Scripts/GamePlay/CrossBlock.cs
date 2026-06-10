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
    [SerializeField] private float fallSpeed = 120f; // 下降速度
    [SerializeField] private float fastFallSpeed = 600f; // 快速下降时的速度

    private const int MinCol = 0; // 对左侧边界的列索引限制

    private char[] _letters = new char[4]; // Up=0, Right=1, Down=2, Left=3
    private int _rotation; // 当前旋转状态，每顺时针旋转一次加1，模4循环
    private int _centerCol; // 当前中心所在的列索引
    private TextMeshProUGUI[] _armTexts = new TextMeshProUGUI[4]; // 四个臂上的文本组件
    private TextMeshProUGUI _centerText; // 中心文本组件
    private RectTransform[] _arms = new RectTransform[5]; // 五个臂的RectTransform，包含中心，用于适配时调整尺寸和位置
    private RectTransform[] _textRects = new RectTransform[5]; // 五个文本组件的RectTransform，用于适配时调整尺寸
    private Vector2[] _baseArmSizes = new Vector2[5]; // 基础尺寸，用于乘以缩放后得到四个臂的最终尺寸
    private Vector2[] _baseArmAnchoredPositions = new Vector2[5]; // 相对于中心点的基础相对位置，用于乘以缩放后再加上中心点位置得到的最终位置
    private bool[] _warnedMissingTMP = new bool[5];

    private bool _isFastFalling;
    private bool _isActive;

    private float[] _colWorldX; // 每列的世界坐标X值，用于根据列索引设置方块位置
    private float _worldCellSize; // 世界坐标中每个格子的大小，用于适配时计算缩放比例和移动距离
    private int _maxCol; // 根据_colWorldX长度计算得到的最大列索引
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
        // 获取armText数组所需的组件引用，并缓存基础尺寸和位置
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
        
        // 获取文本组件的RectTransform引用，方便后续适配时调整尺寸
        _textRects[0] = _armTexts[0] != null ? _armTexts[0].rectTransform : null;
        _textRects[1] = _armTexts[1] != null ? _armTexts[1].rectTransform : null;
        _textRects[2] = _armTexts[2] != null ? _armTexts[2].rectTransform : null;
        _textRects[3] = _armTexts[3] != null ? _armTexts[3].rectTransform : null;
        _textRects[4] = _centerText != null ? _centerText.rectTransform : null;
    }
    
    /// <summary>
    /// 设置每列的世界坐标X值和格子大小
    /// </summary>
    /// <param name="colWorldX"></param>
    /// <param name="worldCellSize"></param>
    public void SetColumnPositions(float[] colWorldX, float worldCellSize)
    {
        _colWorldX = colWorldX;
        _worldCellSize = worldCellSize;
        _maxCol = _colWorldX != null && _colWorldX.Length > 0 ? _colWorldX.Length - 1 : 0;
        _centerCol = Mathf.Clamp(_centerCol, MinCol, _maxCol);
    }

    /// <summary>
    /// 应用自适应缩放
    /// </summary>
    /// <param name="worldCellSize"></param>
    /// <param name="baselineWorldCellSize"></param>
    /// <param name="syncTMPRect"></param>
    /// <param name="tmpPaddingX"></param>
    /// <param name="tmpPaddingY"></param>
    /// <param name="minTMPRectSize"></param>
    public void ApplyAdaptiveSizing(
        float worldCellSize,
        float baselineWorldCellSize,
        bool syncTMPRect,
        float tmpPaddingX,
        float tmpPaddingY,
        float minTMPRectSize)
    {
        // 如果格子大小无效则不应用缩放，避免出现异常尺寸
        if (worldCellSize <= 0f || baselineWorldCellSize <= 0f)
            return;
        
        // 计算当前缩放比例，并限制在合理范围内，避免过大或过小导致显示问题
        _currentScaleRatio = Mathf.Clamp(worldCellSize / baselineWorldCellSize, 0.25f, 2f);
        // 如果缩放比例和TMP参数都没有变化，则不需要重新应用，避免不必要的性能开销
        bool noChange = Mathf.Approximately(_lastAppliedScaleRatio, _currentScaleRatio)
                        && _lastSyncTMPRect == syncTMPRect
                        && Mathf.Approximately(_lastTMPPaddingX, tmpPaddingX)
                        && Mathf.Approximately(_lastTMPPaddingY, tmpPaddingY)
                        && Mathf.Approximately(_lastMinTMPRectSize, minTMPRectSize);
        if (noChange)
            return;
        
        // 根据当前缩放比例调整每个臂的尺寸和位置
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
        
        // 缓存当前应用的缩放比例和TMP参数
        _lastAppliedScaleRatio = _currentScaleRatio;
        _lastSyncTMPRect = syncTMPRect;
        _lastTMPPaddingX = tmpPaddingX;
        _lastTMPPaddingY = tmpPaddingY;
        _lastMinTMPRectSize = minTMPRectSize;
    }
    
    /// <summary>
    /// 初始化方块状态，包括中心列位置、旋转状态、是否激活、字母内容等，并设置初始世界位置
    /// </summary>
    /// <param name="correctLetter"></param>
    /// <param name="decoyLetters"></param>
    /// <param name="spawnCol"></param>
    /// <param name="spawnWorldY"></param>
    public void Initialize(char correctLetter, char[] decoyLetters, int spawnCol, float spawnWorldY)
    {
        // 确保生成位置合法
        _centerCol = Mathf.Clamp(spawnCol, MinCol, _maxCol);
        _rotation = 0;
        _isActive = true;
        _isFastFalling = false;
        // 随机分配正确字母和诱饵字母到四个臂上
        int correctArm = Random.Range(0, 4);
        int decoyIdx = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i == correctArm)
                _letters[i] = correctLetter;
            else
                _letters[i] = decoyLetters[decoyIdx++];
        }
        
        // 根据当前状态更新视觉显示和臂的可见性
        UpdateVisuals();
        UpdateArmVisibility();

        if (_centerText != null)
            _centerText.text = "";

        gameObject.SetActive(true);
        // 设置初始世界位置
        SetWorldPosition(_colWorldX[_centerCol], spawnWorldY);
    }
    
    /// <summary>
    /// 向左移动一格
    /// </summary>
    /// <returns></returns>
    public bool MoveLeft()
    {
        // 如果方块未激活或列坐标未设置，则无法移动
        if (!_isActive || _colWorldX == null) return false;
        //  如果已经在最左边的列，则无法再向左移动
        if (_centerCol - 1 < MinCol) return false;

        _centerCol--;
        // 根据新的中心列索引设置方块的世界X坐标
        SetWorldX(_colWorldX[_centerCol]);
        // 根据新的位置更新臂的可见性
        UpdateArmVisibility();
        return true;
    }
    
    /// <summary>
    /// 向右移动一格
    /// </summary>
    /// <returns></returns>
    public bool MoveRight()
    {   
        // 如果方块未激活或列坐标未设置，则无法移动
        if (!_isActive || _colWorldX == null) return false;
        // 如果已经在最右边的列，则无法再向右移动
        if (_centerCol + 1 > _maxCol) return false;

        _centerCol++;
        // 根据新的中心列索引设置方块的世界X坐标
        SetWorldX(_colWorldX[_centerCol]);
        // 根据新的位置更新臂的可见性
        UpdateArmVisibility();
        return true;
    }

    /// <summary>
    /// 顺时针旋转方块
    /// </summary>
    public void Rotate()
    {
        if (!_isActive) return;
        _rotation = (_rotation + 1) % 4;
        UpdateVisuals();
    }

    /// <summary>
    /// 设置快速下落状态
    /// </summary>
    /// <param name="fast"></param>
    public void SetFastFall(bool fast)
    {
        _isFastFalling = fast;
    }
    
    /// <summary>
    /// 设置快速下落速度和正常下落速度
    /// </summary>
    /// <param name="normalFallSpeed"></param>
    /// <param name="fastFallSpeedValue"></param>
    public void SetFallSpeeds(float normalFallSpeed, float fastFallSpeedValue)
    {
        fallSpeed = Mathf.Max(1f, normalFallSpeed);
        fastFallSpeed = Mathf.Max(fallSpeed, fastFallSpeedValue);
    }
    
    /// <summary>
    /// 应用下落
    /// </summary>
    public void ApplyFall()
    {
        if (!_isActive) return;
        // 根据是否处于快速下落，选择相应的速度
        float speed = _isFastFalling ? fastFallSpeed : fallSpeed;
        // 计算该帧下落得世界坐标距离
        float worldDelta = speed * _worldCellSize / 85f * Time.deltaTime;
        // 更新下落方块的世界坐标Y
        var pos = transform.position;
        pos.y -= worldDelta;
        transform.position = pos;
    }
    
    /// <summary>
    /// 获取方块底部的世界坐标Y值
    /// </summary>
    /// <returns></returns>
    public float GetBottomWorldY()
    {
        return transform.position.y - _worldCellSize;
    }

    /// <summary>
    /// 获取方块中心所在的列索引
    /// </summary>
    /// <returns></returns>
    public int GetBottomCellColumn()
    {
        return _centerCol;
    }
    
    /// <summary>
    /// 获取方块中心的世界坐标X值
    /// </summary>
    /// <returns></returns>
    public float GetCenterWorldX()
    {
        if (armCenter != null)
            return armCenter.position.x;

        return transform.position.x;
    }

    /// <summary>
    /// 获取底部的字母
    /// </summary>
    public char GetBottomLetter()
    {
        // 上，右，下，左
        int idx = (2 + 4 - _rotation) % 4;
        return _letters[idx];
    }
    
    /// <summary>
    /// 重置下落方块到顶部位置
    /// </summary>
    /// <param name="spawnWorldY"></param>
    public void ResetToTop(float spawnWorldY)
    {
        if (!_isActive) return;
        SetWorldPosition(_colWorldX[_centerCol], spawnWorldY);
        _isFastFalling = false;
    }

    /// <summary>
    /// 隐藏下落方块
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 设置方块的世界坐标位置
    /// </summary>
    /// <param name="worldX"></param>
    /// <param name="worldY"></param>
    private void SetWorldPosition(float worldX, float worldY)
    {
        transform.position = new Vector3(worldX, worldY, transform.position.z);
    }
    
    /// <summary>
    /// 设置方块的世界坐标X值
    /// </summary>
    /// <param name="worldX"></param>
    private void SetWorldX(float worldX)
    {
        var pos = transform.position;
        pos.x = worldX;
        transform.position = pos;
    }
    
    /// <summary>
    /// 更新四个臂上的字母显示
    /// </summary>
    private void UpdateVisuals()
    {
        for (int arm = 0; arm < 4; arm++)
        {
            // 重定向索引
            int letterIdx = (arm - _rotation + 4) % 4;
            if (_armTexts[arm] != null)
                _armTexts[arm].text = _letters[letterIdx].ToString();
        }
    }
    
    /// <summary>
    /// 更新左右臂的可见性
    /// </summary>
    private void UpdateArmVisibility()
    {
        if (armLeft != null)
            armLeft.gameObject.SetActive(_centerCol > 0);
        if (armRight != null)
            armRight.gameObject.SetActive(_centerCol < _maxCol);
    }
}
