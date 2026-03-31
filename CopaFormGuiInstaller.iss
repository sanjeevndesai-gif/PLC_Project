; CopaFormGui Inno Setup Script
; Save this as CopaFormGuiInstaller.iss and open with Inno Setup Compiler

[Setup]
AppName=CopaFormGui
AppVersion=1.0
DefaultDirName={pf}\CopaFormGui
DefaultGroupName=CopaFormGui
UninstallDisplayIcon={app}\CopaFormGui.exe
OutputDir=artifacts
OutputBaseFilename=CopaFormGui-Setup
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "artifacts\CopaFormGui-Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CopaFormGui"; Filename: "{app}\CopaFormGui.exe"
Name: "{userdesktop}\CopaFormGui"; Filename: "{app}\CopaFormGui.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"

[Run]
Filename: "{app}\CopaFormGui.exe"; Description: "Launch CopaFormGui"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    MsgBox('If you are reinstalling, the previous version will be overwritten.', mbInformation, MB_OK);
end;
