using UnityEngine;

[CreateAssetMenu(fileName = "AudioConfig", menuName = "Game/AudioConfig")]
public class AudioConfig : ScriptableObject
{
    [Header("SFX")]
    public string blockMove = "Audio/sfx_move";
    public string fillCorrect = "Audio/sfx_correct";
    public string fillWrong = "Audio/sfx_wrong";
    public string gameOverWin = "Audio/sfx_win";
    public string gameOverLose = "Audio/sfx_lose";
    public string starPop = "Audio/sfx_star";

    [Header("BGM")]
    public string bgm = "Audio/bgm_main";
}
