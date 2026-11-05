namespace Audora.Core.Settings;

/// <summary>Everything Audora remembers between runs. Stored as a small JSON file on this computer.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public bool DarkTheme { get; set; } = true;

    public List<SavedDevice> Devices { get; set; } = new();

    /// <summary>The sound used by "All devices".</summary>
    public SavedSound SharedSound { get; set; } = new();

    /// <summary>Presets the user saved.</summary>
    public List<SavedPreset> Presets { get; set; } = new();

    public List<SavedGroup> Groups { get; set; } = new();

    public string? ActiveGroupId { get; set; }
}

public sealed class SavedSound
{
    public string Preset { get; set; } = "Original";

    public double[] Bands { get; set; } = new double[10];

    public double Preamp { get; set; }

    public double Bass { get; set; }

    public double Treble { get; set; }

    public double Width { get; set; } = 100;

    public double Virtualizer { get; set; }

    public double ReverbAmount { get; set; }

    public string ReverbRoom { get; set; } = "Room";
}

public sealed class SavedDevice
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The connection type name, for example Bluetooth or Usb.</summary>
    public string Type { get; set; } = "Unknown";

    public DateTime LastSeenUtc { get; set; }

    /// <summary>Was playing when Audora last ran.</summary>
    public bool Selected { get; set; }

    public double Volume { get; set; } = 100;

    public bool Muted { get; set; }

    public double Balance { get; set; }

    public double Delay { get; set; }

    /// <summary>Set when this device has its own sound instead of the shared one.</summary>
    public SavedSound? OwnSound { get; set; }
}

public sealed class SavedPreset
{
    public string Name { get; set; } = string.Empty;

    public SavedSound Sound { get; set; } = new();
}

public sealed class SavedGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Group";

    public double Volume { get; set; } = 100;

    public bool Muted { get; set; }

    public List<SavedMember> Members { get; set; } = new();
}

public sealed class SavedMember
{
    public string DeviceId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
