; Phonexis installer (Inno Setup 6).
; Build with build\build_installer.bat, which publishes dist\Phonexis.exe first.
; Output: dist\Phonexis-Setup-<version>.exe
;
; Installs per user by default (no admin prompt, into %LOCALAPPDATA%\Programs\Phonexis);
; the first page lets the user switch to an install for all users.
; User data (%LOCALAPPDATA%\Phonexis: settings, keys, cache, sessions) is never removed.

#define AppName "Phonexis"
#define AppVersion "3.0.0"
#define AppPublisher "TX-Breaker"
#define AppUrl "https://github.com/TX-Breaker/Phonexis"
#define AppExe "Phonexis.exe"
#ifndef DistDir
  #define DistDir "..\..\dist"
#endif

[Setup]
AppId={{1A9A7C95-1B7A-4D93-A4D2-B6F2A1CE6D65}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
SetupIconFile=..\..\src\Phonexis\Resources\Images\phonexis.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\..\LICENSE
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir={#DistDir}
OutputBaseFilename=Phonexis-Setup-{#AppVersion}
ShowLanguageDialog=auto

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
en.AssocLists=Open .phonexis lists with Phonexis
it.AssocLists=Apri le liste .phonexis con Phonexis
de.AssocLists=.phonexis-Listen mit Phonexis öffnen
fr.AssocLists=Ouvrir les listes .phonexis avec Phonexis
es.AssocLists=Abrir las listas .phonexis con Phonexis
en.ListFile=Phonexis list
it.ListFile=Lista Phonexis
de.ListFile=Phonexis-Liste
fr.ListFile=Liste Phonexis
es.ListFile=Lista de Phonexis

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "assoc"; Description: "{cm:AssocLists}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#DistDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; .phonexis -> Phonexis.List (HKA = HKCU for a per-user install, HKLM for all users)
Root: HKA; Subkey: "Software\Classes\.phonexis"; ValueType: string; ValueName: ""; ValueData: "Phonexis.List"; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\.phonexis\OpenWithProgids"; ValueType: string; ValueName: "Phonexis.List"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\Phonexis.List"; ValueType: string; ValueName: ""; ValueData: "{cm:ListFile}"; Flags: uninsdeletekey; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\Phonexis.List\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\Phonexis.List\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assoc
; "Open with" entry for plain .txt lists, without taking over .txt
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".txt"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".phonexis"; ValueData: ""

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
