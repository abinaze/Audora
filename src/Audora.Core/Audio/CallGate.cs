namespace Audora.Core.Audio;

/// <summary>
/// Decides when a call is going on, from checks of the app in front. It needs two checks in a row to agree before it
/// changes, so a quick glance at another window never flips it. A check that cannot tell (null) starts counting again.
/// Everything is worked out on this computer; nothing is stored or sent.
/// </summary>
public sealed class CallGate
{
    private bool? _pending;

    /// <summary>True while a call app has been in front.</summary>
    public bool Active { get; private set; }

    /// <summary>True when this app and window title belong to a call app such as Teams, Zoom or Meet.</summary>
    public static bool IsCall(string? process, string? title) =>
        SmartRules.Choose(process, title, false) == SoundPresets.CommunicationName;

    /// <summary>Adds one check: true = a call app is in front, false = another app, null = cannot tell.</summary>
    public bool Observe(bool? inCall)
    {
        if (inCall is null || inCall.Value == Active)
        {
            _pending = null;
            return Active;
        }

        if (_pending == inCall)
        {
            Active = inCall.Value;
            _pending = null;
        }
        else
        {
            _pending = inCall;
        }

        return Active;
    }

    /// <summary>Back to no call.</summary>
    public void Reset()
    {
        Active = false;
        _pending = null;
    }
}
