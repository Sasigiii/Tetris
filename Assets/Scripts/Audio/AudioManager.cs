using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效管理器 负责播放背景音乐和操作事件音效，支持音量控制和开关，并使用对象池优化音效播放性能
/// </summary>
public class AudioManager : MonoBehaviour
{
    // 音效管理器的单例
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError("[AudioManager] Instance is null. Make sure AudioManager exists in the scene.");
            return _instance;
        }
    }

    // 定义操作事件的名称
    public static readonly string[] EventNames =
    {
        "blockMove", "fillCorrect", "fillWrong",
        "starPop", "gameOverWin", "gameOverLose", "uiClick"
    };

    // 定义操作事件的显示标签
    public static readonly string[] EventLabels =
    {
        "方块移动", "填充正确", "填充错误",
        "星星弹出", "通关音效", "失败音效", "按钮点击"
    };

    private const string SfxPoolKey = "sfx"; // 对象池的key
    private const int SfxMinRetain = 2; // 对象池中最少保留的实例数量

    private AudioConfig _config; // 音效配置文件
    private AudioSource _bgmSource; // 用于播放背景音乐的AudioSource组件
    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>(); // AudioClip缓存，避免重复加载
    private readonly Dictionary<string, float> _eventVolumes = new Dictionary<string, float>(); // 操作事件的音量设置
    
    private float _bgmVolume = 1f; // 背景音乐的音量
    private bool _bgmEnabled = true; // 背景音乐是否开启
    
    /// <summary>
    /// 背景音乐开关和音量属性，设置时会更新AudioSource组件并保存到PlayerPrefs
    /// </summary>
    public bool BgmEnabled
    {
        get => _bgmEnabled;
        set
        {
            _bgmEnabled = value;
            _bgmSource.mute = !value;
            PlayerPrefs.SetInt("bgm_enabled", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    
    /// <summary>
    /// 背景音乐音量属性，设置时会更新AudioSource组件并保存到PlayerPrefs，音量值会被限制在0到1之间
    /// </summary>
    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp01(value);
            if (_bgmSource != null)
                _bgmSource.volume = _bgmVolume;
            PlayerPrefs.SetFloat("vol_bgm", _bgmVolume);
            PlayerPrefs.Save();
        }
    }

    private void Awake()
    {
        // 保证单例唯一且不会在场景切换时被销毁
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 加载音效配置文件
        _config = Resources.Load<AudioConfig>("AudioConfig");

        // 创建用于播放背景音乐的AudioSource组件，并设置循环和不自动播放，同时加载背景音乐的开关和音量设置
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _bgmEnabled = PlayerPrefs.GetInt("bgm_enabled", 1) == 1;
        _bgmVolume = PlayerPrefs.GetFloat("vol_bgm", 1f);
        _bgmSource.mute = !_bgmEnabled;
        _bgmSource.volume = _bgmVolume;
        
        // 加载操作事件的音量设置，默认值为1f
        foreach (var name in EventNames)
        {
            float vol = PlayerPrefs.GetFloat($"vol_{name}", 1f);
            _eventVolumes[name] = vol;
        }
        
        // 创建一个用于对象池的模板GameObject，包含AudioSource组件和AudioPoolItem组件，并注册到全局对象池中
        var templateGo = new GameObject("_SfxTemplate");
        templateGo.transform.SetParent(transform);
        templateGo.AddComponent<AudioSource>().playOnAwake = false;
        templateGo.AddComponent<AudioPoolItem>();
        templateGo.SetActive(false);

        GlobalLRUPool.Instance.Register(SfxPoolKey, templateGo, transform, SfxMinRetain);
    }

    /// <summary>
    /// 设置操作事件的音量，音量值会被限制在0到1之间，并保存到PlayerPrefs
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="volume">音量</param>
    public void SetEventVolume(string eventName, float volume)
    {
        volume = Mathf.Clamp01(volume);
        _eventVolumes[eventName] = volume;
        PlayerPrefs.SetFloat($"vol_{eventName}", volume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取操作事件的音量，默认值1f
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <returns></returns>
    public float GetEventVolume(string eventName)
    {
        return _eventVolumes.TryGetValue(eventName, out float vol) ? vol : 1f;
    }

    /// <summary>
    /// 获取操作事件的音量，如果没有设置则返回默认值1f
    /// </summary>
    /// <param name="path">音效路径</param>
    public void PlaySfx(string path)
    {
        PlaySfxWithVolume(path, 1f);
    }

    /// <summary>
    /// 播放操作事件音效，指定音量，音量值会被限制在0到1之间
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="volume">音量</param>
    public void PlaySfxWithVolume(string path, float volume)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        // 从缓存中获取AudioClip，如果没有则从Resources加载并缓存，避免重复加载同一个音效文件
        var clip = GetClip(path);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] AudioClip not found: {path}");
            return;
        }
        
        // 从对象池中获取一个GameObject，获取其中的AudioPoolItem组件，设置音效剪辑和音量，并播放音效
        var go = GlobalLRUPool.Instance.Get(SfxPoolKey);
        var item = go.GetComponent<AudioPoolItem>();
        item.Source.clip = clip;
        item.Source.volume = volume;
        item.Source.Play();
        
        // 音效播放完成后自动回收GameObject，回收时间为音效时长加上一个小的缓冲时间，确保音效能够完整播放
        StartCoroutine(ReleaseAfterPlay(go, clip.length));
    }

    /// <summary>
    /// 根据操作事件名称播放对应的音效
    /// </summary>
    /// <param name="eventName"></param>
    public void PlayEvent(string eventName)
    {
        if (_config == null)
        {
            Debug.LogWarning("[AudioManager] AudioConfig not loaded");
            return;
        }
        
        // 根据事件名称获取对应的音效路径，如果事件名称不在配置文件中则返回null
        string path = eventName switch
        {
            "blockMove" => _config.blockMove,
            "fillCorrect" => _config.fillCorrect,
            "fillWrong" => _config.fillWrong,
            "starPop" => _config.starPop,
            "gameOverWin" => _config.gameOverWin,
            "gameOverLose" => _config.gameOverLose,
            _ => null
        };
        
        // 播放音效
        if (!string.IsNullOrEmpty(path))
        {
            float vol = GetEventVolume(eventName);
            PlaySfxWithVolume(path, vol);
        }
    }
    
    /// <summary>
    /// 播放背景音乐
    /// </summary>
    /// <param name="path">音频路径</param>
    private void PlayBGM(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var clip = GetClip(path);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] BGM clip not found: {path}");
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
            return;

        _bgmSource.Stop();
        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    public void PlayBGM()
    {
        if (_config != null && !string.IsNullOrEmpty(_config.bgm))
            PlayBGM(_config.bgm);
    }
    
    /// <summary>
    /// 暂停背景音乐
    /// </summary>
    public void StopBGM()
    {
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    /// <summary>
    /// 根据音频路径获取AudioClip
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private AudioClip GetClip(string path)
    {
        // 先从缓存中获取AudioClip
        if (_clipCache.TryGetValue(path, out var cached))
            return cached;
        
        // 如果缓存中没有则从Resources加载并缓存
        var clip = Resources.Load<AudioClip>(path);
        if (clip != null)
            _clipCache[path] = clip;

        return clip;
    }
        
    /// <summary>
    /// 音频播放完成自动回收
    /// </summary>
    /// <param name="go">GameObject</param>
    /// <param name="duration">音频时长</param>
    /// <returns></returns>
    private IEnumerator ReleaseAfterPlay(GameObject go, float duration)
    {
        yield return new WaitForSeconds(duration + 0.05f);
        GlobalLRUPool.Instance?.Release(go);
    }
}
