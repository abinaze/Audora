namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    private MicrophoneViewModel? _microphone;

    /// <summary>The Microphone page. Created the first time it is shown. Nothing listens until you press Test.</summary>
    public MicrophoneViewModel Microphone =>
        _microphone ??= new MicrophoneViewModel(_devices, _ui, _settings.Microphones);

    public bool IsMicrophoneTab
    {
        get => _tab == 5;
        set
        {
            if (value)
            {
                Microphone.Refresh();
            }
            else
            {
                _microphone?.StopTest();
            }

            SelectTab(5, value);
        }
    }
}
