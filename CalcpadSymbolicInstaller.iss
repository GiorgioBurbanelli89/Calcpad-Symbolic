; Inno Setup Script para Calcpad-Symbolic
; Genera un instalador setup.exe

#define MyAppName "Calcpad-Symbolic"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Jorge Burbano"
#define MyAppURL "https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic"
#define MyAppExeName "Calcpad.exe"

[Setup]
AppId={{C1D2E3F4-5A6B-7C8D-9E0F-1A2B3C4D5E6F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Calcpad-Symbolic
DefaultGroupName=Calcpad-Symbolic
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=.\Installer
OutputBaseFilename=Calcpad-Symbolic-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "fileassoc"; Description: "Associate .cpd files with Calcpad-Symbolic"; GroupDescription: "File associations:"

[Files]
; Application files
Source: "Symbolic.Wpf\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "runtimes\linux*,runtimes\osx*,runtimes\linux-*,*.dylib"

; Examples — in Documents folder (like CalcpadCE original)
Source: "Examples\Symbolic\*"; DestDir: "{userdocs}\Calcpad-Symbolic\Examples\Symbolic"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "Examples\Finite Elements\*"; DestDir: "{userdocs}\Calcpad-Symbolic\Examples\Finite Elements"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "Examples\Mathematics\*"; DestDir: "{userdocs}\Calcpad-Symbolic\Examples\Mathematics"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.py,__pycache__,*.html,*.txt"
Source: "Examples\Mechanics\*"; DestDir: "{userdocs}\Calcpad-Symbolic\Examples\Mechanics"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.py,__pycache__,*.html"
Source: "Examples\Physics\*"; DestDir: "{userdocs}\Calcpad-Symbolic\Examples\Physics"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.py,__pycache__,*.html"
Source: "Examples\Demos\*"; DestDir: "{userdocs}\Calcpad-Symbolic\Examples\Demos"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.html"
Source: "Examples\Structural Design\*"; DestDir: "{userdocs}\Calcpad-Symbolic\Examples\Structural Design"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.html"

; Documentation
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{userdocs}\Calcpad-Symbolic"; Flags: uninsalwaysuninstall
Name: "{userdocs}\Calcpad-Symbolic\Examples"; Flags: uninsalwaysuninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Examples"; Filename: "{userdocs}\Calcpad-Symbolic\Examples"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\.cpd"; ValueType: string; ValueName: ""; ValueData: "CalcpadSymbolic.Document"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\CalcpadSymbolic.Document"; ValueType: string; ValueName: ""; ValueData: "Calcpad-Symbolic Document"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\CalcpadSymbolic.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\CalcpadSymbolic.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Calcpad-Symbolic"; Flags: nowait postinstall skipifsilent
