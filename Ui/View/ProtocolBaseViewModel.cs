using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.View.Launcher;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View
{
    public class ProtocolBaseViewModel : NotifyPropertyChangedBase
    {
        public DataSourceBase? DataSource => Server.DataSource;
        public string DataSourceName => DataSource?.DataSourceName ?? "";
        private string _dataSourceNameForLauncher = "";
        public string DataSourceNameForLauncher
        {
            get => _dataSourceNameForLauncher;
            set => SetAndNotifyIfChanged(ref _dataSourceNameForLauncher, value);
        }

        /// <summary>
        /// Order in Main window list view
        /// </summary>
        private int _customOrder = 0;
        public int CustomOrder
        {
            get => _customOrder;
            set => SetAndNotifyIfChanged(ref _customOrder, value);
        }

        public double KeywordMark = double.MinValue;

        #region Grouped
        public string GroupedOrder
        {
            get
            {
                var i = LocalityListViewService.GroupedOrderGet(dataSourceName: DataSourceName);
                var mark = IoC.Get<DataSourceService>().LocalDataSource == DataSource ? '!' : '#'; // ! for local, # for remote to make local first when i is same.
                return $"{i}_{mark}_{DataSource}";
            }
        }


        private bool? _groupedIsExpanded = null;
        public bool GroupedIsExpanded
        {
            set
            {
                if (IoC.TryGet<LocalityService>() != null
                    && SetAndNotifyIfChanged(ref _groupedIsExpanded, value))
                {
                    LocalityListViewService.GroupedIsExpandedSet(DataSourceName, value);
                }
            }
            get
            {
                var ret = LocalityListViewService.GroupedIsExpandedGet(DataSourceName);
                _groupedIsExpanded = LocalityListViewService.GroupedIsExpandedGet(DataSourceName);
                return ret;
            }
        }
        #endregion

        public bool IsEditable { get; private set; } = false;
        public bool IsViewable { get; private set; } = false;

        public string Id => Server.Id;

        public string DisplayName => Server.DisplayName;
        public string SubTitle => Server.SubTitle;
        public string ProtocolDisplayNameInShort => Server.ProtocolDisplayName;

        /// <summary>
        /// like: "#work #asd", display in launcher page.
        /// </summary>
        public string TagString { get; private set; } = "";

        public List<Tag> Tags { get; private set; } = new List<Tag>();

        public void ReLoadTags()
        {
            Tags = IoC.TryGet<GlobalData>()?.TagList.Where(x => _server.Tags.Contains(x.Name)).OrderBy(x => x.CustomOrder).ThenBy(x => x.Name).ToList() ?? new List<Tag>();
            RaisePropertyChanged(nameof(Tags));
        }

        private ProtocolBase _server;
        private string _connectionStatusText = "Shell.Checking";
        private string _connectionStatusColor = "#FBBF24";
        private string _connectionStatusHint = "Shell.CheckingHint";
        private object[] _connectionHintArguments = Array.Empty<object>();
        private bool? _connectionOnline;
        private string _remoteState = "Shell.Unknown";
        private bool _hasRemoteStatus;
        private XRemote.Control.ControlStatus? _remoteStatus;
        private bool _remoteInUse;
        public string ConnectionStatusText => UiText.Get(_connectionStatusText);
        public string ConnectionStatusColor => _connectionStatusColor;
        public string CardConnectionStatusColor => _connectionOnline == false ? "#F87171" : _connectionStatusColor;
        public string CardConnectionStatusText => _connectionOnline == false ? UiText.Get("Shell.Offline") : ConnectionStatusText;
        public string ConnectionStatusHint => UiText.Get(_connectionStatusHint, _connectionHintArguments) + "\n" +
            (_remoteStatus == null ? UiText.Get("Shell.UnknownDetail") :
                UiText.Get("Shell.SessionSummary", UiText.Get(_remoteState), _remoteStatus.ObservedAt.ToLocalTime().ToString("HH:mm:ss")) + "\n" +
                string.Join("\n", _remoteStatus.Sessions.Select(s => $"{s.Session.User} · {UiText.Get(s.Session.IsRemoteActive ? "Shell.Remote" : s.Session.State == 4 ? "Remote.Disconnected" : "Shell.LocalOther")} · {s.Session.Client}")));
        internal void SetRemoteSessionStatus(XRemote.Control.ControlStatus? status)
        {
            var active = status?.Sessions.Where(s => s.Session.IsRemoteActive).ToArray();
            _remoteStatus = status;
            _hasRemoteStatus = status != null;
            _remoteState = Server is not RDP ? "Shell.Online" : status == null ? "Shell.Unknown" : active!.Length > 0 ? "Shell.Remote" :
                status.Sessions.Any(s => s.Session.Protocol == 0 && s.Session.State == 0 && s.Session.Locked == false) ? "Shell.Local" : "Shell.Idle";
            _remoteInUse = _remoteState is "Shell.Remote" or "Shell.Local";
        }
        public System.Windows.Visibility ConnectionStatusVisibility => Server is ProtocolBaseWithAddressPort
            ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        internal void SetConnectionStatus(bool? online, string hint, params object[] hintArguments)
        {
            _connectionOnline = online;
            _connectionStatusText = online == true
                ? _hasRemoteStatus && _remoteInUse ? _remoteState : "Shell.Online"
                : online == false ? "Shell.Unreachable" : "Shell.Checking";
            _connectionStatusColor = online == true ? "#34D399" : online == false ? "#9CA3AF" : "#FBBF24";
            _connectionStatusHint = hint;
            _connectionHintArguments = hintArguments;
            RefreshLanguage();
        }

        internal void RefreshLanguage()
        {
            RaisePropertyChanged(nameof(ConnectionStatusText));
            RaisePropertyChanged(nameof(ConnectionStatusColor));
            RaisePropertyChanged(nameof(CardConnectionStatusColor));
            RaisePropertyChanged(nameof(CardConnectionStatusText));
            RaisePropertyChanged(nameof(ConnectionStatusHint));
            RaisePropertyChanged(nameof(ConnectionStatusVisibility));
        }

        public ProtocolBase Server
        {
            get => _server;
            set
            {
                if (_server != value)
                {
                    _server = value;
                    SetRemoteSessionStatus(null);
                    SetConnectionStatus(null, "Shell.CheckingHint");
                    _server.Tags = _server.Tags.Select(x => x.ToLower()).ToList();

                    if (ConverterNoteToVisibility.IsVisible(_server.Note))
                    {
                        Execute.OnUIThreadSync(() =>
                        {
                            HoverNoteDisplayControl = new NoteIcon(_server);
                        });
                    }
                    LastConnectTime = LocalityConnectRecorder.ConnectTimeGet(_server);
                    TagString = string.Join(" ", _server.Tags.Select(x => "#" + x));
                    RaisePropertyChanged(nameof(TagString));
                    ReLoadTags();
                    RaisePropertyChanged(nameof(Id));
                    RaisePropertyChanged(nameof(DisplayName));
                    RaisePropertyChanged(nameof(SubTitle));
                    RaisePropertyChanged(nameof(ProtocolDisplayNameInShort));
                    IsViewable = IsEditable = _server.DataSource?.IsWritable == true;
                    RaisePropertyChanged(nameof(DataSource));
                    RaisePropertyChanged(nameof(IsViewable));
                    RaisePropertyChanged(nameof(IsEditable));
                    LauncherMainTitleViewModel = null;
                    LauncherSubTitleViewModel = null;
                }
                RaisePropertyChanged();
            }
        }

        public ProtocolBaseViewModel(ProtocolBase psb)
        {
            Server = psb;
            _server = psb;
            
            // 初始化 CustomOrder
            CustomOrder = LocalityListViewService.Settings.ServerCustomOrder.GetValueOrDefault(psb.Id, 0);

            if (ConverterNoteToVisibility.IsVisible(Server.Note))
            {
                Execute.OnUIThreadSync(() =>
                {
                    HoverNoteDisplayControl = new NoteIcon(this.Server);
                });
            }
        }

        private ServerTitleViewModel? _launcherMainTitleViewModel;
        public ServerTitleViewModel? LauncherMainTitleViewModel
        {
            get => _launcherMainTitleViewModel ??= new ServerTitleViewModel(Server.DisplayName);
            private set => SetAndNotifyIfChanged(ref _launcherMainTitleViewModel, value);
        }


        private ServerTitleViewModel? _launcherSubTitleViewModel = null;
        public ServerTitleViewModel? LauncherSubTitleViewModel
        {
            get => _launcherSubTitleViewModel ??= new ServerTitleViewModel(Server.SubTitle);
            private set => SetAndNotifyIfChanged(ref _launcherSubTitleViewModel, value);
        }

        private NoteIcon? _hoverNoteDisplayControl = null;
        public NoteIcon? HoverNoteDisplayControl
        {
            get => _hoverNoteDisplayControl;
            set => SetAndNotifyIfChanged(ref _hoverNoteDisplayControl, value);
        }

        private bool _isSelected = false;
        /// <summary>
        /// is selected in list of MainWindow?
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotifyIfChanged(ref _isSelected, value);
        }


        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            private set => SetAndNotifyIfChanged(ref _isVisible, value);
        }

        public virtual void SetIsVisible(bool isVisible)
        {
            IsVisible = isVisible;
        }

        private DateTime _lastConnectTime = DateTime.MinValue;
        public DateTime LastConnectTime
        {
            get => _lastConnectTime;
            set => SetAndNotifyIfChanged(ref _lastConnectTime, value);
        }

        private List<ProtocolAction>? _actions;

        public List<ProtocolAction>? Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(ref _actions, value);
        }

        public void ClearActions()
        {
            Actions = null;
        }
        public void BuildActions()
        {
            Actions = this.GetActions();
        }


        #region CMD
        private RelayCommand? _cmdManageRemoteSessions;
        public RelayCommand CmdManageRemoteSessions => _cmdManageRemoteSessions ??= new RelayCommand(_ =>
        {
            if (Server is ProtocolBaseWithAddressPort endpoint)
                new RemoteSessionsWindow(endpoint.Address, Server.DisplayName).Show();
        });

        private RelayCommand? _cmdConnServer;
        public RelayCommand? CmdConnServer
        {
            get
            {
                return _cmdConnServer ??= new RelayCommand(o =>
                {
                    GlobalEventHelper.OnRequestServerConnect?.Invoke(Server, fromView: nameof(MainWindowView));
                });
            }
        }

        private RelayCommand? _cmdEditServer;
        public RelayCommand CmdEditServer
        {
            get
            {
                return _cmdEditServer ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(server: Server, showAnimation: true);
                });
            }
        }

        #endregion CMD
    }

    public class ProtocolBaseViewModelDummy : ProtocolBaseViewModel
    {
        public ProtocolBaseViewModelDummy(DataSourceBase source) : base(new Dummy() { DataSource = source })
        {
            base.SetIsVisible(false);
        }

        public override void SetIsVisible(bool isVisible)
        {
            base.SetIsVisible(false);
        }
    }
}
