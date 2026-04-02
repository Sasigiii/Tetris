public class LexiconUIController : BaseController<LexiconUIView, LexiconUIModel>
{
    protected override void OnInitialize()
    {
        View.btn_1.onClick.RemoveAllListeners();
        View.btn_2.onClick.RemoveAllListeners();
        View.btn_3.onClick.RemoveAllListeners();
        View.btn_4.onClick.RemoveAllListeners();
        View.returnBtn.onClick.RemoveAllListeners();

        View.btn_1.onClick.AddListener(() => SelectLexicon(LexiconDatabase.Lexicon.ChuZhong));
        View.btn_2.onClick.AddListener(() => SelectLexicon(LexiconDatabase.Lexicon.GaoZhong));
        View.btn_3.onClick.AddListener(() => SelectLexicon(LexiconDatabase.Lexicon.CET4));
        View.btn_4.onClick.AddListener(() => SelectLexicon(LexiconDatabase.Lexicon.CET6));
        View.returnBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());
    }

    private void SelectLexicon(LexiconDatabase.Lexicon lexicon)
    {
        GameContext.CurrentLexicon = lexicon;
        UIManager.Instance.PushPanel<ChooseUIController, ChooseUIView, ChooseUIModel>("ChooseUI");
    }
}
