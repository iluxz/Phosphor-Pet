#define AppName "Phosphor Pet"
#define AppVersion "1.0.0"
#define AppPublisher "iluxz"
#define ProjectDirectory ".."
#define PublishDirectory ProjectDirectory + "\publish\win-x64"

[Setup]
AppId={{E4EF9513-2240-4ED5-AD5B-3C519E5ECDA4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Phosphor Pet
DefaultGroupName=Phosphor Pet
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\PhosphorPet.exe
OutputBaseFilename=PhosphorPet-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile={#ProjectDirectory}\LICENSE

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startup"; Description: "Launch Phosphor Pet when I sign in"; GroupDescription: "Additional options:"; Flags: unchecked

[Files]
Source: "{#PublishDirectory}\PhosphorPet.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ProjectDirectory}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Phosphor Pet"; Filename: "{app}\PhosphorPet.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Phosphor Pet"; Filename: "{app}\PhosphorPet.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PhosphorPet"; ValueData: """{app}\PhosphorPet.exe"""; Tasks: startup; Flags: uninsdeletevalue

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -Command ""Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'PhosphorPet' -ErrorAction SilentlyContinue"""; Flags: runhidden

[Run]
Filename: "{app}\PhosphorPet.exe"; Description: "Launch Phosphor Pet"; Flags: postinstall nowait skipifsilent
