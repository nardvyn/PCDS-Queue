#define MyAppName "PCDS Queue Kiosk"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Polytechnic College of Davao del Sur"
#define MyAppExeName "PCDSQueue.Kiosk.exe"

[Setup]
AppId={{7F936215-A4A0-4BEC-9C09-E3B25BC6E82F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PCDS Queue\Kiosk
DefaultGroupName=PCDS Queue
OutputDir=..\release\Installers
OutputBaseFilename=PCDS Queue Kiosk Setup
SetupIconFile=..\kiosk\PCDSQueue.Kiosk\PCDSQueue.Kiosk\Assets\pcds-queue.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\release\PCDSQueue.Kiosk\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PCDS Queue Kiosk"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PCDS Queue Kiosk"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch PCDS Queue Kiosk"; Flags: nowait postinstall skipifsilent
