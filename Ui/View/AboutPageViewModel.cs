using System;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;
using _1RM.Service;
using _1RM.View.Utils;
using _1RM.View.Utils.MaskAndPop;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;

namespace _1RM.View
{
    public class AboutPageViewModel : PopupBase
    {
        private Timer? _checkUpdateTimer;
        private int _checkingUpdate;

        public AboutPageViewModel()
        {
            StartVersionCheckTimer();
        }

        public void StartVersionCheckTimer()
        {
            if (IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
            {
                XRemoteAutoUpdate.SetEnabled(false);
                _checkUpdateTimer?.Stop();
                return;
            }
            if (_checkUpdateTimer == null)
            {
                _checkUpdateTimer = new Timer()
                {
                    Interval = 1000 * 60 * 60,
                    AutoReset = true,
                };
                _checkUpdateTimer.Elapsed += (sender, args) =>
                {
                    if (IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
                    {
                        _checkUpdateTimer.Stop(); // Stop timer if checking is disabled
                        return;
                    }
                    CheckUpdateAsync();
                };
            }
            CheckUpdateAsync();
            _checkUpdateTimer.Stop();
            _checkUpdateTimer.Start();
        }

        ~AboutPageViewModel()
        {
            _checkUpdateTimer?.Stop();
            _checkUpdateTimer?.Dispose();
        }

        public string CurrentVersion => AppVersion.DisplayVersion;
        public string CurrentVersionDate => AppVersion.BuildDate.IndexOf("+", StringComparison.Ordinal) > 0 ? AppVersion.BuildDate.Substring(0, AppVersion.BuildDate.LastIndexOf("+", StringComparison.Ordinal)) : AppVersion.BuildDate;


        private string _newVersion = "";
        public string NewVersion
        {
            get => _newVersion;
            set => SetAndNotifyIfChanged(ref _newVersion, value);
        }

        private string _newVersionUrl = "";

        public string NewVersionUrl
        {
            get => _newVersionUrl;
            set => SetAndNotifyIfChanged(ref _newVersionUrl, value);
        }

        private bool _isBreakingNewVersion;
        public bool IsBreakingNewVersion
        {
            get => _isBreakingNewVersion;
            set => SetAndNotifyIfChanged(ref _isBreakingNewVersion, value);
        }

        private string _updateStatus = "";
        public string UpdateStatus { get => _updateStatus; set => SetAndNotifyIfChanged(ref _updateStatus, value); }
        private bool _updateReady;
        public bool UpdateReady
        {
            get => _updateReady;
            set { if (SetAndNotifyIfChanged(ref _updateReady, value)) RaisePropertyChanged(nameof(UpdateAction)); }
        }
        public string UpdateAction => UpdateReady
            ? UiText.Get("Update.Restart", "Restart and update")
            : UiText.Get("Update.ReleaseNotes", "View release notes");

        private void ReportUpdate(string status)
        {
            Execute.OnUIThread(() => UpdateStatus = status switch
            {
                "downloading" => UiText.Get("Update.Downloading", "Downloading update in the background"),
                "verifying" => UiText.Get("Update.Verifying", "Preparing update"),
                "ready" => UiText.Get("Update.Ready", "Update ready; takes effect next launch"),
                "failed" => UiText.Get("Update.Retry", "Update incomplete; will retry later"),
                _ => UiText.Get("Update.Latest", "You are up to date")
            });
        }

        public void CheckUpdateAsync()
        {
            if (IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion) return;
            _ = CheckForXRemoteUpdateAsync();
        }

        private async Task CheckForXRemoteUpdateAsync()
        {
            if (Interlocked.Exchange(ref _checkingUpdate, 1) != 0) return;
            try
            {
                var result = await XRemoteUpdateService.CheckAsync().ConfigureAwait(false);
                Execute.OnUIThread(() =>
                {
                    if (result.NewerPublished && !IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
                        OnNewVersionRelease(result);
                });
                if (result.NewerPublished && !IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion)
                {
                    var ready = await XRemoteAutoUpdate.PrepareAsync(result.NewerUrl, ReportUpdate).ConfigureAwait(false);
                    Execute.OnUIThread(() => UpdateReady = ready);
                    if (ready) ReportUpdate("ready");
                }
                else ReportUpdate("latest");
            }
            catch (OperationCanceledException)
            {
                if (!IoC.Get<ConfigurationService>().General.DoNotCheckNewVersion) ReportUpdate("failed");
            }
            catch (Exception exception) { SimpleLogHelper.Warning(exception); ReportUpdate("failed"); }
            finally { Interlocked.Exchange(ref _checkingUpdate, 0); }
        }

        private void OnNewVersionRelease(VersionHelper.CheckUpdateResult result)
        {
            this.NewVersion = result.NewerVersion;
            this.NewVersionUrl = result.NewerUrl;
            this.IsBreakingNewVersion = result.NewerHasBreakChange;
        }


        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand((o) =>
                {
                    this.RequestClose();
                });
            }
        }

        private RelayCommand? _cmdUpdate;
        public RelayCommand CmdUpdate
        {
            get
            {
                return _cmdUpdate ??= new RelayCommand((o) =>
                {
                    if (UpdateReady) App.Close(restartAfterUpdate: true);
                    else
                    {
                        HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
                    }
                });
            }
        }
    }
}
