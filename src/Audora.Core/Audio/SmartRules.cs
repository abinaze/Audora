namespace Audora.Core.Audio;

/// <summary>
/// Decides which profile fits what the user is doing, from the app in front and its window title.
/// Everything is worked out on this computer; nothing is stored or sent.
/// </summary>
public static class SmartRules
{
    private static readonly HashSet<string> CallApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "teams", "ms-teams", "msteams", "zoom", "skype", "lync", "webex", "ciscocollabhost",
        "slack", "discord", "whatsapp", "telegram"
    };

    private static readonly HashSet<string> MusicApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "spotify", "musicbee", "foobar2000", "itunes", "applemusic", "aimp", "winamp", "music.ui"
    };

    private static readonly HashSet<string> VideoApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "vlc", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "potplayer", "potplayermini", "potplayermini64",
        "kodi", "plex", "video.ui"
    };

    private static readonly string[] CallTitles =
        { "zoom meeting", "microsoft teams", "google meet", "meet -", "skype" };

    private static readonly string[] MusicTitles =
        { "spotify", "youtube music", "soundcloud", "apple music", "amazon music", "jiosaavn", "gaana", "deezer" };

    private static readonly string[] VideoTitles =
        { "netflix", "prime video", "disney+", "hotstar", "jiocinema", "hulu", "crunchyroll", "youtube" };

    /// <summary>Returns the name of one of the four profiles.</summary>
    public static string Choose(string? process, string? title, bool fullscreenGame)
    {
        var name = process ?? string.Empty;
        var text = (title ?? string.Empty).ToLowerInvariant();

        if (fullscreenGame)
        {
            return SoundPresets.GamingName;
        }

        if (CallApps.Contains(name) || ContainsAny(text, CallTitles))
        {
            return SoundPresets.CommunicationName;
        }

        if (MusicApps.Contains(name) || ContainsAny(text, MusicTitles))
        {
            return SoundPresets.MusicName;
        }

        if (VideoApps.Contains(name) || ContainsAny(text, VideoTitles))
        {
            return SoundPresets.MovieName;
        }

        return SoundPresets.MusicName;
    }

    private static bool ContainsAny(string text, string[] words) =>
        words.Any(w => text.Contains(w, StringComparison.Ordinal));
}
