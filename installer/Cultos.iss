#define MyAppName "Cultos"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Cultos"
#define MyAppExeName "Cultos.App.exe"

[Setup]
AppId={{D927B9E7-93E9-4684-836E-8F4F1FD16FB4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Cultos
DefaultGroupName=Cultos
OutputDir=..\artifacts\installer
OutputBaseFilename=Cultos-Setup-0.1.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Cultos"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Cultos"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Cultos"; Flags: nowait postinstall skipifsilent
