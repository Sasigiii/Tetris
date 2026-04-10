using UnityEngine;
using UnityEngine.UI;

public class SettingsUIView : BaseView
{
    public Toggle bgmToggle;
    public Slider bgmVolumeSlider;
    public Slider[] eventSliders = new Slider[7];
    public Button returnBtn;
}
