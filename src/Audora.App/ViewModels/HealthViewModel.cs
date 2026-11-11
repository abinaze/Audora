using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Audora.App.Infrastructure;
using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.App.ViewModels;

/// <summary>One line of the scan result, ready to show.</summary>
public sealed class FindingViewModel
{
    public FindingViewModel(ScanFinding finding)
    {
        LevelText = finding.Level switch
        {
            ScanLevel.Ok => "OK",
            ScanLevel.Info => "NOTE",
            ScanLevel.Warning => "WARNING",
            _ => "PROBLEM"
        };
        Area = finding.Area;
        Title = finding.Title;
        Detail = finding.Detail;
        RepairKey = finding.RepairKey;
        DeviceId = finding.DeviceId;
        DeviceName = finding.DeviceName ?? string.Empty;
    }

    public string LevelText { get; }

    public string Area { get; }

    public string Title { get; }

    public string Detail { get; }

    public bool HasDetail => Detail.Length > 0;

    public string? RepairKey { get; }

    public string? DeviceId { get; }

    public string DeviceName { get; }

    public bool CanRepair => RepairKey is not null;

    public string RepairLabel => RepairKey == ScanEvaluator.RepairUnmute ? "Unmute" : RepairKey == ScanEvaluator.RepairOpenMonoSettings ? "Open Settings" : "Fix";
}

/// <summary>One page chip on the Health page: Scan or Sound Check.</summary>
public sealed class HealthPage : ObservableObject
{
    private bool _isActive;

    public HealthPage(string label) => Label = label;

    public string Label { get; }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }
}

/// <summary>The Health page. The scan only reads. Sound Check plays quiet tones and never changes your volume.</summary>
public sealed partial class HealthViewModel : ObservableObject, IDisposable
{
    private readonly DeviceEnumerator _devices;
    private readonly HealthPage _scanPage = new("Scan") { IsActive = true };
    private readonly HealthPage _soundCheckPage = new("Sound Check");
    private bool _scanning;
    private string _summary = "Not scanned yet. Press Scan now.";

    public HealthViewModel(DeviceEnumerator devices, Dispatcher ui, Func<bool> isEngineOn, Func<string> copiesTo, Action turnEngineOff)
    {
        _devices = devices;
        SoundCheck = new SoundCheckViewModel(devices, ui, isEngineOn, copiesTo, turnEngineOff);
        Pages = new[] { _scanPage, _soundCheckPage };

        ScanCommand = new RelayCommand(() => _ = ScanAsync(), () => !_scanning);
        ChoosePageCommand = new RelayCommand(p =>
        {
            if (p is HealthPage page)
            {
                ShowPage(page);
            }
        });
    }

    public IReadOnlyList<HealthPage> Pages { get; }

    public SoundCheckViewModel SoundCheck { get; }

    public ObservableCollection<FindingViewModel> Findings { get; } = new();

    public RelayCommand ScanCommand { get; }

    public RelayCommand ChoosePageCommand { get; }

    public bool IsScanPage => _scanPage.IsActive;

    public bool IsSoundCheckPage => _soundCheckPage.IsActive;

    public bool HasResults => Findings.Count > 0;

    public string ButtonText => _scanning ? "Scanning..." : "Scan now";

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    /// <summary>Ends any test tone at once. Called when the Health page is left.</summary>
    public void StopSounds() => SoundCheck.Stop();

    public void Dispose() => SoundCheck.Dispose();

    private void ShowPage(HealthPage page)
    {
        _scanPage.IsActive = ReferenceEquals(page, _scanPage);
        _soundCheckPage.IsActive = ReferenceEquals(page, _soundCheckPage);
        Raise(nameof(IsScanPage));
        Raise(nameof(IsSoundCheckPage));

        if (IsSoundCheckPage)
        {
            SoundCheck.RefreshState();
        }
        else
        {
            SoundCheck.Stop();
        }
    }

    private async Task ScanAsync()
    {
        if (_scanning)
        {
            return;
        }

        SetScanning(true);
        try
        {
            var report = await Task.Run(() => new AudioScanner(_devices).Run());

            Findings.Clear();
            foreach (var finding in report.Findings)
            {
                Findings.Add(new FindingViewModel(finding));
            }

            Raise(nameof(HasResults));
            Summary = $"Scanned at {report.TakenAt:HH:mm}. {ScanSummary.Describe(report)}";
        }
        catch (Exception ex)
        {
            Log.Error("Health scan failed", ex);
            Summary = "The scan could not finish. Windows audio was not changed. Details are in the log.";
        }
        finally
        {
            SetScanning(false);
        }
    }

    private void SetScanning(bool value)
    {
        _scanning = value;
        Raise(nameof(ButtonText));
        CommandManager.InvalidateRequerySuggested();
    }
}
