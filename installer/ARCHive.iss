#define MyAppName "ARCHive"
#define MyAppVersion "1.1.0-beta2"
#define MyAppPublisher "ARCHive Project"
#define MyAppExeName "ARCHive.exe"

[Setup]
AppId={{6B48A15A-E313-45DD-B8B9-2C86EA51DA57}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName=ARCHive Beta {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=ARCHive-Beta-1.1.0-beta2-7day-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dark includetitlebar hidebevels
SetupIconFile=..\images\ARCHive-icon.ico
WizardImageFile=ARCHive-wizard.png
WizardImageBackColor=#0B0E13
WizardSmallImageFile=ARCHive-wizard-small.png
WizardSmallImageBackColor=#151A22
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes
InfoBeforeFile=..\INSTALLATION_DISCLOSURE.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\INSTALLATION_DISCLOSURE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LOGGING_AND_PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\USER_INSTRUCTIONS.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\beta\README.md"; DestDir: "{app}"; DestName: "BETA_README.md"; Flags: ignoreversion
Source: "..\beta\INSTRUCTIONS_AND_DISCLOSURES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
