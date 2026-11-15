using Audora.App.Infrastructure;

namespace Audora.App.ViewModels;

public sealed partial class MainViewModel
{
    // The Mute button changes the Windows mute of the default output, so the log says when it happens.
    private void LogMute(bool muted)
    {
        var name = _watchedDefault is not null && _byId.TryGetValue(_watchedDefault, out var device)
            ? device.Name
            : "the default output";
        Log.Info(muted ? $"Mute: Audora muted {name} in Windows." : $"Mute: Audora unmuted {name} in Windows.");
    }
}
