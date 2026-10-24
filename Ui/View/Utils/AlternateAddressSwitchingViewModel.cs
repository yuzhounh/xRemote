using Stylet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using _1RM.Service;
using Shawn.Utils;

namespace _1RM.View.Utils
{
    public enum PingStatus { None, Pinging, Canceled, Failed, Success, TimedOut }

    public class PingTestItem : NotifyPropertyChangedBase
    {
        public PingTestItem(string name, string address) { Name = name; Address = address; }
        public string Name { get; }
        public string Address { get; }
        private PingStatus _status;
        public PingStatus Status
        {
            get => _status;
            set { SetAndNotifyIfChanged(ref _status, value); RaisePropertyChanged(nameof(StatusText)); }
        }
        private int _ping;
        public int Ping
        {
            get => _ping;
            set { SetAndNotifyIfChanged(ref _ping, value); RaisePropertyChanged(nameof(StatusText)); }
        }
        public string StatusText => Status switch
        {
            PingStatus.Pinging => UiText.Get("ConnectionCheck.Checking"),
            PingStatus.Success => UiText.Get("ConnectionCheck.Success", Ping),
            PingStatus.Failed => UiText.Get("ConnectionCheck.Failed"),
            PingStatus.TimedOut => UiText.Get("ConnectionCheck.Timeout"),
            PingStatus.Canceled => UiText.Get("ConnectionCheck.Canceled"),
            _ => ""
        };
    }

    public class AlternateAddressSwitchingViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly CancellationTokenSource _cts;
        private TaskCompletionSource<bool>? _retry;
        public AlternateAddressSwitchingViewModel(CancellationTokenSource cts) => _cts = cts;
        public bool IsCanceled { get; private set; }
        public bool IsCloseRequested { get; private set; }

        public override void RequestClose(bool? dialogResult = null)
        {
            // Native caption handling can defer the actual window close until after the probe ends.
            IsCloseRequested = true;
            base.RequestClose(dialogResult);
        }

        private string _title = "";
        public string Title { get => _title; set => SetAndNotifyIfChanged(ref _title, value); }
        private string _message = "";
        public string Message { get => _message; set => SetAndNotifyIfChanged(ref _message, value); }
        private int _eta;
        public int Eta
        {
            get => _eta;
            set { SetAndNotifyIfChanged(ref _eta, value); RaisePropertyChanged(nameof(RemainingText)); }
        }
        public string RemainingText => UiText.Get("ConnectionCheck.Remaining", Eta);
        private bool _isChecking = true;
        public bool IsChecking
        {
            get => _isChecking;
            private set
            {
                SetAndNotifyIfChanged(ref _isChecking, value);
                RaisePropertyChanged(nameof(HasFailed));
                RaisePropertyChanged(nameof(StatusIcon));
            }
        }
        public bool HasFailed => !IsChecking;
        public string StatusIcon => IsChecking ? "…" : "!";
        private List<PingTestItem> _pingTestItems = new();
        public List<PingTestItem> PingTestItems
        {
            get => _pingTestItems;
            set => SetAndNotifyIfChanged(ref _pingTestItems, value);
        }

        public void BeginAttempt(int seconds)
        {
            _retry = null;
            IsChecking = true;
            Eta = seconds;
            Message = UiText.Get("ConnectionCheck.Checking");
            foreach (var item in PingTestItems) { item.Ping = 0; item.Status = PingStatus.Pinging; }
        }

        public Task<bool> ShowFailure(bool timedOut)
        {
            _retry = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Message = UiText.Get(timedOut ? "ConnectionCheck.Timeout" : "ConnectionCheck.Failed");
            IsChecking = false;
            return _retry.Task;
        }

        private RelayCommand? _cmdRetry;
        public RelayCommand CmdRetry => _cmdRetry ??= new RelayCommand(_ =>
        {
            if (!IsChecking && _retry != null)
            {
                IsChecking = true;
                _retry.TrySetResult(true);
            }
        });
        private RelayCommand? _cmdCloseEnd;
        public RelayCommand CmdCloseEnd => _cmdCloseEnd ??= new RelayCommand(_ =>
        {
            if (IsCloseRequested) return;
            IsCanceled = true;
            _retry?.TrySetResult(false);
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            RequestClose();
        });
    }
}
