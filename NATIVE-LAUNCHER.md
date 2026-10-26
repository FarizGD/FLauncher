# FLauncher — integrated React/Go launcher

This replaces the earlier Avalonia wrapper and bundled-launcher experiments. The active application is built from `LeviLauncher/`, retaining LeviLauncher's Bedrock services and adding an in-process Go Java service and React management page. It does not start Prism Launcher or LeviLauncher executables.

## Run

Run `artifacts/FLauncher-native/FLauncher.exe`, or extract `artifacts/FLauncher-native-win-x64.zip`. The frontend and Bedrock native resources are embedded in the executable. Windows WebView2 is required; it is the renderer used by Wails. Minecraft licensing and normal Windows Bedrock dependencies still apply.

The **Java Edition** section creates Vanilla/Fabric/Forge/NeoForge instances, installs Minecraft and the matching Java runtime, launches directly, shows game output, edits memory/runtime settings, searches Modrinth and installs required dependencies, enables/disables local mods, imports worlds/resource packs/shader packs, backs up instances, and moves removed instances/content into recoverable trash. The Java home uses the same Minecraft hero, version picker, launch controls, and content cards as Bedrock. Use the crafting-table/user icon to open Java accounts. The selected Java profile is independent of Bedrock.

The **Bedrock Edition** sections retain the existing install/download/version/mod/content screens and their Go implementations. FLauncher is permanently portable: all launcher-managed files live in `Flauncher/` beside `FLauncher.exe`, including downloads, logs, settings, WebView2 data and the Java runtime. Saved absolute base-folder overrides are ignored. Move the executable and this folder together. Use a writable **NTFS** USB drive: shared Java mods use junctions, and Bedrock depends on Windows package support. Existing Prism data is not automatically migrated. Older FLauncher profiles in `%APPDATA%/flauncher.exe` can be copied into this folder with the launcher closed; do not follow or copy old mods junctions. They are recreated at launch. Existing system Bedrock worlds must be imported into a portable instance.

Java downloads now have their own tab alongside GDK and UWP, with a Java sidebar shortcut. Entries come from official Minecraft and loader catalogs. **Vanilla, Fabric, modern Forge (1.13+), and NeoForge** are supported. Modpack import is not implemented yet. Modern Java versions with Mojang version metadata are supported; historical versions without a client/asset-index manifest produce an error. This is an integrated working foundation, not full Prism feature parity. Interactive Microsoft login and actual licensed Java/Bedrock game launches require manual account testing.

All Java instances share `<executable folder>/Flauncher/java-edition/mods`. Each game uses a Windows junction to that folder; importing, removing, or toggling a mod affects every instance. Existing instance mods migrate without overwriting conflicting files. Conflicts stop migration and name the file to resolve. Mod compatibility still depends on the Minecraft version and loader. Backups include the shared folder once as `shared-mods/`.

Forge and NeoForge use checksum-verified official installers in headless mode, running under the managed Java runtime. These are background installation tools, not separate launcher interfaces. Live installation checks passed for Minecraft 1.20.1/Fabric, 1.20.1/Forge, and 1.21.1/NeoForge; licensed gameplay still needs interactive testing.

## Portable behavior

Managed Java runtime paths are stored relative to the library. Owned shared-mods junctions reconnect after relocation, including a changed drive letter. Custom Java executables outside the library remain external dependencies. Bedrock data isolation is always on; launching old UWP versions before 1.19.70.2 is blocked because they cannot keep game data in the portable instance. Windows registration, Gaming Services, WebView2 and account sign-in may still be required on each PC.

Bedrock installation accepts valid **Full or Trial** content licenses returned by Microsoft. The launcher no longer demands a full license. This does not grant a trial, bypass Microsoft/Windows entitlement checks, or remove trial limits. Trial availability is controlled by Microsoft.

The labeled sidebar groups Play, Library and Launcher. Java and Bedrock share a navy animated launch panel, a mint launch button, and edition-specific controls. Ambient motion, spring navigation and hover transitions honor the animation setting and reduced-motion preference.

Release ZIPs contain only the executable, notices and README; they never include your `Flauncher` data, games or account files.

