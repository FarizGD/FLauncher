# FLauncher

**FLauncher is a portable Minecraft launcher for Windows that brings Java Edition and Bedrock Edition management into one application.**

The active source build uses LeviLauncher's React/Go foundation, with FLauncher's Java Edition integration alongside the existing Bedrock services. It does not open a separate Prism Launcher or LeviLauncher window.

> FLauncher originated as a Prism Launcher fork. It is an independent project and is not endorsed by or affiliated with Prism Launcher, LeviLauncher, Mojang, or Microsoft.

## Download

**Latest published release: [FLauncher v2.0.0](https://github.com/FarizGD/FLauncher/releases/tag/v2.0.0)**

- [Download FLauncher-Portable-Win64-Shipping.exe](https://github.com/FarizGD/FLauncher/releases/download/v2.0.0/FLauncher-Portable-Win64-Shipping.exe) (Windows 64-bit, approximately 34 MB)
- [View release notes and SHA-256](https://github.com/FarizGD/FLauncher/releases/tag/v2.0.0)

The published release currently provides a standalone Windows executable. Download it from the release page and run it. Windows may display a security warning for downloaded applications; only continue if you trust the source and have reviewed the release details.

The release lists this SHA-256 checksum:

```
e57d0fa18375e6608ad1c7ca864fa4451b936449b2828b9005457be0a2c26bed
```

To verify the downloaded file in PowerShell:

```powershell
Get-FileHash .\FLauncher-Portable-Win64-Shipping.exe -Algorithm SHA256
```

Compare the result with the checksum above. The release links to [VirusTotal's report for this file](https://www.virustotal.com/gui/file/e57d0fa18375e6608ad1c7ca864fa4451b936449b2828b9005457be0a2c26bed).

## Features

### Java Edition

- Create and manage Vanilla, Fabric, Forge, and NeoForge instances.
- Install Minecraft versions and matching Java runtimes.
- Launch directly from FLauncher and view game output.
- Configure memory and runtime settings.
- Search Modrinth and install mods with required dependencies.
- Enable, disable, and remove local mods.
- Import worlds, resource packs, and shader packs.
- Back up instances and recover removed instances or content from trash.
- Manage Microsoft, Ely.by, and offline profiles.

**Shared mods:** Java instances use one shared mods directory. Changes to a mod can affect every Java instance, so check Minecraft-version and loader compatibility before enabling it.

**Current limitations:** Modpack import is not implemented. Historical Minecraft versions without the required client and asset-index metadata may not install. Real account sign-in and licensed gameplay still need interactive testing.

### Bedrock Edition

- Retains the existing Bedrock installation, download, version, mod, and content screens.
- Supports portable Bedrock instances and instance data isolation.
- Accepts valid Full or Trial licenses reported by Microsoft. This does not grant a license or bypass Microsoft entitlement checks.

Windows package support, Gaming Services, WebView2, and account sign-in may still be required. Trial availability and limitations are controlled by Microsoft.

## Portable storage

FLauncher is designed to keep launcher-managed data in the `Flauncher` folder beside the executable. Keep the executable and its adjacent data folder together when moving the launcher. For USB use, a writable NTFS drive is recommended. Custom Java executables outside the portable folder remain external dependencies.

Existing Prism data is not migrated automatically. Older FLauncher profiles may be copied into the portable data folder with the launcher closed; do not copy old mods junctions. Existing system Bedrock worlds must be imported into a portable instance.

## Build from source

### Requirements

- Windows x64
- Go 1.25.3 or newer (dependencies may select a newer Go toolchain)
- Node.js 22 or newer
- npm
- Windows WebView2 runtime

The repository includes the Bedrock native resources used by the integrated launcher.

### Build

Run these commands from the repository root in PowerShell:

```powershell
./scripts/build-flauncher.ps1
./scripts/verify-flauncher.ps1 -Executable artifacts/FLauncher-native/FLauncher.exe
```

The build script generates Wails bindings, runs Go tests and frontend checks, builds the production frontend, embeds it, and creates the executable and ZIP artifact. The source build outputs `artifacts/FLauncher-native/FLauncher.exe` and, by default, `artifacts/FLauncher-native-win-x64.zip`.

For additional build details, live-install checks, test coverage, account behavior, and diagnostics, see [NATIVE-LAUNCHER.md](NATIVE-LAUNCHER.md).

> `BULDING.md` documents the retained legacy C++/Qt fork. The integrated React/Go application described above is the active source build.

## Troubleshooting

- **The application does not open:** Check that WebView2 is installed and supported on your Windows system.
- **Bedrock installation fails:** Check `Flauncher/logs/bedrock-install.log`. The log records installation-stage diagnostics and is designed to exclude tokens and account credentials.
- **The launcher was moved to another drive:** Move the executable and its adjacent `Flauncher` folder together.
- **Java mods appear in multiple instances:** This is expected. Java instances share the same mods directory.
- **An account needs to sign in again:** Online account tokens are protected for the current Windows user and may require reauthentication under another user.

## Upstream projects and notices

FLauncher builds on work from [Prism Launcher](https://github.com/PrismLauncher/PrismLauncher), [LeviLauncher](https://github.com/LiteLDev/LeviLauncher), LiteDev (LiteLDev), and their contributors. Minecraft is owned by Mojang Studios and Microsoft. This project is not endorsed by those projects or companies.

Retain `COPYING` and `THIRD_PARTY_NOTICES` when redistributing. Review the repository's [LICENSE](LICENSE) and upstream notices before redistributing binaries or modified source.

## Contributing and support

Bug reports and feature requests are welcome through [GitHub Issues](https://github.com/FarizGD/FLauncher/issues). When reporting a problem, include the launcher version, Windows version, steps to reproduce, and relevant sanitized logs. Never post account tokens, passwords, or other credentials.
