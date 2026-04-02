public class MainUIController : BaseController<MainUIView, MainUIModel>
{
    protected override void OnInitialize()
    {
        View.startBtn.onClick.RemoveAllListeners();
        View.startBtn.onClick.AddListener(() =>
        {
            UIManager.Instance.PushPanel<LexiconUIController, LexiconUIView, LexiconUIModel>("LexiconUI");
        });
    }
}
