using System.Text.Json;

namespace Audora.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/>. A damaged file never stops Audora from starting.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Audora", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options);
            return Normalize(loaded ?? new AppSettings());
        }
        catch (Exception)
        {
            // Keep the unreadable file for inspection and start fresh.
            try { File.Move(_path, _path + ".bad", true); } catch { }
            return new AppSettings();
        }
    }

    public string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    /// <summary>Writes to a temporary file first, so a crash can never leave a half-written settings file.</summary>
    public void Save(string json)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, true);
        }
    }

    private static AppSettings Normalize(AppSettings s)
    {
        s.Devices ??= new();
        s.Presets ??= new();
        s.Groups ??= new();
        s.SharedSound = Fix(s.SharedSound);

        foreach (var device in s.Devices)
        {
            device.Id ??= string.Empty;
            device.Name ??= string.Empty;
            device.Type ??= "Unknown";
            if (device.OwnSound is not null)
            {
                device.OwnSound = Fix(device.OwnSound);
            }
        }

        foreach (var preset in s.Presets)
        {
            preset.Name ??= string.Empty;
            preset.Sound = Fix(preset.Sound);
        }

        foreach (var group in s.Groups)
        {
            group.Id = string.IsNullOrWhiteSpace(group.Id) ? Guid.NewGuid().ToString("N") : group.Id;
            group.Name = string.IsNullOrWhiteSpace(group.Name) ? "Group" : group.Name;
            group.Members ??= new();
        }

        return s;
    }

    private static SavedSound Fix(SavedSound? sound)
    {
        sound ??= new SavedSound();
        sound.Preset = string.IsNullOrWhiteSpace(sound.Preset) ? "Original" : sound.Preset;

        var bands = new double[10];
        if (sound.Bands is not null)
        {
            Array.Copy(sound.Bands, bands, Math.Min(sound.Bands.Length, 10));
        }

        sound.Bands = bands;
        return sound;
    }
}
