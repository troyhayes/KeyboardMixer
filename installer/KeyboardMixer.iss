; KeyboardMixer installer (Inno Setup 6). Build it with build-installer.ps1 in the repo root,
; which publishes the app and passes AppVersion and PublishDir.
;
; Installs per user (no admin prompt) to %LocalAppData%\KeyboardMixer\app. Settings and the log
; live one level up in %LocalAppData%\KeyboardMixer. Upgrades and uninstalls both keep
; settings.json, so a reinstall picks up your shortcuts again.
;
; Upgrading: run a newer setup over the old one. It closes the running app, replaces the files,
; keeps settings, and skips the options page (choices made since in the app are left alone).

#ifndef AppVersion
  #error Pass /DAppVersion=x.y.z (build-installer.ps1 does this)
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif

#define AppName "KeyboardMixer"
#define AppExe "KeyboardMixer.exe"

[Setup]
; Never change AppId: it's how Windows and future installers recognise an existing install.
AppId={{CA420A70-C70B-497D-9E02-968FA6AB760E}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Troy
AppPublisherURL=https://github.com/troyhayes/KeyboardMixer
AppSupportURL=https://github.com/troyhayes/KeyboardMixer/issues
AppUpdatesURL=https://github.com/troyhayes/KeyboardMixer/releases

PrivilegesRequired=lowest
DefaultDirName={localappdata}\{#AppName}\app
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
UsePreviousTasks=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041

OutputDir=..\artifacts
; Fixed name (no version) so the README can link to
; https://github.com/troyhayes/KeyboardMixer/releases/latest/download/KeyboardMixer-Setup.exe
OutputBaseFilename={#AppName}-Setup
SetupIconFile=..\src\KeyboardMixer\Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardSizePercent=100
Compression=lzma2/ultra64
SolidCompression=yes
; We close the app ourselves with "KeyboardMixer.exe --exit" (see [Code]).
CloseApplications=no

[Tasks]
Name: startup; Description: "Start KeyboardMixer when I sign in to Windows (runs in the tray)"
Name: minimized; Description: "Open in the tray instead of showing the settings window when I start it myself"; Flags: unchecked
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; Replace the whole app folder on upgrade so files from older versions don't linger.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "--show"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "--show"; Tasks: desktopicon

[Run]
; Apply the startup/minimized choices through the app itself, so it stays the one place that
; knows how they're stored (Run key and settings.json). Fresh installs only.
Filename: "{app}\{#AppExe}"; Parameters: "{code:SetupArguments}"; Flags: runhidden waituntilterminated; Check: not IsUpgrade
Filename: "{app}\{#AppExe}"; Parameters: "--show"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--exit"; RunOnceId: "ExitApp"; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall"; RunOnceId: "RemoveStartup"; Flags: runhidden waituntilterminated

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{CA420A70-C70B-497D-9E02-968FA6AB760E}_is1';

var
  WasInstalled: Boolean;

{ Checked once at startup: by the time [Run] entries execute, this install has registered itself. }
function InitializeSetup: Boolean;
begin
  WasInstalled := RegKeyExists(HKCU, UninstallKey);
  Result := True;
end;

function IsUpgrade: Boolean;
begin
  Result := WasInstalled;
end;

{ On an upgrade there's nothing to choose: go straight to installing. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpSelectTasks) and IsUpgrade;
end;

function SetupArguments(Param: String): String;
begin
  Result := '--setup';
  if WizardIsTaskSelected('startup') then Result := Result + ' --startup';
  if WizardIsTaskSelected('minimized') then Result := Result + ' --minimized';
end;

{ Ask a running copy (from a previous install) to quit before its files are replaced. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  ExistingExe: String;
begin
  Result := '';
  ExistingExe := ExpandConstant('{app}\{#AppExe}');
  if FileExists(ExistingExe) then
    Exec(ExistingExe, '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

{ Uninstall removes the app and the log, but keeps settings.json. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  DataDir := ExpandConstant('{localappdata}\{#AppName}');
  DelTree(ExpandConstant('{app}'), True, True, True);
  DeleteFile(DataDir + '\log.txt');
  DeleteFile(DataDir + '\settings.json.bad');
  RemoveDir(DataDir); { only succeeds if nothing (no settings) is left }
end;
