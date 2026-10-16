using System.Windows;
using _1RM.View.Utils;

namespace _1RM.View;

public partial class MainWindowView
{
    private DeviceDetailsPane? detailsPane;
    private Window? detailsWindow;

    public void ShowDeviceDetails(ProtocolBaseViewModel device)
    {
        detailsWindow?.Close();
        CloseDeviceDetailsPanel();
        if (ActualWidth >= 1760)
        {
            detailsPane = new DeviceDetailsPane(device, true);
            detailsPane.CloseRequested += CloseDeviceDetailsPanel;
            DeviceDetailsHost.Child = detailsPane;
            DeviceDetailsHost.Visibility = Visibility.Visible;
        }
        else
        {
            var pane = new DeviceDetailsPane(device, false);
            var window = new DialogWindowBase
            {
                Owner = this, Title = _1RM.Service.UiText.Get("Device.Details") + " · " + device.DisplayName,
                Width = 1080, Height = 800, MinWidth = 560, MinHeight = 400, MaxWidth = 1440,
                SizeToContent = SizeToContent.Manual, ResizeMode = ResizeMode.CanResize,
                DialogContent = pane
            };
            _ = new WindowWorkArea(window);
            pane.CloseRequested += window.Close;
            window.Closed += (_, _) => { pane.Dispose(); if (detailsWindow == window) detailsWindow = null; };
            detailsWindow = window; window.Show();
        }
    }
    private void CloseDeviceDetailsPanel()
    {
        detailsPane?.Dispose(); detailsPane = null;
        DeviceDetailsHost.Child = null; DeviceDetailsHost.Visibility = Visibility.Collapsed;
    }
    private void FitDeviceDetails()
    {
        if (detailsPane != null && ActualWidth < 1700) ShowDeviceDetails(detailsPane.Device);
    }
}
