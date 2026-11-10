using System.Collections.ObjectModel;
using System.Windows.Input;
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
    }

    public string LevelText { get; }

    public string Area { get; }

    public string Title { get; }

    public string Detail { get; }

    public bool HasDetail => Detail.Length > 0;
}

/// <summary>The Health page. It only reads: a scan changes nothing in Windows.</summary>
public sealed class HealthViewModel : ObservableObject
{
    private readonly DeviceEnumerator _devices;
    private bool _scanning;
    private string _summary = "Not scanned yet. Press Scan now.";

    public HealthViewModel(DeviceEnumerator devices)
    {
        _devices = devices;
        ScanCommand = new RelayCommand(() => _ = ScanAsync(), () => !_scanning);
    }

    public ObservableCollection<FindingViewModel> Findings { get; } = new();

    public RelayCommand ScanCommand { get; }

    public bool HasResults => Findings.Count > 0;

    public string ButtonText => _scanning ? "Scanning..." : "Scan now";

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
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
