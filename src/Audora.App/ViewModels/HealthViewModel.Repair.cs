using System.Diagnostics;
using System.Windows.Input;
using Audora.App.Infrastructure;
using Audora.Core.Diagnostics;

namespace Audora.App.ViewModels;

/// <summary>
/// Repairs on the Health page. Each one asks first, changes one thing, checks the result, and can be undone.
/// Nothing here needs administrator rights.
/// </summary>
public sealed partial class HealthViewModel
{
    private readonly UnmuteRepair _unmute = new(new EndpointMuteAccess());
    private FindingViewModel? _pending;
    private string? _undoDeviceId;
    private string _undoDeviceName = string.Empty;
    private string _repairMessage = string.Empty;
    private RelayCommand? _askRepair;
    private RelayCommand? _confirmRepair;
    private RelayCommand? _cancelRepair;
    private RelayCommand? _undoRepair;

    public RelayCommand AskRepairCommand => _askRepair ??= new RelayCommand(p =>
    {
        if (p is FindingViewModel finding)
        {
            AskRepair(finding);
        }
    });

    public RelayCommand ConfirmRepairCommand =>
        _confirmRepair ??= new RelayCommand(() => _ = ConfirmRepairAsync(), () => _pending is not null);

    public RelayCommand CancelRepairCommand =>
        _cancelRepair ??= new RelayCommand(() => SetPending(null));

    public RelayCommand UndoRepairCommand =>
        _undoRepair ??= new RelayCommand(() => _ = UndoRepairAsync(), () => _undoDeviceId is not null);

    public bool HasPending => _pending is not null;

    public string PendingTitle => _pending is null ? string.Empty : $"Unmute {_pending.DeviceName}?";

    public string PendingText =>
        "Audora will turn off the Windows mute for this device. Nothing else changes, and you can undo it.";

    public string RepairMessage
    {
        get => _repairMessage;
        private set
        {
            if (Set(ref _repairMessage, value))
            {
                Raise(nameof(HasRepairMessage));
            }
        }
    }

    public bool HasRepairMessage => _repairMessage.Length > 0;

    public bool CanUndo => _undoDeviceId is not null;

    private void AskRepair(FindingViewModel finding)
    {
        if (finding.RepairKey == ScanEvaluator.RepairOpenMonoSettings)
        {
            OpenMonoSettings();
        }
        else if (finding.RepairKey == ScanEvaluator.RepairUnmute && !string.IsNullOrEmpty(finding.DeviceId))
        {
            SetPending(finding);
        }
    }

    private void OpenMonoSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:easeofaccess-audio") { UseShellExecute = true });
            RepairMessage = "Opened Windows Settings. Switch Mono audio off there, then press Scan now again. Audora does not change it for you.";
            Log.Info("Repair: opened the Windows Mono audio settings page.");
        }
        catch (Exception ex)
        {
            Log.Error("Could not open Windows Settings", ex);
            RepairMessage = "Audora could not open Windows Settings. Open Settings, Accessibility, Audio, and switch Mono audio off.";
        }
    }

    private async Task ConfirmRepairAsync()
    {
        var finding = _pending;
        if (finding?.DeviceId is null)
        {
            return;
        }

        SetPending(null);
        var id = finding.DeviceId;
        var name = finding.DeviceName;
        Log.Info($"Repair: unmuting {name}, approved by you.");

        var result = await Task.Run(() => _unmute.Apply(id, name));
        Log.Info($"Repair: {result.Message}");
        RepairMessage = result.Message;

        _undoDeviceId = result.Success && result.PreviousMuted == true ? id : null;
        _undoDeviceName = name;
        Raise(nameof(CanUndo));
        CommandManager.InvalidateRequerySuggested();

        await ScanAsync(); // the check afterwards: the list shows what Windows reports now
    }

    private async Task UndoRepairAsync()
    {
        var id = _undoDeviceId;
        if (id is null)
        {
            return;
        }

        var name = _undoDeviceName;
        _undoDeviceId = null;
        Raise(nameof(CanUndo));
        CommandManager.InvalidateRequerySuggested();

        var undone = await Task.Run(() => _unmute.Undo(id, true));
        Log.Info(undone ? $"Repair: {name} is muted again, as before." : $"Repair: could not put the mute back on {name}.");
        RepairMessage = undone
            ? $"{name} is muted again, as it was before."
            : $"Windows would not let Audora put the mute back on {name}.";

        await ScanAsync();
    }

    private void SetPending(FindingViewModel? finding)
    {
        _pending = finding;
        Raise(nameof(HasPending));
        Raise(nameof(PendingTitle));
        CommandManager.InvalidateRequerySuggested();
    }
}
