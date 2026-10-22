#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef StageDir
  #error StageDir is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif
[Setup]
AppId={{6613E914-CC1E-4C25-A952-992BDB8500F3}
AppName=AirspyPanel
AppVersion={#AppVersion}
AppPublisher=PriscillaOvO
AppPublisherURL=https://github.com/PriscillaOvO/AirspyPanel
AppSupportURL=https://github.com/PriscillaOvO/AirspyPanel/issues
DefaultDirName={localappdata}\Programs\AirspyPanel
DefaultGroupName=AirspyPanel
PrivilegesRequired=lowest
MinVersion=10.0
AppMutex=Local\AirspyPanel
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\AirspyPanel.exe
SetupIconFile=..\src\Airplane.ico
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir={#ReleaseDir}
OutputBaseFilename=AirspyPanel-v{#AppVersion}-setup
DisableProgramGroupPage=yes
DisableWelcomePage=no
VersionInfoVersion={#AppVersion}.0
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
[CustomMessages]
en.Framework=.NET Framework 4.8 is required. Open Microsoft's official download page?
zh.Framework=需要 .NET Framework 4.8。是否打开微软官方下载页？
ja.Framework=.NET Framework 4.8 が必要です。Microsoft の公式ページを開きますか？
fr.Framework=.NET Framework 4.8 est requis. Ouvrir la page officielle Microsoft ?
de.Framework=.NET Framework 4.8 wird benötigt. Offizielle Microsoft-Seite öffnen?
ru.Framework=Требуется .NET Framework 4.8. Открыть официальную страницу Microsoft?
en.Purge=Also delete AirspyPanel personal settings? Default: keep. VRS and decoder files will not be removed.
zh.Purge=同时删除 AirspyPanel 个人设置？默认保留，不删除 VRS 或解码器。
ja.Purge=AirspyPanel の設定も削除しますか？既定では保持します。VRS とデコーダーは削除しません。
fr.Purge=Supprimer les réglages AirspyPanel ? Par défaut ils sont conservés. VRS et le décodeur restent intacts.
de.Purge=AirspyPanel-Einstellungen löschen? Standard: behalten. VRS und Decoder bleiben erhalten.
ru.Purge=Удалить настройки AirspyPanel? По умолчанию сохраняются. VRS и декодер не удаляются.
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
[Files]
Source: "{#StageDir}\AirspyPanel.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\AirspyPanel.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\AirspyPanel"; Filename: "{app}\AirspyPanel.exe"
Name: "{autodesktop}\AirspyPanel"; Filename: "{app}\AirspyPanel.exe"; Tasks: desktopicon
[Code]
function InitializeSetup(): Boolean;
var ReleaseValue: Cardinal; ResultCode: Integer;
begin
  Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) and (ReleaseValue >= 528040);
  if not Result then begin
    if not WizardSilent then
      if MsgBox(CustomMessage('Framework'), mbInformation, MB_YESNO) = IDYES then
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox(CustomMessage('Purge'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then begin
      DeleteFile(ExpandConstant('{localappdata}\AirspyPanel\AirspyPanel.settings.xml'));
      DeleteFile(ExpandConstant('{localappdata}\AirspyPanel\AirspyPanel-error.txt'));
      RemoveDir(ExpandConstant('{localappdata}\AirspyPanel'));
    end;
end;
