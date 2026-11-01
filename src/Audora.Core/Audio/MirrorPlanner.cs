using Audora.Core.Devices;

namespace Audora.Core.Audio;

/// <summary>What must change to bring the running mirror in line with the devices and selection.</summary>
public sealed record MirrorPlan(
    string? SourceId,
    bool RestartCapture,
    IReadOnlyList<string> Remove,
    IReadOnlyList<string> Add);

public static class MirrorPlanner
{
    /// <summary>
    /// Works out which outputs to start and stop. The Windows default output is the capture
    /// source and plays natively, so it is never a mirror target. Only active devices can be targets.
    /// </summary>
    public static MirrorPlan Plan(
        IEnumerable<AudioDeviceInfo> devices,
        IEnumerable<string> selectedIds,
        string? currentSourceId,
        IReadOnlyCollection<string> currentOutputIds)
    {
        var list = devices.ToList();
        var sourceId = list.FirstOrDefault(d => d.IsDefault && d.Status == DeviceStatus.Active)?.Id;

        if (sourceId is null)
        {
            return new MirrorPlan(null, false, currentOutputIds.ToList(), Array.Empty<string>());
        }

        var activeIds = list
            .Where(d => d.Status == DeviceStatus.Active)
            .Select(d => d.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var desired = selectedIds
            .Where(id => activeIds.Contains(id) && !Same(id, sourceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!Same(sourceId, currentSourceId))
        {
            // Capture has to be rebuilt on the new source, which drops every current output.
            return new MirrorPlan(sourceId, true, currentOutputIds.ToList(), desired);
        }

        var remove = currentOutputIds
            .Where(id => !desired.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var add = desired
            .Where(id => !currentOutputIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new MirrorPlan(sourceId, false, remove, add);
    }

    private static bool Same(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
