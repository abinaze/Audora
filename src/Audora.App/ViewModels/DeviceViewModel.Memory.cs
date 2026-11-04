namespace Audora.App.ViewModels;

/// <summary>What the user had set on a device, kept while it is away.</summary>
public readonly record struct DeviceMemory(double Volume, bool Muted, double Balance, double Delay);

public sealed partial class DeviceViewModel
{
    public DeviceMemory Snapshot() => new(_volume, _isMuted, _balance, _delay);

    /// <summary>
    /// Shows the values the user had before the device went away. The audio engine still holds
    /// them, so nothing is re-applied. The Windows default device always shows its real volume instead.
    /// </summary>
    public void Restore(DeviceMemory memory)
    {
        if (_isDefault)
        {
            return;
        }

        _applying = true;
        try
        {
            Set(ref _volume, memory.Volume, nameof(Volume));
            Set(ref _isMuted, memory.Muted, nameof(IsMuted));
            Set(ref _balance, memory.Balance, nameof(Balance));
            Set(ref _delay, memory.Delay, nameof(Delay));
        }
        finally
        {
            _applying = false;
        }
    }
}
