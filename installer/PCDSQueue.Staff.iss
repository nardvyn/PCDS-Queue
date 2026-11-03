#define MyAppName "PCDS Queue Staff"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Polytechnic College of Davao del Sur"
#define MyAppExeName "PCDSQueue.Staff.exe"

[Setup]
AppId={{513C9CD1-595C-4820-8A72-C7166708F5A0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PCDS Queue\Staff
DefaultGroupName=PCDS Queue
OutputDir=..\release\Installers
OutputBaseFilename=PCDS Queue Staff Setup
SetupIconFile=..\desktop\PCDSQueue.Staff\PCDSQueue.Staff\Assets\pcds-queue.ico
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
Source: "..\release\PCDSQueue.Staff\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PCDS Queue Staff"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\PCDS Queue Staff"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch PCDS Queue Staff"; Flags: nowait postinstall skipifsilent
