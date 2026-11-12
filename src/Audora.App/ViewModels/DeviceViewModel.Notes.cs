namespace Audora.App.ViewModels;

public sealed partial class DeviceViewModel
{
    /// <summary>The silent virtual cable that Audora listens to in Full control mode.</summary>
    public bool IsVirtualInput => _name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase);

    public string DefaultNote => IsVirtualInput
        ? "This is the virtual cable Audora listens to. It makes no sound itself; Audora plays everything on your other devices, so each has full control."
        : "This is the Windows default device, so it plays directly and its volume is the master volume. To apply the equalizer, boost and delay to it, turn on Full control in Settings.";
}
