namespace Audora.Core.Diagnostics;

/// <summary>One tone test: what to play, what the person should hear, and what to ask afterwards.</summary>
public sealed record SoundCheckTest(string Key, string Title, string Hint, string Question, ToneRequest Request);

public static class SoundCheckTests
{
    public static IReadOnlyList<SoundCheckTest> All { get; } = new[]
    {
        new SoundCheckTest(
            "left",
            "Left side",
            "You should hear a steady tone from the LEFT side only.",
            "Did you hear the tone from the left side only?",
            new ToneRequest(ToneChannel.Left, ToneShape.Steady, 440, 3)),
        new SoundCheckTest(
            "right",
            "Right side",
            "You should hear a steady tone from the RIGHT side only.",
            "Did you hear the tone from the right side only?",
            new ToneRequest(ToneChannel.Right, ToneShape.Steady, 440, 3)),
        new SoundCheckTest(
            "both",
            "Both sides",
            "You should hear the same steady tone from BOTH sides.",
            "Did you hear the same tone from both sides?",
            new ToneRequest(ToneChannel.Both, ToneShape.Steady, 440, 3)),
        new SoundCheckTest(
            "sweep",
            "Sweep",
            "You should hear a smooth tone that rises from low to high.",
            "Did you hear a smooth tone rising from low to high, with no crackle or buzzing?",
            new ToneRequest(ToneChannel.Both, ToneShape.Sweep, 0, 6))
    };

    public static SoundCheckTest? Find(string key) =>
        All.FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
