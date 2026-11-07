; Inno Setup script for the Audora installer. Build it with scripts/package.sh.
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{8E3C1D5A-6B1F-4C7E-9F0A-2D6A5C1B7E41}
AppName=Audora
AppVersion={#AppVersion}
AppVerName=Audora {#AppVersion}
AppPublisher=abinaze
AppPublisherURL=https://github.com/abinaze/Audora
AppSupportURL=https://github.com/abinaze/Audora/issues
AppUpdatesURL=https://github.com/abinaze/Audora/releases
DefaultDirName={autopf}\Audora
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=Audora-Setup
SetupIconFile=..\assets\icons\audora.ico
UninstallDisplayIcon={app}\Audora.exe
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start Audora with Windows (opens quietly in the tray)"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Audora"; Filename: "{app}\Audora.exe"
Name: "{autodesktop}\Audora"; Filename: "{app}\Audora.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Audora"; ValueData: """{app}\Audora.exe"" --minimized"; Tasks: startup

[Run]
Filename: "{app}\Audora.exe"; Description: "Open Audora"; Flags: nowait postinstall skipifsilent

[Code]
procedure StopRunningAudora();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/im Audora.exe /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningAudora();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningAudora();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Audora');
    if not UninstallSilent then
    begin
      if MsgBox('Also delete your Audora settings, saved devices and log file?', mbConfirmation, MB_YESNO) = IDYES then
        DelTree(ExpandConstant('{localappdata}\Audora'), True, True, True);
    end;
  end;
end;
