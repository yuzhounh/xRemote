using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using _1RM.Service.DataSource;
using _1RM.View.Settings.DataSource;
using _1RM.View.Settings.General;
using _1RM.View.Settings.Launcher;
using _1RM.View.Settings.ProtocolConfig;
using _1RM.View.Settings.Theme;
using _1RM.Service.DataSource.DAO;
using _1RM.View.Settings.CredentialVault;

namespace _1RM.View.Settings
{
    public partial class SettingsPageViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly DataSourceService _dataSourceService;
        private ConfigurationService _configurationService => IoC.Get<ConfigurationService>();
        private readonly GlobalData _appData;


        public SettingsPageViewModel(DataSourceService dataSourceService, GlobalData appData)
        {
            _dataSourceService = dataSourceService;
            _appData = appData;
        }

        protected override void OnViewLoaded()
        {
            ShowPage(CurrentPage);
        }


        private INotifyPropertyChanged? _selectedViewModel;
        public INotifyPropertyChanged SelectedViewModel
        {
            get => _selectedViewModel ??= GeneralSettingViewModel;
            set => SetAndNotifyIfChanged(ref _selectedViewModel, value);
        }

        private readonly DataAndCredentialsViewModel _dataAndCredentials = new();

        public void ShowPage(EnumMainWindowPage page)
        {
            page = page switch
            {
                EnumMainWindowPage.SettingsLauncher => EnumMainWindowPage.SettingsGeneral,
                EnumMainWindowPage.SettingsCredentialVault => EnumMainWindowPage.SettingsData,
                _ => page
            };
            switch (page)
            {
                case EnumMainWindowPage.SettingsGeneral: SelectedViewModel = GeneralSettingViewModel; break;
                case EnumMainWindowPage.SettingsData: SelectedViewModel = _dataAndCredentials; break;
                case EnumMainWindowPage.SettingsTheme: SelectedViewModel = IoC.Get<ThemeSettingViewModel>(); break;
                case EnumMainWindowPage.SettingsRunners: SelectedViewModel = IoC.Get<ProtocolRunnerSettingsPageViewModel>(); break;
                default: return;
            }
            CurrentPage = page;
        }

        private EnumMainWindowPage _currentPage = EnumMainWindowPage.SettingsGeneral;
        public EnumMainWindowPage CurrentPage
        {
            get => _currentPage;
            private set => SetAndNotifyIfChanged(ref _currentPage, value);
        }

        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set => SetAndNotifyIfChanged(ref _progressBarVisibility, value);
        }

        public GeneralSettingViewModel GeneralSettingViewModel => IoC.Get<GeneralSettingViewModel>();
        public LauncherSettingViewModel LauncherSettingViewModel => IoC.Get<LauncherSettingViewModel>();


        public bool TabHeaderShowIconButton
		{
            get => _configurationService.General.TabHeaderShowIconButton;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.TabHeaderShowIconButton, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public bool TabHeaderShowCloseButton
        {
            get => _configurationService.General.TabHeaderShowCloseButton;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.TabHeaderShowCloseButton, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public bool TabHeaderShowReConnectButton
        {
            get => _configurationService.General.TabHeaderShowReConnectButton;
            set
            {
                if (SetAndNotifyIfChanged(ref _configurationService.General.TabHeaderShowReConnectButton, value))
                {
                    _configurationService.Save();
                }
            }
        }

        public event Action? CloseRequested;
        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose => _cmdClose ??= new RelayCommand(_ => CloseRequested?.Invoke());

        public bool TryFinishEditing()
        {
            var result = _dataSourceService.LocalDataSource?.Database_SelfCheck() ?? DatabaseStatus.New(EnumDatabaseStatus.AccessDenied);
            if (result.Status != EnumDatabaseStatus.OK)
            {
                ShowPage(EnumMainWindowPage.SettingsData);
                MessageBoxHelper.ErrorAlert(result.GetErrorMessage);
                return false;
            }
            var launcher = _configurationService.Launcher;
            if (IoC.TryGet<LauncherWindowViewModel>() is { } launcherWindow &&
                launcher.LauncherEnabled != launcherWindow.SetHotKey(launcher.LauncherEnabled, launcher.HotKeyModifiers, launcher.HotKeyKey))
            {
                ShowPage(EnumMainWindowPage.SettingsGeneral);
                MessageBoxHelper.ErrorAlert(IoC.Translate("hotkey_registered_fail"));
                return false;
            }
            _configurationService.Save();
            IoC.Get<ProtocolConfigurationService>().Save();
            return true;
        }

        private RelayCommand? _cmdShowPage;
        public RelayCommand CmdShowPage
        {
            get
            {
                return _cmdShowPage ??= new RelayCommand((o) =>
                {
                    if (o is EnumMainWindowPage page)
                    {
                        ShowPage(page);
                    }
                });
            }
        }
    }
}
