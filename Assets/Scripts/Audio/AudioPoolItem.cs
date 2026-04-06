using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioPoolItem : MonoBehaviour, IPoolable
{
    private AudioSource _source;

    public AudioSource Source
    {
        get
        {
            if (_source == null)
                _source = GetComponent<AudioSource>();
            return _source;
        }
    }

    public void OnPoolGet()
    {
        Source.Stop();
        Source.clip = null;
        Source.loop = false;
        Source.volume = 1f;
    }

    public void OnPoolRelease()
    {
        Source.Stop();
        Source.clip = null;
    }
}
