; Build the integrated application first: powershell -File scripts/build-flauncher.ps1
; Then compile with Inno Setup 6: ISCC.exe installer-script.iss
#ifndef BundleRoot
  #define BundleRoot SourcePath + "artifacts\FLauncher-native"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#if !FileExists(BundleRoot + "\FLauncher.exe")
  #error FLauncher.exe is missing. Run scripts/build-flauncher.ps1 first.
#endif

[Setup]
AppId={{2FD03D30-CFBB-473F-9C3D-9A1729A92CCF}
AppName=FLauncher
AppVersion={#AppVersion}
AppPublisher=FarizGD
DefaultDirName={localappdata}\Programs\FLauncher
DefaultGroupName=FLauncher
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#SourcePath}artifacts
OutputBaseFilename=FLauncher-Install-Windows-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\FLauncher.exe
CloseApplications=yes

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#BundleRoot}\FLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BundleRoot}\COPYING"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BundleRoot}\THIRD_PARTY_NOTICES"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BundleRoot}\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\FLauncher"; Filename: "{app}\FLauncher.exe"
Name: "{autodesktop}\FLauncher"; Filename: "{app}\FLauncher.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FLauncher.exe"; Description: "Launch FLauncher"; Flags: nowait postinstall skipifsilent
