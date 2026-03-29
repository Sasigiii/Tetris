public abstract class BaseController
{
    public abstract void OnEnter();
    public abstract void OnPause();
    public abstract void OnResume();
    public abstract void OnExit();
}

public abstract class BaseController<TView, TModel> : BaseController
    where TView : BaseView
    where TModel : BaseModel, new()
{
    protected TView View { get; private set; }
    protected TModel Model { get; private set; }

    public void Initialize(TView view)
    {
        View = view;
        Model = new TModel();
        OnInitialize();
    }

    protected virtual void OnInitialize() { }

    public override void OnEnter()
    {
        View.OnEnter();
    }

    public override void OnPause()
    {
        View.OnPause();
    }

    public override void OnResume()
    {
        View.OnResume();
    }

    public override void OnExit()
    {
        View.OnExit();
    }
}
