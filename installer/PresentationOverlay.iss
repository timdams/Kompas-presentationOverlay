; Inno Setup-script voor Presentation Overlay.
; Bouwen (de release-workflow doet dit automatisch):
;   dotnet publish src -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
;   iscc /DAppVersion=1.0.0 /DSourceDir=..\publish installer\PresentationOverlay.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

#define AppName "Presentation Overlay"
#define AppExe "PresentationOverlay.exe"

[Setup]
AppId={{6B0F3C1E-5D2A-4F7B-9C1E-2A8D4E6F0B13}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Tim Dams
AppPublisherURL=https://github.com/timdams/Kompas-presentationOverlay
DefaultDirName={localappdata}\Programs\PresentationOverlay
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per gebruiker, zonder admin: zo blijft config.json naast de exe bewerkbaar.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=PresentationOverlay-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
; Draait de overlay nog, dan vraagt de installer om hem te sluiten.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Een bestaande config blijft staan bij een update.
Source: "{#SourceDir}\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Config bewerken"; Filename: "notepad.exe"; Parameters: """{app}\config.json"""
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
