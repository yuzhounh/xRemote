using System;
using System.Threading;
using System.Windows;
using _1RM.Service;
using Shawn.Utils;
using Stylet;
using XRemote.Control;

namespace _1RM.View.Host.ProtocolHosts;

public sealed partial class AxMsRdpClient09Host
{
    private CancellationTokenSource? _remoteAccessCancellation;
    private string? _remoteAccessDenied;

    private void StopRemoteAccessMonitor() => Interlocked.Exchange(ref _remoteAccessCancellation, null)?.Cancel();

    public override async void Conn()
    {
        if (_rdpClient == null || Status is ProtocolHostStatus.Connected or ProtocolHostStatus.Connecting
            || _remoteAccessCancellation != null) return;
        _remoteAccessDenied = null; // Only an explicit connection/reconnection attempt clears the stop reason.
        var client = _rdpClient;
        var cancellation = new CancellationTokenSource();
        _remoteAccessCancellation = cancellation;
        var token = cancellation.Token;
        try
        {
            await RemoteAccessMonitor.Run(
                ct => DeviceControlClient.RemoteAccess(_rdpSettings.Address, ct),
                _ => { },
                () => Execute.OnUIThreadSync(() =>
                {
                    if (!token.IsCancellationRequested && _rdpClient == client) ConnectRdpCore();
                }),
                decision => Execute.OnUIThreadSync(() =>
                {
                    if (token.IsCancellationRequested || _rdpClient != client) return;
                    _remoteAccessDenied = UiText.Get("Remote.GuardStopped", string.Join(", ", decision.Active));
                    _flagHasConnected = false; _flagHasEverConnected = false; _retryCount = 0;
                    client.AdvancedSettings5.EnableAutoReconnect = false;
                    // Disposing this ActiveX transport also ends its drive/clipboard redirection.
                    // Do not log off Windows or operate on other users' RDP/third-party sessions.
                    ParentWindowResize_StopWatch();
                    RdpClientDispose();
                    if (_rdpClient != null) _remoteAccessDenied = UiText.Get("Remote.GuardStopUnconfirmed");
                    ShowRemoteAccessDenied();
                }), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { SimpleLogHelper.Error(ex); }
        finally
        {
            Interlocked.CompareExchange(ref _remoteAccessCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private void ShowRemoteAccessDenied()
    {
        Status = ProtocolHostStatus.Disconnected;
        RdpHost.Visibility = Visibility.Collapsed;
        GridLoading.Visibility = Visibility.Collapsed;
        GridMessageBox.Visibility = Visibility.Visible;
        TbMessageTitle.Visibility = Visibility.Collapsed;
        TbMessage.Visibility = Visibility.Visible;
        TbMessage.Text = _remoteAccessDenied ?? "";
        BtnReconn.Visibility = Visibility.Visible;
        ParentWindowSetToWindow();
    }
}
