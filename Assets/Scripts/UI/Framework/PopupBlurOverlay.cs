using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class PopupBlurOverlay : MonoBehaviour
{
    [SerializeField] private int _downSample = 4;
    [SerializeField] private int _blurPasses = 4;
    [SerializeField] private Color _tintColor = new Color(0f, 0f, 0f, 0.4f);

    private RawImage _rawImage;
    private Image _tintImage;
    private Material _blurMat;
    private Texture2D _captured;
    private RenderTexture _blurred;

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();

        var tintGo = new GameObject("Tint");
        tintGo.transform.SetParent(transform, false);
        var tintRT = tintGo.AddComponent<RectTransform>();
        tintRT.anchorMin = Vector2.zero;
        tintRT.anchorMax = Vector2.one;
        tintRT.sizeDelta = Vector2.zero;
        _tintImage = tintGo.AddComponent<Image>();
        _tintImage.color = _tintColor;
        _tintImage.raycastTarget = false;

        var shader = Shader.Find("UI/KawaseBlur");
        if (shader != null)
            _blurMat = new Material(shader);

        gameObject.SetActive(false);
    }

    public void Show(MonoBehaviour host)
    {
        if (_blurMat == null)
        {
            gameObject.SetActive(true);
            return;
        }

        host.StartCoroutine(CaptureAndShow());
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        ReleaseResources();
    }

    private IEnumerator CaptureAndShow()
    {
        yield return new WaitForEndOfFrame();

        int sw = Screen.width;
        int sh = Screen.height;

        if (_captured == null || _captured.width != sw || _captured.height != sh)
        {
            if (_captured != null) Destroy(_captured);
            _captured = new Texture2D(sw, sh, TextureFormat.RGB24, false);
        }

        _captured.ReadPixels(new Rect(0, 0, sw, sh), 0, 0, false);
        _captured.Apply(false);

        ApplyBlur();

        gameObject.SetActive(true);
    }

    private void ApplyBlur()
    {
        int w = _captured.width / _downSample;
        int h = _captured.height / _downSample;

        var src = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(_captured, src);

        for (int i = 0; i < _blurPasses; i++)
        {
            var dst = RenderTexture.GetTemporary(w, h, 0);
            _blurMat.SetFloat("_Offset", i + 1);
            Graphics.Blit(src, dst, _blurMat);
            RenderTexture.ReleaseTemporary(src);
            src = dst;
        }

        ReleaseRT();
        _blurred = src;
        _rawImage.texture = _blurred;
    }

    private void ReleaseRT()
    {
        if (_blurred != null)
        {
            _rawImage.texture = null;
            RenderTexture.ReleaseTemporary(_blurred);
            _blurred = null;
        }
    }

    private void ReleaseResources()
    {
        ReleaseRT();
        if (_captured != null)
        {
            Destroy(_captured);
            _captured = null;
        }
    }

    private void OnDestroy()
    {
        ReleaseResources();
        if (_blurMat != null)
            Destroy(_blurMat);
    }
}
