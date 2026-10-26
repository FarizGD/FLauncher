# BULDING

This file describes how to build FLauncher on Windows, Linux, and macOS.

The active integrated launcher uses React/Go in `LeviLauncher/`. Run `./scripts/build-flauncher.ps1`; see [`NATIVE-LAUNCHER.md`](NATIVE-LAUNCHER.md). The Avalonia wrapper is superseded. The instructions below describe the retained legacy C++ fork.

FLauncher is a CMake + Qt 6 project. The repo includes CMake presets and a `vcpkg.json` manifest.

## Common Requirements

- CMake 3.28+ (required by `CMakePresets.json`)
- Ninja (recommended, presets use Ninja Multi-Config)
- Qt 6 (Core, Widgets, Network, NetworkAuth, OpenGL)
- A C++20 compiler

Targets:
- `FLauncher` = classic Widgets UI

All commands below are run from the repo root.

## Windows

### Option A: MSVC + vcpkg (recommended)

1. Install Visual Studio 2022 with the `Desktop development with C++` workload.
2. Install vcpkg and set `VCPKG_ROOT` to the vcpkg folder.
3. Configure + build:

```powershell
cmake --preset windows_msvc
cmake --build --preset windows_msvc --config Release
```

Build a specific target:

```powershell
cmake --build --preset windows_msvc --config Release --target FLauncher
```

### Option B: MinGW

This uses Qt's MinGW toolchain. You can install both MinGW and Qt via the Qt Online Installer.

#### Step 1: Install Qt + MinGW (Qt Online Installer)

1. Download and run the Qt Online Installer.
2. Choose a Qt 6.x version.
3. Under that Qt version, select:
   - `MinGW <version> 64-bit` (the toolchain)
   - `Qt 6.x > MinGW 64-bit` (the Qt build that matches MinGW)
   - `Qt 6.x > Qt NetworkAuth`
4. Finish install.

#### Step 2: Add Qt and MinGW to PATH

Adjust these paths to your Qt install location:

```powershell
$env:QTDIR = "C:\\Qt\\6.x.x\\mingw_64"
$env:PATH = "$env:QTDIR\\bin;C:\\Qt\\Tools\\mingw1120_64\\bin;$env:PATH"
```

If you want them permanently, set environment variables in Windows System Settings:
- `QTDIR` = `C:\Qt\6.x.x\mingw_64`
- Add `C:\Qt\6.x.x\mingw_64\bin` and `C:\Qt\Tools\mingw1120_64\bin` to `PATH`.

#### Step 3: Configure + build

Use the preset below when MinGW and Qt are on PATH:

```powershell
cmake --preset windows_mingw
cmake --build --preset windows_mingw --config Release
```

If CMake cannot find Qt:

```powershell
$env:CMAKE_PREFIX_PATH = "C:\\Qt\\6.x.x\\mingw_64"
```

#### ECM (extra-cmake-modules) requirement

MinGW builds also need `ECM` (extra-cmake-modules). If you see:
`Could not find a package configuration file provided by "ECM"` then install ECM and point CMake to it.

Option A (vcpkg, easiest):

```powershell
# from the repo root (manifest mode uses vcpkg.json)
vcpkg install

# then add vcpkg to CMake (if not already set):
$env:CMAKE_TOOLCHAIN_FILE = "C:\\path\\to\\vcpkg\\scripts\\buildsystems\\vcpkg.cmake"
cmake --preset windows_mingw
```

Option B (manual ECM):

1. Download the ECM release zip and extract it.
2. Set `ECM_DIR` to the folder that contains `ECMConfig.cmake`:

```powershell
$env:ECM_DIR = "C:\\path\\to\\extra-cmake-modules\\share\\ECM\\cmake"
cmake --preset windows_mingw
```

## Linux

### Install build tools and Qt 6

Pick your distro:

Ubuntu/Debian:

```bash
sudo apt update
sudo apt install -y build-essential cmake ninja-build qt6-base-dev qt6-declarative-dev qt6-tools-dev qt6-networkauth-dev
```

Fedora:

```bash
sudo dnf install -y gcc-c++ cmake ninja-build qt6-qtbase-devel qt6-qtdeclarative-devel qt6-qttools-devel qt6-qtnetworkauth-devel
```

Arch:

```bash
sudo pacman -S --needed base-devel cmake ninja qt6-base qt6-declarative qt6-tools qt6-networkauth
```

### Configure + build

```bash
cmake --preset linux
cmake --build --preset linux --config Release
```

Build only the launcher target:

```bash
cmake --build --preset linux --config Release --target FLauncher
```

## macOS

### Install build tools and Qt 6

Using Homebrew:

```bash
brew install cmake ninja qt@6
```

If Qt is not found, set:

```bash
export CMAKE_PREFIX_PATH="$(brew --prefix qt@6)"
```

### Apple Silicon / Intel (single-arch)

Requires vcpkg (preset uses `VCPKG_ROOT`).

```bash
cmake --preset macos
cmake --build --preset macos --config Release
```

### Universal Binary (x86_64 + arm64)

```bash
cmake --preset macos_universal
cmake --build --preset macos_universal --config Release
```

## Notes

- Presets use `Ninja Multi-Config`, so pass `--config Debug` or `--config Release` when building.
- Build `FLauncher` (classic widgets UI).
- If Qt is not found, set `CMAKE_PREFIX_PATH` to your Qt 6 install.
