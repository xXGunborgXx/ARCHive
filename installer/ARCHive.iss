#define MyAppName "ARCHive"
#define MyAppVersion "1.2.0-beta3"
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
OutputBaseFilename=ARCHive-Beta-1.2.0-beta3-7day-Setup
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

[Registry]
; "Copy with ARCHive" for any file
Root: HKCU; Subkey: "Software\Classes\*\shell\ARCHiveCopy"; ValueType: string; ValueName: ""; ValueData: "Copy with ARCHive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\ARCHiveCopy"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\Classes\*\shell\ARCHiveCopy\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --copy ""%1"""

; "Archive with ARCHive" for any file
Root: HKCU; Subkey: "Software\Classes\*\shell\ARCHiveArchive"; ValueType: string; ValueName: ""; ValueData: "Archive with ARCHive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\ARCHiveArchive"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\Classes\*\shell\ARCHiveArchive\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --archive ""%1"""

; "Copy with ARCHive" for folders
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ARCHiveCopy"; ValueType: string; ValueName: ""; ValueData: "Copy with ARCHive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ARCHiveCopy"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ARCHiveCopy\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --copy ""%1"""

; "Archive with ARCHive" for folders
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ARCHiveArchive"; ValueType: string; ValueName: ""; ValueData: "Archive with ARCHive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ARCHiveArchive"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ARCHiveArchive\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --archive ""%1"""

; "Extract with ARCHive" for .7z files
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\ARCHiveExtract"; ValueType: string; ValueName: ""; ValueData: "Extract with ARCHive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\ARCHiveExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\ARCHiveExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""

; "Extract with ARCHive" for .zip files
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\ARCHiveExtract"; ValueType: string; ValueName: ""; ValueData: "Extract with ARCHive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\ARCHiveExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\ARCHiveExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
