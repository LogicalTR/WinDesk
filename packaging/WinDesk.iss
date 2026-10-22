#ifndef AppVersion
  #error AppVersion is required. Use build-release.ps1.
#endif
#ifndef AppSource
  #error AppSource is required. Use build-release.ps1.
#endif
#ifndef ReleaseOutput
  #error ReleaseOutput is required. Use build-release.ps1.
#endif

[Setup]
AppId={{C984BBCE-651F-4B55-ACB5-8531C17B7C38}
AppName=WinDesk
AppVersion={#AppVersion}
AppPublisher=LogicalTR
AppPublisherURL=https://github.com/LogicalTR/WinDesk
AppSupportURL=https://github.com/LogicalTR/WinDesk/issues
AppUpdatesURL=https://github.com/LogicalTR/WinDesk/releases/latest
DefaultDirName={localappdata}\Programs\WinDesk
DefaultGroupName=WinDesk
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22621
OutputDir={#ReleaseOutput}
OutputBaseFilename=WinDesk-Setup-{#AppVersion}-x64
SetupIconFile=..\src\WinDesk.App\Assets\WinDesk.ico
UninstallDisplayIcon={app}\WinDesk.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
AppMutex=Local\WinDesk.Desktop.v1
CloseApplications=no
RestartApplications=no
Uninstallable=yes
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstüne WinDesk kısayolu ekle"; GroupDescription: "Kısayollar:"
Name: "contextmenu"; Description: "Masaüstü sağ tık menüsüne WinDesk ekle (Daha fazla seçenek göster)"; GroupDescription: "Masaüstü:"
Name: "startup"; Description: "Windows oturumu açıldığında WinDesk'i başlat"; GroupDescription: "Başlangıç:"; Flags: unchecked

[Files]
Source: "{#AppSource}\WinDesk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "KULLANIM.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\WinDesk"; Filename: "{app}\WinDesk.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\WinDesk"; Filename: "{app}\WinDesk.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WinDesk"; ValueData: """{app}\WinDesk.exe"""; Tasks: startup
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk"; ValueType: string; ValueName: "MUIVerb"; ValueData: "WinDesk"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\WinDesk.exe"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk\shell\01-new"; ValueType: string; ValueName: ""; ValueData: "Yeni grup"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk\shell\01-new\command"; ValueType: string; ValueName: ""; ValueData: """{app}\WinDesk.exe"" --command new-group"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk\shell\02-toggle"; ValueType: string; ValueName: ""; ValueData: "Panelleri gizle / göster"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk\shell\02-toggle\command"; ValueType: string; ValueName: ""; ValueData: """{app}\WinDesk.exe"" --command toggle-panels"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk\shell\03-settings"; ValueType: string; ValueName: ""; ValueData: "Ayarlar"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\WinDesk\shell\03-settings\command"; ValueType: string; ValueName: ""; ValueData: """{app}\WinDesk.exe"" --command settings"; Tasks: contextmenu

[Run]
Filename: "{app}\WinDesk.exe"; Description: "WinDesk'i başlat"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Preserve a startup entry belonging to another WinDesk installation. }
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WinDesk', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\WinDesk.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WinDesk');
  end;
  { User layouts and saved shortcuts are deliberately outside the installation. }
end;
