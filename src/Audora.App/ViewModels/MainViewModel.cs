using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using Audora.App.Infrastructure;
using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _ui;
    private readonly DeviceEnumerator _devices;
    private readonly MirrorController _controller;
    private readonly Dictionary<string, DeviceViewModel> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeviceMemory> _memory = new(StringComparer.OrdinalIgnoreCase);

    private double _master = 100;
    private string _search = string.Empty;
    private bool _isDark = true;
    private string _status = "Starting...";
    private string _routingText = "Looking for audio devices...";
    private int _refreshing;
    private volatile bool _again;
    private volatile bool _disposed;

    public MainViewModel(Dispatcher ui)
    {
        _ui = ui;

        // Created on a background thread so Windows audio calls never depend on the UI thread.
        _devices = Task.Run(() => new DeviceEnumerator()).GetAwaiter().GetResult();
        _controller = new MirrorController(_devices);

        View = CollectionViewSource.GetDefaultView(Devices);
        View.Filter = Filter;
        View.SortDescriptions.Add(new SortDescription(nameof(DeviceViewModel.IsDefault), ListSortDirection.Descending));
        View.SortDescriptions.Add(new SortDescription(nameof(DeviceViewModel.Name), ListSortDirection.Ascending));

        RefreshCommand = new RelayCommand(() => _ = RefreshAsync());
        SelectAllCommand = new RelayCommand(() => SetAll(d => d.CanSelect, d => d.IsSelected = true));
        DeselectAllCommand = new RelayCommand(() => SetAll(d => d.CanSelect, d => d.IsSelected = false));
        MuteAllCommand = new RelayCommand(() => SetAll(d => d.IsAvailable, d => d.IsMuted = true));
        UnmuteAllCommand = new RelayCommand(() => SetAll(d => d.IsAvailable, d => d.IsMuted = false));
        ToggleThemeCommand = new RelayCommand(() =>
        {
            IsDark = !IsDark;
            ThemeService.Apply(IsDark);
            Raise(nameof(ThemeButtonText));
        });

        _devices.DevicesChanged += OnDevicesChanged;
        _controller.Message += OnMessage;

        SerialWorker.Run(() => _controller.Start());
        _ = RefreshAsync();
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    public ICollectionView View { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand DeselectAllCommand { get; }

    public RelayCommand MuteAllCommand { get; }

    public RelayCommand UnmuteAllCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public bool IsDark
    {
        get => _isDark;
        private set => Set(ref _isDark, value);
    }

    public string ThemeButtonText => _isDark ? "Light theme" : "Dark theme";

    /// <summary>Master volume, 0 to 100. Multiplies every device's own volume.</summary>
    public double Master
    {
        get => _master;
        set
        {
            if (Set(ref _master, Math.Clamp(value, 0, 100)))
            {
                foreach (var device in Devices)
                {
                    device.ApplyAudio();
                }
            }
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            if (Set(ref _search, value ?? string.Empty))
            {
                View.Refresh();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string RoutingText
    {
        get => _routingText;
        private set => Set(ref _routingText, value);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _devices.DevicesChanged -= OnDevicesChanged;
        _controller.Message -= OnMessage;

        // Let queued audio changes finish, then release everything off the UI thread.
        SerialWorker.Shutdown(TimeSpan.FromSeconds(2));
        var cleanup = Task.Run(() =>
        {
            try { _controller.Dispose(); } catch (Exception ex) { Log.Error("Stopping audio failed", ex); }
            try { _devices.Dispose(); } catch (Exception ex) { Log.Error("Releasing audio devices failed", ex); }
        });
        cleanup.Wait(TimeSpan.FromSeconds(4));
    }

    private double CurrentMaster() => _master / 100.0;

    private bool Filter(object item) =>
        string.IsNullOrWhiteSpace(_search)
        || ((DeviceViewModel)item).Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase);

    private void SetAll(Func<DeviceViewModel, bool> include, Action<DeviceViewModel> change)
    {
        foreach (var device in Devices.Where(include).ToList())
        {
            change(device);
        }
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _ = RefreshAsync();
        }
    }

    private void OnMessage(object? sender, string text)
    {
        if (_disposed)
        {
            return;
        }

        Log.Info(text);
        _ui.BeginInvoke(() => Status = text);
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            _again = true;
            return;
        }

        try
        {
            do
            {
                _again = false;

                // Windows sends a burst of events for one change; wait for it to settle so the
                // window never shows a half-updated picture.
                await Task.Delay(250);
                if (_disposed)
                {
                    return;
                }

                var snapshot = await Task.Run(() =>
                    (Devices: _devices.GetRenderDevices(), State: _controller.GetStatus(), Source: _controller.SourceName));
                await _ui.InvokeAsync(() => Apply(snapshot.Devices, snapshot.State, snapshot.Source));
            }
            while (_again && !_disposed);
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                Log.Error("Could not refresh devices", ex);
                _ = _ui.BeginInvoke(() => Status = $"Could not refresh devices: {ex.Message}");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void Apply(IReadOnlyList<AudioDeviceInfo> devices, IReadOnlyList<MirrorTargetStatus> state, string? source)
    {
        if (_disposed)
        {
            return;
        }

        var visible = DeviceCatalog.Visible(devices);
        var states = state.ToDictionary(s => s.DeviceId, s => s.State, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var info in visible)
        {
            seen.Add(info.Id);
            if (!_byId.TryGetValue(info.Id, out var vm))
            {
                vm = new DeviceViewModel(info, _controller, CurrentMaster);
                if (_memory.TryGetValue(info.Id, out var memory))
                {
                    vm.Restore(memory);
                }

                _byId[info.Id] = vm;
                Devices.Add(vm);
            }

            states.TryGetValue(info.Id, out var mirrorState);
            vm.Update(info, mirrorState);
        }

        // A device that is no longer connected disappears from the list. Its settings are kept.
        foreach (var gone in _byId.Values.Where(v => !seen.Contains(v.Id)).ToList())
        {
            _memory[gone.Id] = gone.Snapshot();
            _byId.Remove(gone.Id);
            Devices.Remove(gone);
        }

        var waiting = state.Where(s => !seen.Contains(s.DeviceId)).Select(s => s.Name).ToList();
        var playing = Devices.Count(d => d.IsSelected);

        var text = source is null
            ? "Audio routing is paused: no active default output."
            : $"Listening to {source}  -  playing on {playing} device{(playing == 1 ? "" : "s")}";
        if (waiting.Count > 0)
        {
            text += $"  -  waiting for {string.Join(", ", waiting)}";
        }

        RoutingText = text;
        View.Refresh();
    }
}
