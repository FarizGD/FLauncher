# FLauncher desktop

**Superseded:** this is the earlier Avalonia experiment. The active integrated React/Go app is documented in [`NATIVE-LAUNCHER.md`](../NATIVE-LAUNCHER.md) and built with `scripts/build-flauncher.ps1`.

The Windows build includes the Avalonia interface **and both functional launchers**. Users do not need to download Prism Launcher or LeviLauncher separately or configure executable paths.

## Use the complete build

Extract `artifacts/FLauncher-win-x64.zip` and run `FLauncher.Desktop.exe`. Keep the extracted directory together: `backends/java` contains the complete Java engine and its native dependencies; `backends/bedrock` contains LeviLauncher. The .NET runtime is included too.

- **Java:** add instances, sign in, install modpacks and manage mods with the included Prism manager. Instances appear in the Avalonia library and can be launched there.
- **Bedrock:** install and manage Release/Preview versions and mods with the included LeviLauncher manager. Its instances appear in the Avalonia library and launch through the bundled engine. Registered Microsoft Store editions can also be launched directly.
- **Java runtimes:** download, verify and assign Temurin 8/17/21/25 from the Avalonia runtime page. Downloads use SHA-256 verification and `java -version` validation; failures/cancellation preserve working runtimes.
- **Settings:** choose a game data folder. Bundled backend discovery is automatic and takes precedence over obsolete executable paths from older builds.

The default data folder is `%APPDATA%/FLauncher`. Java uses its `instances` directory. The included Bedrock manager receives a private `%APPDATA%` environment rooted at `<data>/bedrock/profile`, keeping its launcher config separate from a standalone LeviLauncher installation. If you change LeviLauncher's base directory, the library follows its `base_root` setting. Returning from a manager refreshes the library.

Minecraft game files, Microsoft accounts/licensing and Windows Gaming Services are not redistributed. Bedrock may require Windows components such as WebView2, Gaming Services, GameInput, UWP dependencies or the VC runtime; LeviLauncher handles its normal dependency checks. The welcome attribution appears on every desktop startup.

## Build

```powershell
dotnet test desktop/FLauncher.Tests
./scripts/publish-desktop.ps1
./scripts/verify-desktop-bundle.ps1 -BundlePath artifacts/desktop-win-x64-complete
```

The default publisher downloads the **pinned, SHA-256-checked** official backend releases in `backends.lock.json`, packages the .NET and Qt runtime dependencies, includes matching upstream source archives plus the desktop sources, checks the engine can start, then publishes the complete directory and ZIP. Downloads are cached in `.cache/bundle`. It never silently produces a shell-only release when a backend fails to download.

Currently pinned: Prism Launcher **11.1.1** (MinGW x64 portable) and LeviLauncher **0.3.13**. These are unmodified upstream binaries. The C++ Java fixes in this repository are only part of a locally built FLauncher engine; they are **not** compiled into the pinned Prism binary. The Avalonia Java installer is included in either build.

To include a built copy of this fork instead of the upstream Java binary:

```powershell
./scripts/publish-desktop.ps1 -EngineDirectory C:/path/to/complete/FLauncher-distribution
```

Supply the complete CMake install output including `FLauncher.exe`, Qt plugins, DLLs and launch JARs. Build instructions remain in `BULDING.md`. If distributing a custom engine, include its exact corresponding source/build changes as well. The manifest marks custom builds.

To produce a setup executable, compile the repo's `installer-script.iss` with Inno Setup 6 after publishing. It consumes the complete bundle and creates per-user shortcuts to the Avalonia entry point. It no longer contains machine-specific paths.

## Development and checks

```powershell
dotnet run --project desktop/FLauncher.Desktop
# UI development only; intentionally does not include launchers:
./scripts/publish-desktop.ps1 -ShellOnly
# Opt-in real Java runtime download and execution test:
dotnet test desktop/FLauncher.Tests --settings desktop/FLauncher.Tests/network.runsettings
```

Plain `dotnet run` is a development build; it can use external engine paths in the expanded development settings. Use the publish script for a working distributable. This is a combined distribution with an Avalonia home/library/runtime interface. Advanced Java and Bedrock management retain their upstream windows; those screens have not been rewritten in Avalonia.

Tests cover download rejection and cancellation, configuration preservation, library metadata, bundled path selection and process arguments, isolated Bedrock data and rendered UI navigation. Captures are written to `artifacts/ui`. Account authentication and launching a licensed game need an interactive account/game installation and are not automated by these tests.

## Credits

- **Prism Launcher** and contributors: Java launch engine, accounts and instance/modpack management — https://prismlauncher.org
- **LeviLauncher**, **LiteDev (LiteLDev)** and contributors: Bedrock launch/install/mod tools and design inspiration — https://github.com/LiteLDev/LeviLauncher
- **Avalonia**: interface framework — https://avaloniaui.net
- **Eclipse Adoptium**: Temurin runtime downloads — https://adoptium.net
- Minecraft belongs to Mojang / Microsoft.

All rights to upstream work remain with their respective authors. This independent fork is not endorsed by those projects. Existing GPL-3.0-only licensing and upstream notices in `LICENSE` and `COPYING.md` are retained; upstream source archives retain their own notices. The original Avalonia design uses a slate-and-mint palette and rounded panels inspired by LeviLauncher.
