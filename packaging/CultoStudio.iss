#ifndef PackageDirectory
  #error PackageDirectory is required
#endif
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef InstallerOutput
  #error InstallerOutput is required
#endif

[Setup]
AppId={{3D48BFC5-6291-45BE-99F2-6CCF9AFCA731}
AppName=Culto Studio
AppVersion={#AppVersion}
AppPublisher=Culto Studio
DefaultDirName={localappdata}\Programs\CultoStudio
DefaultGroupName=Culto Studio
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#InstallerOutput}
OutputBaseFilename=CultoStudio-{#AppVersion}-Windows-x64-Setup
SetupIconFile=..\src\Culto.Desktop\Assets\CultoStudio.ico
UninstallDisplayIcon={app}\CultoStudio.exe
Compression=lzma2/fast
SolidCompression=no
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el &Escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "{#PackageDirectory}\App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Culto Studio"; Filename: "{app}\CultoStudio.exe"; WorkingDir: "{app}"
Name: "{group}\Desinstalar Culto Studio"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Culto Studio"; Filename: "{app}\CultoStudio.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\CultoStudio.exe"; Description: "Abrir Culto Studio"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Target, Profile: String;
begin
  Result := '';
  Target := AddBackslash(ExpandFileName(WizardDirValue));
  Profile := AddBackslash(ExpandConstant('{localappdata}\CultoStudio'));
  if (CompareText(Copy(Target, 1, Length(Profile)), Profile) = 0) or
     (Length(RemoveBackslashUnlessRoot(Target)) <= 3) then
  begin
    Result := 'Elige una carpeta de programa distinta de la carpeta de datos y de la raíz del disco.';
  end;
end;
