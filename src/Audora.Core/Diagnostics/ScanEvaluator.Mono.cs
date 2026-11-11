namespace Audora.Core.Diagnostics;

public static partial class ScanEvaluator
{
    public const string SettingsArea = "Windows settings";

    private static void CheckMonoAudio(bool? monoAudioOn, List<ScanFinding> findings)
    {
        if (monoAudioOn is null)
        {
            return;
        }

        findings.Add(monoAudioOn.Value
            ? new ScanFinding(ScanLevel.Warning, SettingsArea, "Windows Mono audio is on",
                "Windows mixes left and right into one on every sound device, so a left-only sound plays on both sides. If you did not turn this on, switch it off in Settings, Accessibility, Audio. Audora does not change it.", RepairOpenMonoSettings)
            : new ScanFinding(ScanLevel.Ok, SettingsArea, "Windows Mono audio is off", string.Empty));
    }
}
