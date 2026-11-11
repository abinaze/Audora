using Audora.Core.Devices;

namespace Audora.Core.Diagnostics;

/// <summary>How serious a scan finding is. Ok is good news, Problem needs attention.</summary>
public enum ScanLevel
{
    Ok,
    Info,
    Warning,
    Problem
}

public enum ServiceRunState
{
    Unknown,
    Stopped,
    StartPending,
    StopPending,
    Running,
    Paused
}

public enum ServiceStartup
{
    Unknown,
    Automatic,
    Manual,
    Disabled
}

/// <summary>One thing the scan found, in plain words.</summary>
public sealed record ScanFinding(ScanLevel Level, string Area, string Title, string Detail, string? RepairKey = null, string? DeviceId = null, string? DeviceName = null);

/// <summary>What was read from one Windows service. Read only.</summary>
public sealed record ServiceSnapshot(
    string Name,
    string DisplayName,
    bool Found,
    ServiceRunState State,
    ServiceStartup Startup);

/// <summary>Everything the scan looked at. The rules work on this, so they can be tested without Windows.</summary>
public sealed record ScanInput(
    IReadOnlyList<ServiceSnapshot> Services,
    IReadOnlyList<AudioDeviceInfo> RenderDevices,
    IReadOnlyList<AudioDeviceInfo> CaptureDevices,
    IReadOnlyList<EndpointDetails>? Details = null,
    bool? MonoAudioOn = null,
    IReadOnlyCollection<string>? MutedDeviceIds = null);

public sealed record ScanReport(DateTime TakenAt, ScanInput Input, IReadOnlyList<ScanFinding> Findings)
{
    public int Count(ScanLevel level) => Findings.Count(f => f.Level == level);

    public ScanLevel Worst => Findings.Count == 0 ? ScanLevel.Ok : Findings.Max(f => f.Level);
}
