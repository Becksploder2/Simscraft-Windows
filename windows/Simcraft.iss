#define MyAppName "Simcraft Studio"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "Wolfpack Studios"
#define MyAppExeName "SimcraftStudio.exe"

[Setup]
AppId={{B8B9EA5C-1E82-4A5D-9C18-90E7FBD49D5C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Simcraft Studio
DefaultGroupName=Simcraft Studio
OutputDir=output
OutputBaseFilename=Simcraft-Studio-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Simcraft Studio"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Simcraft Studio"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Simcraft Studio"; Flags: nowait postinstall skipifsilent
