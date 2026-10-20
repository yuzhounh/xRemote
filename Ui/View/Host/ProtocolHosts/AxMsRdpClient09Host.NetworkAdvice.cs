using System;
using System.Windows;
using System.Windows.Threading;
using AxMSTSCLib;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Stylet;

namespace _1RM.View.Host.ProtocolHosts;

public sealed partial class AxMsRdpClient09Host
{
    private readonly RdpNetworkAdvicePolicy _networkAdvice = new();
    private DispatcherTimer? _networkAdviceTimer;
    private (uint Width, uint Height) _suggestedResolution;

    private void InitializeNetworkAdvice()
    {
        _networkAdviceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _networkAdviceTimer.Tick += (_, _) =>
        {
            if (!_flagHasConnected || _rdpClient == null
                || Status != ProtocolHostStatus.Connected
                || _rdpSettings.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullAllScreens) return;
            var size = _networkAdvice.Recommend(DateTime.UtcNow, _rdpClient.DesktopWidth, _rdpClient.DesktopHeight);
            if (size == null) return;
            _suggestedResolution = size.Value;
            NetworkAdviceText.Text = UiText.Get("Remote.NetworkAdvice");
            NetworkAdviceApply.Content = UiText.Get("Remote.NetworkLower", size.Value.Width, size.Value.Height);
            NetworkAdviceBanner.Visibility = Visibility.Visible;
            _networkAdviceTimer.Stop();
        };
    }

    private void OnRdpNetworkStatusChanged(object sender, IMsTscAxEvents_OnNetworkStatusChangedEvent e)
    {
        Execute.OnUIThreadSync(() =>
        {
            if (!ReferenceEquals(sender, _rdpClient)) return;
            _networkAdvice.Observe(e.qualityLevel, DateTime.UtcNow);
            if (e.qualityLevel == 1) _networkAdviceTimer?.Start();
            else
            {
                _networkAdviceTimer?.Stop();
                NetworkAdviceBanner.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void StopNetworkAdvice()
    {
        _networkAdviceTimer?.Stop();
        _networkAdvice.Reset();
        NetworkAdviceBanner.Visibility = Visibility.Collapsed;
    }

    private void NetworkAdviceDismiss_OnClick(object sender, RoutedEventArgs e)
    {
        _networkAdviceTimer?.Stop();
        NetworkAdviceBanner.Visibility = Visibility.Collapsed;
    }

    private void NetworkAdviceApply_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_flagHasConnected || Status != ProtocolHostStatus.Connected || _rdpClient == null) return;
        try
        {
            var (width, height) = _suggestedResolution;
            if (width == 0 || height == 0) return;
            _rdpClient.UpdateSessionDisplaySettings(width, height, width, height, 0, 100, 100);
            // Apply to this session only, including a manual reconnect; preserve the saved profile.
            _rdpSettings.RdpWidth = (int)width;
            _rdpSettings.RdpHeight = (int)height;
            _rdpSettings.RdpWindowResizeMode = ERdpWindowResizeMode.Stretch;
            _rdpSettings.IsScaleFactorFollowSystem = false;
            _rdpSettings.ScaleFactorCustomValue = 100;
            _lastScaleFactor = 100;
            NetworkAdviceDismiss_OnClick(sender, e);
            _rdpClient.Focus();
        }
        catch (Exception ex)
        {
            SimpleLogHelper.Warning(ex);
            NetworkAdviceText.Text = UiText.Get("Remote.NetworkLowerFailed");
        }
    }
}
