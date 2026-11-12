using System.Collections.ObjectModel;
using System.Diagnostics;
using Audora.App.Infrastructure;
using Audora.Core.Devices;

namespace Audora.App.ViewModels;

/// <summary>One paired Bluetooth speaker or headphones that can be connected or disconnected from Audora.</summary>
public sealed class BluetoothItemViewModel : ObservableObject
{
    private bool _isConnected;
    private bool _isBusy;

    public BluetoothItemViewModel(ulong address, string name)
    {
        Address = address;
        Name = string.IsNullOrWhiteSpace(name) ? "Bluetooth device" : name;
    }

    public ulong Address { get; }

    public string Name { get; }

    public bool IsConnected => _isConnected;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (Set(ref _isBusy, value))
            {
                Raise(nameof(StatusText));
                Raise(nameof(CanToggle));
            }
        }
    }

    public string StatusText => _isBusy ? "Working..." : _isConnected ? "Connected" : "Not connected";

    public string ButtonText => _isConnected ? "Disconnect" : "Connect";

    public bool CanToggle => !_isBusy;

    public RelayCommand? ToggleCommand { get; set; }

    public void SetConnected(bool connected)
    {
        if (Set(ref _isConnected, connected, nameof(IsConnected)))
        {
            Raise(nameof(StatusText));
            Raise(nameof(ButtonText));
        }
    }
}

public sealed partial class MainViewModel
{
    private DateTime _lastBluetoothRefresh = DateTime.MinValue;
    private string _bluetoothMessage = string.Empty;
    private int _bluetoothRefreshing;

    public ObservableCollection<BluetoothItemViewModel> BluetoothDevices { get; } = new();

    public RelayCommand RefreshBluetoothCommand { get; private set; } = null!;

    public RelayCommand ConnectAllBluetoothCommand { get; private set; } = null!;

    public RelayCommand PairNewDeviceCommand { get; private set; } = null!;

    public bool HasNoBluetooth => BluetoothDevices.Count == 0;

    public string BluetoothMessage
    {
        get => _bluetoothMessage;
        private set => Set(ref _bluetoothMessage, value);
    }

    private void InitializeBluetooth()
    {
        RefreshBluetoothCommand = new RelayCommand(() => _ = RefreshBluetoothAsync());
        ConnectAllBluetoothCommand = new RelayCommand(() => _ = ConnectAllBluetoothAsync());
        PairNewDeviceCommand = new RelayCommand(OpenBluetoothSettings);
        _ = RefreshBluetoothAsync();
    }

    private void RefreshBluetoothSoon()
    {
        if ((DateTime.UtcNow - _lastBluetoothRefresh).TotalSeconds < 5)
        {
            return;
        }

        _lastBluetoothRefresh = DateTime.UtcNow;
        _ = RefreshBluetoothAsync();
    }

    private async Task RefreshBluetoothAsync()
    {
        if (_disposed || !_engineOn || Interlocked.Exchange(ref _bluetoothRefreshing, 1) == 1)
        {
            return;
        }

        try
        {
            var paired = await Task.Run(() => BluetoothAudio.ListPaired());
            if (_disposed)
            {
                return;
            }

            var seen = new HashSet<ulong>();
            foreach (var device in paired)
            {
                seen.Add(device.Address);
                var item = BluetoothDevices.FirstOrDefault(b => b.Address == device.Address);
                if (item is null)
                {
                    item = new BluetoothItemViewModel(device.Address, device.Name);
                    var created = item;
                    created.ToggleCommand = new RelayCommand(() => _ = ToggleBluetoothAsync(created));
                    BluetoothDevices.Add(item);
                }

                item.SetConnected(device.IsConnected);
            }

            foreach (var gone in BluetoothDevices.Where(b => !seen.Contains(b.Address)).ToList())
            {
                BluetoothDevices.Remove(gone);
            }

            Raise(nameof(HasNoBluetooth));
        }
        catch (Exception ex)
        {
            Log.Error("Could not list Bluetooth devices", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _bluetoothRefreshing, 0);
        }
    }

    private async Task ToggleBluetoothAsync(BluetoothItemViewModel item)
    {
        if (item.IsBusy || _disposed)
        {
            return;
        }

        var connect = !item.IsConnected;
        item.IsBusy = true;
        BluetoothMessage = connect
            ? $"Connecting {item.Name}... this can take up to 10 seconds."
            : $"Disconnecting {item.Name}...";

        BluetoothResult result;
        try
        {
            result = await Task.Run(() => connect ? BluetoothAudio.Connect(item.Address) : BluetoothAudio.Disconnect(item.Address));
            await Task.Delay(connect ? 2500 : 1000);
        }
        catch (Exception ex)
        {
            Log.Error("Bluetooth change failed", ex);
            result = BluetoothResult.Failed;
        }

        item.IsBusy = false;
        await RefreshBluetoothAsync();

        BluetoothMessage = result switch
        {
            BluetoothResult.Done when connect && item.IsConnected => $"{item.Name} is connected. It shows up in the device list above.",
            BluetoothResult.Done when connect => $"Connection requested for {item.Name}. If it does not appear shortly, check that it is on and near the PC.",
            BluetoothResult.Done => $"{item.Name} was disconnected.",
            BluetoothResult.NotFound => $"{item.Name} is not paired in Windows. Pair it again in Bluetooth settings.",
            _ => $"Windows could not {(connect ? "connect" : "disconnect")} {item.Name}. Make sure it is switched on, close to the PC and not connected to a phone."
        };
    }

    private async Task ConnectAllBluetoothAsync()
    {
        var waiting = BluetoothDevices.Where(b => !b.IsConnected).ToList();
        if (waiting.Count == 0)
        {
            BluetoothMessage = "All paired Bluetooth devices are already connected.";
            return;
        }

        foreach (var item in waiting)
        {
            await ToggleBluetoothAsync(item);
        }

        BluetoothMessage = "Done. Devices that did not connect may be off or out of range. Most PCs support two to four at once.";
    }

    // The "Connect" button on a saved device: match it to a paired Bluetooth device by name.
    private void ConnectKnown(KnownDeviceViewModel known)
    {
        var name = DeviceCatalog.BaseName(known.Name);
        var item = BluetoothDevices.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            BluetoothMessage = $"Audora could not find \"{name}\" among your paired Bluetooth devices. Opening Bluetooth settings.";
            OpenBluetoothSettings();
            return;
        }

        if (!item.IsConnected)
        {
            _ = ToggleBluetoothAsync(item);
        }
    }

    private static void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Could not open Bluetooth settings", ex);
        }
    }
}
