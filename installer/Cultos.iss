#define MyAppName "Cultos"
#define MyAppVersion "0.3.0"
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
OutputBaseFilename=Cultos-Setup-0.3.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Cultos.App\Assets\Cultos.ico
ChangesAssociations=yes
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


[Registry]
Root: HKCU; Subkey: "Software\Classes\.cultos"; ValueType: string; ValueData: "Cultos.Backup"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Cultos.Backup"; ValueType: string; ValueData: "Copia de Cultos"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Cultos.Backup\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Cultos.Backup\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.cultosperfil"; ValueType: string; ValueData: "Cultos.Profile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Cultos.Profile"; ValueType: string; ValueData: "Perfil de iglesia de Cultos"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Cultos.Profile\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Cultos.Profile\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
