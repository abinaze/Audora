using System.Text.RegularExpressions;

namespace Audora.Core.Settings;

/// <summary>
/// Windows gives a USB device a new id (and a name like "Speakers (2- AB13X)") when it is plugged into
/// another port. Matching on a cleaned-up name lets Audora recognise it as the same device.
/// </summary>
public static class DeviceMatcher
{
    private static readonly Regex PortNumber = new(@"\(\d+- ", RegexOptions.Compiled);

    public static string Normalize(string? name) =>
        PortNumber.Replace(name ?? string.Empty, "(").Trim().ToLowerInvariant();
}
