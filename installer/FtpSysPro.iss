; Script generated for FTPSys Pro Backup Solution
; Requires Inno Setup 6 (free from jrsoftware.org)

#define MyAppName "FTPSys Pro"
#define MyAppVersion "1.2.2"
#define MyAppPublisher "0xAndyNew"
#define MyAppURL "https://github.com/0xAndyNew/FTPSys-Pro"
#define MyAppExeName "FtpSysPro.Manager.exe"
#define MyServiceExeName "FtpSysPro.Service.exe"

[Setup]
AppId={{D37E74B0-4A92-4C15-998C-8FE8F4F19E92}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\dist
OutputBaseFilename=FTPSys_Pro_Setup_v{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
Name: "{commonappdata}\FtpSysPro"; Permissions: users-modify
Name: "{commonappdata}\FtpSysPro\logs"; Permissions: users-modify

[Files]
; Core, Service, and Manager output binaries from Release build
Source: "..\src\FtpSysPro.Service\bin\Release\net48\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\FtpSysPro.Manager\bin\Release\net48\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Gỡ cài đặt {#MyAppName}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Install Windows Service using .NET InstallUtil / Service --install
Filename: "{app}\{#MyServiceExeName}"; Parameters: "--install"; Flags: runhidden; StatusMsg: "Đang cài đặt Windows Service..."
; Start Windows Service
Filename: "sc.exe"; Parameters: "start FtpSysProService"; Flags: runhidden; StatusMsg: "Đang khởi động Windows Service..."
; Run Manager at end of setup with elevated admin privileges using ShellExecuteEx (shellexec prevents code 740 elevation error)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Parameters: "{code:GetLaunchParameters}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Stop and uninstall Manager auto-start task if present
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""FTPSys Pro Manager Startup"" /F"; Flags: runhidden; RunOnceId: "DelStartupTask"
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""FtpSysPro Manager Startup"" /F"; Flags: runhidden; RunOnceId: "DelStartupTaskOld"
; Terminate running Manager
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillManager"
; Stop Windows Service
Filename: "sc.exe"; Parameters: "stop FtpSysProService"; Flags: runhidden; RunOnceId: "StopService"
; Uninstall Windows Service
Filename: "{app}\{#MyServiceExeName}"; Parameters: "--uninstall"; Flags: runhidden; RunOnceId: "UninstallService"

[Code]
var
  RunAsAdminCheckbox: TNewCheckBox;

procedure RunListClick(Sender: TObject);
begin
  if (WizardForm.RunList <> nil) and (WizardForm.RunList.Items.Count > 0) and (RunAsAdminCheckbox <> nil) then
  begin
    RunAsAdminCheckbox.Enabled := WizardForm.RunList.Checked[0];
  end;
end;

procedure InitializeWizard();
begin
  RunAsAdminCheckbox := TNewCheckBox.Create(WizardForm);
  RunAsAdminCheckbox.Parent := WizardForm.FinishedPage;
  RunAsAdminCheckbox.Caption := 'Chạy với quyền Quản trị viên (Run as administrator)';
  RunAsAdminCheckbox.Checked := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    if (WizardForm.RunList <> nil) and (RunAsAdminCheckbox <> nil) then
    begin
      // Inno Setup default RunList has Height=221 in modern style, which pushes controls offscreen.
      // Explicitly resize RunList to fit only its single postinstall item (~26px).
      WizardForm.RunList.Height := ScaleY(26);

      RunAsAdminCheckbox.Left := WizardForm.RunList.Left;
      RunAsAdminCheckbox.Top := WizardForm.RunList.Top + WizardForm.RunList.Height + ScaleY(6);
      RunAsAdminCheckbox.Width := WizardForm.RunList.Width;
      RunAsAdminCheckbox.Height := ScaleY(24);
      RunAsAdminCheckbox.Font := WizardForm.FinishedLabel.Font;
      RunAsAdminCheckbox.BringToFront;

      WizardForm.RunList.OnClickCheck := @RunListClick;
      WizardForm.RunList.OnClick := @RunListClick;
      if WizardForm.RunList.Items.Count > 0 then
        RunAsAdminCheckbox.Enabled := WizardForm.RunList.Checked[0];
    end;
  end;
end;

function GetLaunchParameters(Param: String): String;
begin
  if (WizardForm.RunList <> nil) and (WizardForm.RunList.Items.Count > 0) and
     (WizardForm.RunList.Checked[0]) and (RunAsAdminCheckbox <> nil) and (RunAsAdminCheckbox.Checked) then
    Result := '--admin'
  else
    Result := '';
end;

// Check .NET Framework 4.8 presence
function IsDotNet48Detected(): Boolean;
var
  Success: Boolean;
  Install: Cardinal;
  Release: Cardinal;
begin
  Success := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Install', Install);
  if Success and (Install = 1) then
  begin
    Success := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
    // Release >= 528040 means .NET 4.8 or later
    Result := Success and (Release >= 528040);
  end
  else
    Result := False;
end;

function InitializeSetup(): Boolean;
begin
  if not IsDotNet48Detected() then
  begin
    MsgBox('Máy tính của bạn chưa có Microsoft .NET Framework 4.8 trở lên.' + #13#10 +
           'Vui lòng tải và cài đặt .NET Framework 4.8 từ trang chủ Microsoft trước khi cài đặt FtpSysPro.' + #13#10 + #13#10 +
           'Microsoft .NET Framework 4.8 is required. Please install it before running setup.', mbCriticalError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  // Stop existing Windows Service if running so binaries can be overwritten cleanly
  Exec('sc.exe', 'stop FtpSysProService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // Terminate any running Manager instance so binaries and mutex are freed
  Exec('taskkill.exe', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Bạn có muốn xoá toàn bộ dữ liệu cấu hình và lịch sử sao lưu trong C:\ProgramData\FtpSysPro không?' + #13#10 +
              'Do you want to delete all configuration and backup history data in C:\ProgramData\FtpSysPro?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{commonappdata}\FtpSysPro'), True, True, True);
    end;
  end;
end;
