using UnityEngine;

/// <summary>
/// 音频对象池的单个音频项，包含一个AudioSource组件，实现IPoolable接口以便在对象池中使用
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPoolItem : MonoBehaviour, IPoolable
{
    private AudioSource _source;
    
    // 对象池的key
    public string PoolKey => "sfx";

    public AudioSource Source
    {
        get
        {
            if (_source == null)
                _source = GetComponent<AudioSource>();
            return _source;
        }
    }

    /// <summary>
    /// 实现获取接口
    /// </summary>
    public void OnPoolGet()
    {
        Source.Stop();
        Source.clip = null;
        Source.loop = false;
        Source.volume = 1f;
    }

    /// <summary>
    /// 实现释放接口
    /// </summary>
    public void OnPoolRelease()
    {
        Source.Stop();
        Source.clip = null;
    }
}
