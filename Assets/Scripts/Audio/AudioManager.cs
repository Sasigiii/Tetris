using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
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

    public static readonly string[] EventNames =
    {
        "blockMove", "fillCorrect", "fillWrong",
        "starPop", "gameOverWin", "gameOverLose", "uiClick"
    };

    public static readonly string[] EventLabels =
    {
        "方块移动", "填充正确", "填充错误",
        "星星弹出", "通关音效", "失败音效", "按钮点击"
    };

    private const int SfxPoolCapacity = 8;

    private AudioConfig _config;
    private LRUObjectPool _sfxPool;
    private AudioSource _bgmSource;
    private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> _eventVolumes = new Dictionary<string, float>();
    private float _bgmVolume = 1f;

    private bool _bgmEnabled = true;
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
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _config = Resources.Load<AudioConfig>("AudioConfig");

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _bgmEnabled = PlayerPrefs.GetInt("bgm_enabled", 1) == 1;
        _bgmVolume = PlayerPrefs.GetFloat("vol_bgm", 1f);
        _bgmSource.mute = !_bgmEnabled;
        _bgmSource.volume = _bgmVolume;

        foreach (var name in EventNames)
        {
            float vol = PlayerPrefs.GetFloat($"vol_{name}", 1f);
            _eventVolumes[name] = vol;
        }

        var templateGo = new GameObject("_SfxTemplate");
        templateGo.transform.SetParent(transform);
        templateGo.AddComponent<AudioSource>().playOnAwake = false;
        templateGo.AddComponent<AudioPoolItem>();
        templateGo.SetActive(false);

        _sfxPool = new LRUObjectPool(templateGo, transform, SfxPoolCapacity);
    }

    public void SetEventVolume(string eventName, float volume)
    {
        volume = Mathf.Clamp01(volume);
        _eventVolumes[eventName] = volume;
        PlayerPrefs.SetFloat($"vol_{eventName}", volume);
        PlayerPrefs.Save();
    }

    public float GetEventVolume(string eventName)
    {
        return _eventVolumes.TryGetValue(eventName, out float vol) ? vol : 1f;
    }

    public void PlaySfx(string path)
    {
        PlaySfxWithVolume(path, 1f);
    }

    public void PlaySfxWithVolume(string path, float volume)
    {
        if (string.IsNullOrEmpty(path)) return;

        var clip = GetClip(path);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] AudioClip not found: {path}");
            return;
        }

        var go = _sfxPool.Get();
        var item = go.GetComponent<AudioPoolItem>();
        item.Source.clip = clip;
        item.Source.volume = volume;
        item.Source.Play();

        StartCoroutine(ReleaseAfterPlay(go, clip.length));
    }

    public void PlayEvent(string eventName)
    {
        if (_config == null)
        {
            Debug.LogWarning("[AudioManager] AudioConfig not loaded");
            return;
        }

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

        if (!string.IsNullOrEmpty(path))
        {
            float vol = GetEventVolume(eventName);
            PlaySfxWithVolume(path, vol);
        }
    }

    public void PlayBGM(string path)
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

    public void StopBGM()
    {
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    private AudioClip GetClip(string path)
    {
        if (_clipCache.TryGetValue(path, out var cached))
            return cached;

        var clip = Resources.Load<AudioClip>(path);
        if (clip != null)
            _clipCache[path] = clip;

        return clip;
    }

    private IEnumerator ReleaseAfterPlay(GameObject go, float duration)
    {
        yield return new WaitForSeconds(duration + 0.05f);
        _sfxPool.Release(go);
    }
}
