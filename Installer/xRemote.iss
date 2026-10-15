#ifndef ReleaseVersion
  #error ReleaseVersion must be supplied by Build-WindowsInstaller.ps1
#endif
#ifndef FileVersion
  #error FileVersion must be supplied by Build-WindowsInstaller.ps1
#endif
#ifndef PackageSource
  #error PackageSource must be supplied by Build-WindowsInstaller.ps1
#endif
#ifndef ModeMarker
  #error ModeMarker must be supplied by Build-WindowsInstaller.ps1
#endif

[Setup]
AppId={{D5A704E3-50A9-4E37-9A72-87BC0B229F06}
AppName=xRemote
AppVersion={#ReleaseVersion}
AppPublisher=yuzhounh
AppPublisherURL=https://github.com/yuzhounh/xRemote
AppUpdatesURL=https://github.com/yuzhounh/xRemote/releases
VersionInfoVersion={#FileVersion}
DefaultDirName={localappdata}\Programs\xRemote
DefaultGroupName=xRemote
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=xRemote-v{#ReleaseVersion}-x64-setup
SetupIconFile=..\Ui\LOGO.ico
UninstallDisplayIcon={app}\xRemote.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#PackageSource}\*"; DestDir: "{app}"; Excludes: "FORCE_INTO_PORTABLE_MODE"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ModeMarker}"; DestDir: "{app}"; Flags: ignoreversion; Check: UseAppDataMode

[Icons]
Name: "{group}\xRemote"; Filename: "{app}\xRemote.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\xRemote"; Filename: "{app}\xRemote.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\xRemote.exe"; WorkingDir: "{app}"; Description: "启动 xRemote"; Flags: nowait postinstall skipifsilent

[Code]
function UseAppDataMode: Boolean;
begin
  { Preserve portable profiles if the user deliberately installs over one. }
  Result := not FileExists(ExpandConstant('{app}\FORCE_INTO_PORTABLE_MODE')) and
    not FileExists(ExpandConstant('{app}\1Remote.json'));
end;