## Java accounts

The account manager supports adding, selecting, reauthenticating, and removing Microsoft, Ely.by, and offline profiles. Display-only account metadata travels in `accounts.json`; tokens stay DPAPI-encrypted. On a different Windows user, offline profiles remain usable and online profiles ask for sign-in again. Removing the active profile leaves no selected account rather than silently switching identities.

- Microsoft uses the native Windows account picker and checks for a Java Edition profile. Saved Windows account references are used for exact-account renewal, without changing Bedrock selection. Minecraft access tokens remain in memory.
- Ely.by supports email/username, password, and optional six-digit two-factor code using its documented [Minecraft authentication API](https://docs.ely.by/en/minecraft-auth.html). Passwords and two-factor codes are never saved. Session tokens are refreshed before launch and stored in a Windows DPAPI-encrypted `java-edition/accounts.dpapi` file. Tokens never cross the frontend bindings.
- Offline profiles use standard name-derived Minecraft offline UUIDs. Installed instances use cached launch metadata without network authentication or installation checks. The initial game/runtime install still requires internet. Offline profiles support singleplayer and offline-enabled servers, not servers requiring Microsoft authentication.

Ely.by launches download a pinned, SHA-256-verified authlib-injector 1.2.8 agent and initialize it against Ely.by. Microsoft and offline launches do not use that agent. Upstream [integration documentation](https://docs.ely.by/en/authlib-injector.html) and [source/license](https://github.com/yushijinhun/authlib-injector) describe the runtime component.

Account tests cover encrypted persistence, switching/removal, two-factor errors, session refresh, rejecting mismatched Microsoft identities, provider-specific launch arguments, and cached offline preparation. UI tests use simulated provider responses; the agent download and initialization were tested live. Real Microsoft/Ely.by account sign-in and actual gameplay still need interactive testing.

## Build and test

Prerequisites: Windows x64, Go 1.25.3+ (dependencies may select a newer toolchain), Node 22+, npm. The checkout includes the embedded Bedrock native resources from LeviLauncher; rebuilding that native loader separately still uses its `native/levilauncher` xmake build.

```powershell
./scripts/build-flauncher.ps1
./scripts/verify-flauncher.ps1 -Executable artifacts/FLauncher-native/FLauncher.exe
```

The build generates Wails bindings, runs Go tests, checks frontend types/materials/bundle budgets, embeds the production frontend, and writes the single application and ZIP. The native loader may start game/dependency processes as required; it never opens another launcher UI.

Opt-in live installation check (downloads real game files and Java, does not launch a game):

```powershell
cd LeviLauncher
go test -tags integration ./internal/javaedition -run TestLiveInstall -v -timeout 12m
```

UI checks use `frontend/e2e/java-edition.test.cjs` against `npm run dev:ui-review`; fixtures are restricted to the development server and excluded from production. Standard tests cover archive traversal, download corruption/cancellation, conditional launch arguments, recoverable instance removal, and direct Java launch command construction.

## Credits and sources

FLauncher originated as a Prism Launcher fork and now uses LeviLauncher's React/Go foundation. Credit to **Prism Launcher**, **LeviLauncher**, **LiteDev (LiteLDev)** and their contributors. All rights to upstream work remain with its respective authors. Minecraft belongs to Mojang/Microsoft. This fork is not endorsed by those projects. Retain `COPYING` and `THIRD_PARTY_NOTICES` when distributing. Modified source is in `LeviLauncher/`, especially `internal/javaedition/`, `internal/app/service_java.go`, `internal/xbox/java_windows.go`, and `frontend/src/pages/JavaPage.tsx`.

Upstream self-updates are disabled so they cannot overwrite FLauncher with LeviLauncher and remove its Java functionality. The old `desktop/` code and previous artifact ZIPs are retained only as superseded experiments.

## Bedrock installation diagnostics

Installation errors include the failing stage and a safe reason. Detailed stage events are saved in `Flauncher/logs/bedrock-install.log`. Diagnostics exclude tokens, account references, request URLs and response bodies. A generic error alone does not mean a license is missing.
