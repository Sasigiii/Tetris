using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError("[UIManager] Instance is null. Make sure UIManager exists in the scene.");
            return _instance;
        }
    }

    private readonly Stack<BaseController> _panelStack = new Stack<BaseController>();
    private readonly Dictionary<string, string> _panelPaths = new Dictionary<string, string>();
    private readonly Dictionary<string, BaseView> _panelCache = new Dictionary<string, BaseView>();

    private Transform _canvasTransform;
    private bool _isTransitioning;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _canvasTransform = transform;

        LoadConfig();
    }

    private void LoadConfig()
    {
        var configAsset = Resources.Load<TextAsset>("UIConfig");
        if (configAsset == null)
        {
            Debug.LogError("[UIManager] UIConfig.json not found in Resources!");
            return;
        }

        var config = JsonUtility.FromJson<UIConfigData>(configAsset.text);

        foreach (var panel in config.panels)
            _panelPaths[panel.name] = panel.path;

        Debug.Log($"[UIManager] Loaded {_panelPaths.Count} panel configs");
    }

    public void PushPanel<TController, TView, TModel>(string panelName)
        where TController : BaseController<TView, TModel>, new()
        where TView : BaseView
        where TModel : BaseModel, new()
    {
        if (_isTransitioning)
            return;

        if (!_panelPaths.TryGetValue(panelName, out string path))
        {
            Debug.LogWarning($"[UIManager] Panel '{panelName}' not found in config");
            return;
        }

        _isTransitioning = true;

        TView view = GetOrCreateView<TView>(panelName, path);
        var controller = new TController();
        controller.Initialize(view);

        if (_panelStack.Count > 0 && !controller.IsPopup)
            _panelStack.Peek().OnPause();

        _panelStack.Push(controller);
        _isTransitioning = false;
        controller.OnEnter();
    }

    public void PopPanel()
    {
        if (_isTransitioning || _panelStack.Count == 0)
            return;

        _isTransitioning = true;

        var top = _panelStack.Pop();
        top.OnExit();

        _isTransitioning = false;

        if (_panelStack.Count > 0 && !top.IsPopup)
            _panelStack.Peek().OnResume();
    }

    public void PopToRoot()
    {
        if (_isTransitioning || _panelStack.Count <= 1)
            return;

        _isTransitioning = true;

        while (_panelStack.Count > 1)
        {
            var top = _panelStack.Pop();
            top.OnExit();
        }

        _panelStack.Peek().OnResume();

        _isTransitioning = false;
    }

    private TView GetOrCreateView<TView>(string panelName, string path) where TView : BaseView
    {
        if (_panelCache.TryGetValue(panelName, out BaseView cached))
            return (TView)cached;

        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[UIManager] Prefab not found at path: {path}");
            return null;
        }

        var go = Instantiate(prefab, _canvasTransform);
        go.name = panelName;
        var view = go.GetComponent<TView>();

        if (view == null)
        {
            Debug.LogError($"[UIManager] {panelName} prefab is missing {typeof(TView).Name} component");
            Destroy(go);
            return null;
        }

        _panelCache[panelName] = view;
        go.SetActive(false);
        return view;
    }

    [System.Serializable]
    private class UIConfigData
    {
        public PanelEntry[] panels;
    }

    [System.Serializable]
    private class PanelEntry
    {
        public string name;
        public string path;
    }
}
