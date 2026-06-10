using UnityEngine;

/// <summary>
/// 操作事件对应音效的配置文件
/// </summary>
[CreateAssetMenu(fileName = "AudioConfig", menuName = "Game/AudioConfig")]
public class AudioConfig : ScriptableObject
{
    // 音效
    [Header("SFX")]
    public string blockMove = "Audio/sfx_move"; // 方块移动音效
    public string fillCorrect = "Audio/sfx_correct"; // 填充正确音效
    public string fillWrong = "Audio/sfx_wrong"; // 填充错误音效
    public string gameOverWin = "Audio/sfx_win"; // 游戏胜利音效
    public string gameOverLose = "Audio/sfx_lose"; // 游戏失败音效
    public string starPop = "Audio/sfx_star"; // 星星弹出音效

    // BGM
    [Header("BGM")]
    public string bgm = "Audio/bgm_main";
}
