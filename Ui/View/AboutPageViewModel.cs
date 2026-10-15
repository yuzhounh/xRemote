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
            }
            catch (Exception exception) { SimpleLogHelper.Warning(exception); }
            finally { Interlocked.Exchange(ref _checkingUpdate, 0); }
        }

        private void OnNewVersionRelease(VersionHelper.CheckUpdateResult result)
        {
            this.NewVersion = result.NewerVersion;
            this.NewVersionUrl = result.NewerUrl;
            this.IsBreakingNewVersion = result.NewerHasBreakChange;
            var v = IoC.Get<ConfigurationService>().Engagement.BreakingChangeAlertVersion;
            if (this.IsBreakingNewVersion
                && VersionHelper.Version.FromString(result.NewerVersion) > v)
            {
                Execute.OnUIThreadSync(() =>
                {
                    IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>());
                });
            }
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
                    if (IsBreakingNewVersion)
                    {
                        MaskLayerController.ShowProcessingRing();
                        IoC.Get<IWindowManager>().ShowDialog(IoC.Get<BreakingChangeUpdateViewModel>(), ownerViewModel: IoC.Get<MainWindowViewModel>());
                        MaskLayerController.HideMask();
                    }
                    else
                    {
                        HyperlinkHelper.OpenUriBySystem(NewVersionUrl);
                    }
                });
            }
        }
    }
}
