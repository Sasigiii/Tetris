public class SettingsUIController : BaseController<SettingsUIView, SettingsUIModel>
{
    public override bool IsPopup => true;

    protected override void OnInitialize()
    {
        View.returnBtn.onClick.RemoveAllListeners();
        View.returnBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());
    }

    public override void OnEnter()
    {
        base.OnEnter();

        var am = AudioManager.Instance;
        if (am == null) return;

        View.bgmToggle.onValueChanged.RemoveAllListeners();
        View.bgmToggle.isOn = am.BgmEnabled;
        View.bgmToggle.onValueChanged.AddListener(val => am.BgmEnabled = val);

        if (View.bgmVolumeSlider != null)
        {
            View.bgmVolumeSlider.onValueChanged.RemoveAllListeners();
            View.bgmVolumeSlider.value = am.BgmVolume;
            View.bgmVolumeSlider.onValueChanged.AddListener(val => am.BgmVolume = val);
        }

        for (int i = 0; i < AudioManager.EventNames.Length && i < View.eventSliders.Length; i++)
        {
            var slider = View.eventSliders[i];
            if (slider == null) continue;

            string eventName = AudioManager.EventNames[i];

            slider.onValueChanged.RemoveAllListeners();
            slider.value = am.GetEventVolume(eventName);
            slider.onValueChanged.AddListener(val => am.SetEventVolume(eventName, val));
        }
    }

    public override void OnExit()
    {
        base.OnExit();

        View.bgmToggle.onValueChanged.RemoveAllListeners();
        if (View.bgmVolumeSlider != null)
            View.bgmVolumeSlider.onValueChanged.RemoveAllListeners();
        foreach (var slider in View.eventSliders)
        {
            if (slider != null)
                slider.onValueChanged.RemoveAllListeners();
        }
    }
}
