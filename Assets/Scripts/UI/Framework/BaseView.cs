using UnityEngine;

public class BaseView : MonoBehaviour
{
    public virtual void OnEnter()
    {
        gameObject.SetActive(true);
    }

    public virtual void OnPause()
    {
        gameObject.SetActive(false);
    }

    public virtual void OnResume()
    {
        gameObject.SetActive(true);
    }

    public virtual void OnExit()
    {
        gameObject.SetActive(false);
    }
}
