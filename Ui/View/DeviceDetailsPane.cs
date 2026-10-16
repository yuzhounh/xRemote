using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using Microsoft.Win32;

namespace _1RM.View;

public sealed class DeviceDetailsPane : UserControl, IDisposable
{
    public ProtocolBaseViewModel Device { get; }
    public event Action? CloseRequested;
    private readonly string address;
    private readonly TextBlock online = Value(), usage = Value(), sessionNote = Value(), hardwareNote = Value(), storageNote = Value();
    private readonly StackPanel sessionRows = new(), hardwareRows = new(), storageRows = new();
    private readonly Image preview = new() { Height = 150, Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
    private readonly Button refreshStatus, refreshHardware, refreshStorage;
    private readonly Grid dashboard = new();
    private readonly StackPanel leftColumn = new(), rightColumn = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private CancellationTokenSource? lifetime;
    private DeviceHardwareInfo? hardware, storage;
    private bool statusBusy, hardwareBusy, storageBusy, disposed;

    public DeviceDetailsPane(ProtocolBaseViewModel device, bool embedded)
    {
        Device = device;
        address = (device.Server as ProtocolBaseWithAddressPort)?.Address ?? "";
        SetResourceReference(FontFamilyProperty, "GlobalFontFamily");
        SetResourceReference(FontSizeProperty, "GlobalFontSizeBody");
        SetResourceReference(ForegroundProperty, "BackgroundTextBrush");
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        var layout = new DockPanel { Margin = new Thickness(18, 12, 18, 18) };
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = ActionButton("Close", () => { CloseRequested?.Invoke(); return Task.CompletedTask; });
        close.Content = "×"; close.FontSize = 20; close.Width = 40; close.MinWidth = 40; close.Padding = new Thickness(0);
        UiText.Bind(close, ToolTipProperty, "Close");
        close.Visibility = embedded ? Visibility.Visible : Visibility.Collapsed;
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(Label("Device.Details", 18, true));
        if (embedded) { DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading); }
        var content = new StackPanel();
        layout.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = layout;
        var identity = new DockPanel();
        var icon = new Image { Source = device.Server.IconImg, Width = 32, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(icon, Dock.Left); identity.Children.Add(icon);
        identity.Children.Add(new TextBlock { Text = device.DisplayName, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(identity);
        AddValue(content, "Device.Connection", device.Server.SubTitle);
        AddValue(content, "Protocol", device.Server.ProtocolDisplayName);
        if (device.Tags.Count > 0) AddValue(content, "Tags", string.Join(" · ", device.Tags.Select(t => t.Name)));
        var statuses = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        online.Margin = new Thickness(0, 0, 24, 0); usage.Margin = new Thickness(0);
        statuses.Children.Add(online); statuses.Children.Add(usage); content.Children.Add(statuses);

        dashboard.ColumnDefinitions.Add(new ColumnDefinition());
        dashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        dashboard.ColumnDefinitions.Add(new ColumnDefinition());
        dashboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dashboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dashboard.Children.Add(leftColumn); dashboard.Children.Add(rightColumn); content.Children.Add(dashboard);
        SizeChanged += (_, _) => ArrangeColumns();

        var previewArea = new StackPanel();
        previewArea.Children.Add(preview);
        var previewHint = Label("Device.PreviewHint"); previewHint.Opacity = .7; previewArea.Children.Add(previewHint);
        previewArea.Children.Add(ActionButton("Device.SetPreview", SelectPreview));
        var previewSection = new Expander { Content = previewArea, Margin = new Thickness(0, 12, 0, 4) };
        previewSection.Header = Label("Device.Preview");
        LoadPreview();

        refreshStatus = ActionButton("Remote.Refresh", ReloadStatus);
        var sessions = Section(leftColumn, "Remote.Title", refreshStatus);
        var sessionActions = new WrapPanel();
        sessionActions.Children.Add(ActionButton("Remote.ImportPairing", ImportPairing));
        sessionActions.Children.Add(ActionButton("Device.ManageSessions", () =>
        {
            if (address.Length > 0) new RemoteSessionsWindow(address, Device.DisplayName) { Owner = Window.GetWindow(this) }.Show();
            return Task.CompletedTask;
        }));
        sessions.Children.Add(sessionActions); sessions.Children.Add(sessionNote); sessions.Children.Add(sessionRows);
        refreshHardware = ActionButton("Remote.Refresh", () => ReloadHardware(true));
        var info = Section(leftColumn, "Device.Hardware", refreshHardware);
        info.Children.Add(hardwareNote); info.Children.Add(hardwareRows);
        refreshStorage = ActionButton("Remote.Refresh", () => ReloadStorage(true));
        var disks = Section(rightColumn, "Device.Storage", refreshStorage);
        disks.Children.Add(storageNote); disks.Children.Add(storageRows);
        rightColumn.Children.Add(previewSection);
        if (!string.IsNullOrWhiteSpace(device.Server.Note))
        {
            var notes = Section(rightColumn, "Note");
            notes.Children.Add(new _1RM.Controls.NoteDisplay.NoteDisplayAndEditor
            { Server = device.Server, EditEnable = false, Height = 200, CloseButtonVisibility = Visibility.Collapsed });
        }
        Loaded += OnLoaded;
        Unloaded += (_, _) => Dispose();
        timer.Tick += async (_, _) =>
        {
            if (IsVisible && Window.GetWindow(this)?.WindowState != WindowState.Minimized) await ReloadStatus();
        };
    }

    private void ArrangeColumns()
    {
        bool wide = ActualWidth >= 800;
        dashboard.ColumnDefinitions[1].Width = new GridLength(wide ? 16 : 0);
        dashboard.ColumnDefinitions[2].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(rightColumn, wide ? 2 : 0); Grid.SetRow(rightColumn, wide ? 0 : 1);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (disposed || lifetime != null) return;
        lifetime = new CancellationTokenSource();
        Device.PropertyChanged += DeviceChanged;
        UpdateStatus(); RenderSessions();
        hardware = DeviceInformationCache.Load(address, false);
        storage = DeviceInformationCache.Load(address, true);
        if (hardware != null) RenderHardware(hardware);
        if (storage != null) RenderStorage(storage);
        else if (hardware != null) { storage = hardware; RenderStorage(hardware); }
        timer.Start();
        await Task.WhenAll(ReloadStatus(), ReloadHardware(false));
    }
    private void DeviceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (disposed) return;
        if (e.PropertyName == nameof(Device.RemoteStatus)) { UpdateStatus(); RenderSessions(); }
        else if (e.PropertyName == nameof(Device.ConnectionStatusText)) UpdateStatus();
    }
    private void UpdateStatus()
    {
        online.Text = UiText.Get("Shell.Status") + " · " + Device.ConnectionStatusText;
        usage.Text = UiText.Get("Device.RemoteUsage") + " · " + Device.RemoteUsageText;
        usage.ToolTip = Device.RemoteUsageHint;
    }
    private bool Paired()
    {
        try { return address.Length > 0 && DeviceControlClient.Find(address) != null; }
        catch { return false; }
    }
    private async Task ReloadStatus()
    {
        if (statusBusy || lifetime == null || disposed) return;
        if (!Paired())
        {
            Device.SetRemoteSessionStatus(null); sessionNote.Text = UiText.Get("Device.PairingNeeded"); return;
        }
        statusBusy = true; refreshStatus.IsEnabled = false;
        try
        {
            var state = await DeviceControlClient.Status(address, lifetime.Token);
            if (!disposed) Device.SetRemoteSessionStatus(state);
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!disposed) { Device.SetRemoteSessionStatus(null); sessionNote.Text = UiText.Get("Device.UsageUnknownHint"); }
        }
        finally { statusBusy = false; refreshStatus.IsEnabled = true; }
    }
    private async Task ReloadHardware(bool refresh)
    {
        if (hardwareBusy || lifetime == null || disposed) return;
        if (!Paired()) { hardwareNote.Text = UiText.Get("Device.PairingNeeded"); return; }
        hardwareBusy = true; refreshHardware.IsEnabled = false; hardwareNote.Text = UiText.Get("Device.Loading");
        try
        {
            var value = await DeviceInformationCache.Read(address, false, refresh, lifetime.Token);
            if (disposed) return;
            hardware = value; RenderHardware(value);
            // Hardware refresh never replaces an already displayed storage snapshot.
            if (storage == null) { storage = value; RenderStorage(value); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!disposed) hardwareNote.Text = UiText.Get(ex is DeviceInformationException failure ? failure.ResourceKey : "Device.ReadFailed")
                + (hardware != null ? " · " + UiText.Get("Device.KeepingCache") + " · " + Observed(hardware.ObservedAt) : "");
        }
        finally { hardwareBusy = false; refreshHardware.IsEnabled = true; }
    }
    private async Task ReloadStorage(bool refresh)
    {
        if (storageBusy || lifetime == null || disposed) return;
        if (!Paired()) { storageNote.Text = UiText.Get("Device.PairingNeeded"); return; }
        storageBusy = true; refreshStorage.IsEnabled = false; storageNote.Text = UiText.Get("Device.Loading");
        try
        {
            var value = await DeviceInformationCache.Read(address, true, refresh, lifetime.Token);
            if (!disposed) { storage = value; RenderStorage(value); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!disposed) storageNote.Text = UiText.Get(ex is DeviceInformationException failure ? failure.ResourceKey : "Device.ReadFailed")
                + (storage != null ? " · " + UiText.Get("Device.KeepingCache") + " · " + Observed(storage.ObservedAt) : "");
        }
        finally { storageBusy = false; refreshStorage.IsEnabled = true; }
    }
    private async Task ImportPairing()
    {
        if (address.Length == 0 || disposed) return;
        var picker = new OpenFileDialog { Filter = UiText.Get("Remote.PairingFilter") + "|*.json" };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            DeviceControlClient.Import(address, picker.FileName);
            hardware = null; storage = null; hardwareRows.Children.Clear(); storageRows.Children.Clear();
            await Task.WhenAll(ReloadStatus(), ReloadHardware(true));
            IoC.Get<ConnectionStatusService>().RequestRefresh();
        }
        catch { sessionNote.Text = UiText.Get("Remote.PairingFailed"); }
    }
    private void RenderSessions()
    {
        sessionRows.Children.Clear();
        var state = Device.RemoteStatus;
        if (state == null) { sessionNote.Text = UiText.Get(Paired() ? "Device.UsageUnknownHint" : "Device.PairingNeeded"); return; }
        sessionNote.Text = state.Machine + " · " + Observed(state.ObservedAt)
            + (Device.HasFreshRemoteStatus ? "" : " · " + UiText.Get("Device.Stale"));
        var rows = state.Sessions.Where(s => !string.IsNullOrWhiteSpace(s.Session.User) || s.Session.IsRemoteActive)
            .OrderByDescending(s => s.Session.IsRemoteActive).ThenBy(s => s.Session.User).GroupBy(s => s.Session.User);
        if (!rows.Any()) { sessionRows.Children.Add(Label("Remote.Empty")); return; }
        foreach (var group in rows)
        {
            var account = group.Key;
            if (account.StartsWith(state.Machine + "\\", StringComparison.OrdinalIgnoreCase)) account = account[(state.Machine.Length + 1)..];
            var card = Section(sessionRows, null); card.Children.Add(new TextBlock { Text = account, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            foreach (var row in group)
            {
                var session = row.Session;
                var key = session.IsRemoteActive ? "Shell.Remote" : session.State == 4 ? "Remote.Disconnected"
                    : session.Protocol == 0 ? "Remote.LocalSession" : "Remote.OtherSession";
                card.Children.Add(Label(key));
                AddValue(card, "Device.Source", string.IsNullOrWhiteSpace(session.Client) ? UiText.Get("Device.NotRecorded") : session.Client);
                var details = new StackPanel();
                AddValue(details, "Device.SessionId", session.Id.ToString(CultureInfo.InvariantCulture));
                AddValue(details, "Device.Account", session.User);
                AddValue(details, "Device.LockState", session.Locked.HasValue ? UiText.Get(session.Locked.Value ? "Device.Locked" : "Device.Unlocked") : Unknown());
                AddValue(details, "Device.Logon", FileTime(session.LogonTime));
                var more = new Expander { Content = details, Margin = new Thickness(0, 4, 0, 8) };
                more.Header = Label("Device.More"); card.Children.Add(more);
            }
        }
    }
    private void RenderHardware(DeviceHardwareInfo info)
    {
        hardwareRows.Children.Clear(); hardwareNote.Text = Observed(info.ObservedAt);
        AddValue(hardwareRows, "Device.Machine", info.Machine);
        if (info.Windows is { } windows)
        {
            AddValue(hardwareRows, "Windows", Join(windows.Name, windows.Architecture, windows.DisplayVersion, windows.Version, windows.Build));
            AddValue(hardwareRows, "Device.LastBoot", windows.LastBootAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? Unknown());
        }
        if (info.Cpu is { } cpu)
        {
            AddValue(hardwareRows, "CPU", string.Join(" / ", cpu.Models ?? Array.Empty<string>()));
            if (cpu.Cores > 0) AddValue(hardwareRows, "Device.CpuLayout", UiText.Get("Device.CpuLayoutValue", cpu.Sockets, cpu.Cores, cpu.Threads));
        }
        AddValue(hardwareRows, "Device.Memory", Bytes(info.MemoryBytes));
        var gpus = (info.Gpus ?? Array.Empty<string>()).Where(g => !g.Contains("basic display", StringComparison.OrdinalIgnoreCase) && !g.Contains("基本显示"));
        AddValue(hardwareRows, "GPU", string.Join(" / ", gpus));
        if (info.Unavailable?.Length > 0) hardwareRows.Children.Add(Label("Device.Partial"));
    }
    private void RenderStorage(DeviceHardwareInfo info)
    {
        storageRows.Children.Clear(); storageNote.Text = Observed(info.ObservedAt);
        var volumes = info.Volumes ?? Array.Empty<DeviceVolumeInfo>();
        foreach (var disk in info.Disks ?? Array.Empty<DeviceDiskInfo>())
        {
            var card = Section(storageRows, null);
            card.Children.Add(new TextBlock { Text = UiText.Get("Device.Disk", disk.Index) + " · " + disk.Model, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            AddValue(card, "Device.Total", Bytes(disk.SizeBytes));
            foreach (var volume in volumes.Where(v => v.DiskNumbers?.Length == 1 && v.DiskNumbers[0] == disk.Index)) RenderVolume(card, volume);
        }
        foreach (var volume in volumes.Where(v => v.DiskNumbers?.Length != 1 || !(info.Disks ?? Array.Empty<DeviceDiskInfo>()).Any(d => d.Index == v.DiskNumbers[0])))
        {
            var card = Section(storageRows, null);
            card.Children.Add(Label(volume.DiskNumbers?.Length > 1 ? "Device.SpannedVolume" : "Device.UnmappedVolume"));
            RenderVolume(card, volume);
        }
        if (storageRows.Children.Count == 0) storageRows.Children.Add(Label("Device.NoStorage"));
        if (info.Unavailable?.Length > 0) storageRows.Children.Add(Label("Device.Partial"));
    }
    private static void RenderVolume(StackPanel target, DeviceVolumeInfo volume)
    {
        var text = Join(volume.Drive, volume.Label, volume.FileSystem) + " · " + UiText.Get("Device.Total") + " " + Bytes(volume.SizeBytes);
        bool known = volume.SizeBytes > 0 && volume.FreeBytes >= 0 && volume.FreeBytes <= volume.SizeBytes;
        if (known) text += " · " + UiText.Get("Device.Free") + " " + Bytes(volume.FreeBytes) + " · " + UiText.Get("Device.Used") + " " + Bytes(volume.SizeBytes - volume.FreeBytes);
        target.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 2) });
        if (known)
        {
            var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 100.0 * (volume.SizeBytes!.Value - volume.FreeBytes!.Value) / volume.SizeBytes.Value,
                Height = 5, Margin = new Thickness(0, 2, 0, 6) };
            bar.SetResourceReference(Control.ForegroundProperty, "AccentMidBrush"); target.Children.Add(bar);
        }
    }
    private void LoadPreview()
    {
        try
        {
            var path = DeviceInformationCache.PreviewPath(address.Length > 0 ? address : Device.Id);
            if (!File.Exists(path)) return;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze();
            preview.Source = image; preview.Visibility = Visibility.Visible;
        }
        catch { preview.Source = null; preview.Visibility = Visibility.Collapsed; }
    }
    private Task SelectPreview()
    {
        var picker = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return Task.CompletedTask;
        try
        {
            if (new FileInfo(picker.FileName).Length > 20 * 1024 * 1024) throw new IOException();
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1280;
            image.UriSource = new Uri(picker.FileName); image.EndInit(); image.Freeze();
            var path = DeviceInformationCache.PreviewPath(address.Length > 0 ? address : Device.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = File.Create(path)) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(stream); }
            preview.Source = image; preview.Visibility = Visibility.Visible;
        }
        catch { sessionNote.Text = UiText.Get("Device.PreviewFailed"); }
        return Task.CompletedTask;
    }

    private static string Unknown() => UiText.Get("Device.Unavailable");
    private static string Join(params string[] values) => string.Join(" · ", values.Where(v => !string.IsNullOrWhiteSpace(v)));
    private static string Observed(DateTimeOffset time) => time == default ? Unknown() : UiText.Get("Device.ObservedAt", time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
    private static string FileTime(long value) { try { return value > 0 ? DateTime.FromFileTimeUtc(value).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : Unknown(); } catch { return Unknown(); } }
    private static string Bytes(long? value)
    {
        if (!value.HasValue || value < 0) return Unknown();
        double amount = value.Value; string[] units = { "B", "KiB", "MiB", "GiB", "TiB" }; int unit = 0;
        while (amount >= 1024 && unit < units.Length - 1) { amount /= 1024; unit++; }
        return amount.ToString("0.##", CultureInfo.CurrentCulture) + " " + units[unit];
    }
    private static TextBlock Value()
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 5) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "BackgroundTextBrush");
        return text;
    }
    private static TextBlock Label(string key, double size = 12, bool bold = false)
    {
        var text = Value(); text.FontSize = size; text.FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal;
        UiText.Bind(text, TextBlock.TextProperty, key); return text;
    }
    private static void AddValue(StackPanel target, string label, string value)
    {
        var row = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };
        var caption = Label(label); caption.Opacity = .7; caption.Width = 96;
        caption.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(caption, Dock.Left); row.Children.Add(caption);
        row.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(value) ? Unknown() : value, TextWrapping = TextWrapping.Wrap, IsHitTestVisible = true });
        target.Children.Add(row);
    }
    private static StackPanel Section(StackPanel target, string? key, Button? action = null)
    {
        var content = new StackPanel();
        if (key != null)
        {
            var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            if (action != null)
            {
                action.Margin = new Thickness(12, 0, 0, 0); action.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(action, Dock.Right); heading.Children.Add(action);
            }
            var title = Label(key, 15, true); title.VerticalAlignment = VerticalAlignment.Center;
            heading.Children.Add(title); content.Children.Add(heading);
        }
        var border = new Border { Child = content, CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 12, 0, 0), BorderThickness = new Thickness(1) };
        border.SetResourceReference(Border.BorderBrushProperty, "MenuBorderBrush"); target.Children.Add(border); return content;
    }
    private static Button ActionButton(string key, Func<Task> action)
    {
        var button = new Button { MinHeight = 32, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 4, 8, 6), HorizontalAlignment = HorizontalAlignment.Left };
        button.SetResourceReference(StyleProperty, "ButtonPrimaryStyle"); UiText.Bind(button, ContentControl.ContentProperty, key);
        button.Click += async (_, e) => { e.Handled = true; await action(); }; return button;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Device.PropertyChanged -= DeviceChanged; timer.Stop();
        lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
    }
}
