#define MyAppName "PCDS Queue TV Display"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Polytechnic College of Davao del Sur"
#define MyAppExeName "PCDSQueue.TVDisplay.exe"

[Setup]
AppId={{2A8CA138-7ED7-415C-A644-A7DCE942892B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PCDS Queue\TV Display
DefaultGroupName=PCDS Queue
OutputDir=..\release\Installers
OutputBaseFilename=PCDS Queue TV Display Setup
SetupIconFile=..\tv-display\PCDSQueue.TVDisplay\Assets\pcds-queue.ico
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
Source: "..\release\PCDSQueue.TVDisplay\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PCDS Queue TV Display"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PCDS Queue TV Display"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch PCDS Queue TV Display"; Flags: nowait postinstall skipifsilent
