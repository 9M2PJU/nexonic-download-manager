; Builds NexonicDownloadManager-Setup-<version>.exe (per-user install, no admin rights needed).
#define Version GetEnv("NDM_VERSION")
[Setup]
AppId={{6F1C2E4B-8B7D-4C55-9E2A-3D9B1F0A7C21}
AppName=Nexonic Download Manager
AppVersion={#Version}
AppPublisher=Nexonic
DefaultDirName={localappdata}\Programs\Nexonic Download Manager
DefaultGroupName=Nexonic Download Manager
PrivilegesRequired=lowest
OutputDir=..\build
OutputBaseFilename=NexonicDownloadManager-Setup-{#Version}
SetupIconFile=App\app.ico
UninstallDisplayIcon={app}\NexonicDownloadManager.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\build\win\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Nexonic Download Manager"; Filename: "{app}\NexonicDownloadManager.exe"
Name: "{userdesktop}\Nexonic Download Manager"; Filename: "{app}\NexonicDownloadManager.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"

[Run]
Filename: "{app}\NexonicDownloadManager.exe"; Description: "Start Nexonic Download Manager"; Flags: nowait postinstall skipifsilent
