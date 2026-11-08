using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Audora.App.ViewModels;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace Audora.App.Infrastructure;

/// <summary>
/// The Audora icon in the notification area: left-click shows or hides the window, right-click opens quick
/// controls (volume, mute, sound profile, groups, quit). Uses the standard Windows menu look.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly WinForms.NotifyIcon _icon;
    private readonly Drawing.Icon _image;
    private readonly IntPtr _handle;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public TrayIconService(MainWindow window, MainViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;
        _image = CreateIcon(out _handle);

        _icon = new WinForms.NotifyIcon
        {
            Icon = _image,
            Text = "Audora",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip()
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                Toggle();
            }
        };
        _icon.ContextMenuStrip.Opening += (_, _) => Rebuild();
    }

    /// <summary>Shows the "still running here" note the first time the window goes to the tray.</summary>
    public void ShowHintOnce()
    {
        if (_viewModel.TrayHintShown)
        {
            return;
        }

        _viewModel.TrayHintShown = true;
        _icon.ShowBalloonTip(
            5000,
            "Audora is still running",
            "Right-click this icon for quick controls, or choose Quit to close Audora.",
            WinForms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
        DestroyIcon(_handle);
    }

    private void Toggle()
    {
        if (_window.IsVisible && _window.WindowState != WindowState.Minimized)
        {
            _window.HideToTray();
        }
        else
        {
            _window.ShowFromTray();
        }
    }

    private void Rebuild()
    {
        var menu = _icon.ContextMenuStrip!;
        menu.Items.Clear();

        menu.Items.Add(Item("Open Audora", _window.ShowFromTray, bold: true));
        menu.Items.Add(new WinForms.ToolStripSeparator());

        menu.Items.Add(Item(_viewModel.EngineOn ? "Turn Audora off" : "Turn Audora on", () => _viewModel.EngineOn = !_viewModel.EngineOn));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        if (!_viewModel.EngineOn)
        {
            menu.Items.Add(Item("Quit Audora", App.Quit));
            return;
        }

        menu.Items.Add(Item($"Master volume {_viewModel.Master:0}%", () => { }, enabled: false));
        menu.Items.Add(Item("Volume up", () => _viewModel.Master += 10));
        menu.Items.Add(Item("Volume down", () => _viewModel.Master -= 10));
        menu.Items.Add(Item(_viewModel.IsMasterMuted ? "Unmute" : "Mute", () => _viewModel.IsMasterMuted = !_viewModel.IsMasterMuted));
        menu.Items.Add(new WinForms.ToolStripSeparator());

        var profiles = new WinForms.ToolStripMenuItem("Sound profile");
        foreach (var preset in _viewModel.Sound.Presets)
        {
            var item = preset;
            profiles.DropDownItems.Add(Item(item.Name, () => _viewModel.Sound.ChoosePresetCommand.Execute(item), isChecked: item.IsActive));
        }

        menu.Items.Add(profiles);

        if (_viewModel.Groups.Count > 0)
        {
            var groups = new WinForms.ToolStripMenuItem("Groups");
            foreach (var group in _viewModel.Groups)
            {
                var item = group;
                groups.DropDownItems.Add(item.IsActive
                    ? Item($"Stop {item.Name}", () => item.StopCommand?.Execute(null), isChecked: true)
                    : Item($"Play {item.Name}", () => item.PlayCommand?.Execute(null)));
            }

            menu.Items.Add(groups);
        }

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Item("Quit Audora", App.Quit));
    }

    private static WinForms.ToolStripMenuItem Item(string text, Action click, bool bold = false, bool isChecked = false, bool enabled = true)
    {
        var item = new WinForms.ToolStripMenuItem(text) { Checked = isChecked, Enabled = enabled };
        if (bold)
        {
            item.Font = new Drawing.Font(item.Font, Drawing.FontStyle.Bold);
        }

        item.Click += (_, _) => click();
        return item;
    }

    // Draws the Audora mark from the app's own shapes, so no icon file is needed.
    private static Drawing.Icon CreateIcon(out IntPtr handle)
    {
        const int size = 32;
        const double markWidth = 746;
        const double markHeight = 642;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var scale = size * 0.92 / markWidth;
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, (size - markWidth * scale) / 2, (size - markHeight * scale) / 2));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x35, 0x1B, 0x9F)), null, (Geometry)Application.Current.FindResource("MarkDark"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x16, 0x76, 0xFC)), null, (Geometry)Application.Current.FindResource("MarkBlue"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x7E, 0x33, 0xF5)), null, (Geometry)Application.Current.FindResource("MarkPurple"));
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        using var picture = new Drawing.Bitmap(stream);
        handle = picture.GetHicon();
        return Drawing.Icon.FromHandle(handle);
    }
}
