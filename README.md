# MetahookInstaller

Windows installer and plugin list editor for [MetaHookSv](https://github.com/hzqst/MetaHookSv), built with Avalonia and ReactiveUI on .NET 8.

## Features

- Discover supported Steam games and select custom GoldSrc game directories.
- Install or uninstall MetaHook runtime files from a supplied `install/output/` directory.
- Install or uninstall from the command line with `MetahookInstallerCLI.exe`.
- Enable, disable and reorder plugins in `metahook/configs/plugins.lst`.
- Switch between English and Simplified Chinese, and System, Light and Dark themes.

## Projects

- `src/MetahookInstallerCore`: shared installation library (payload copying, Steam lookup, uninstall, shortcuts).
- `src/MetahookInstallerCLI`: command line wrapper.
- `src/MetahookInstallerAvalonia`: Avalonia UI and resources.
- `src/MetahookInstallerAvalonia.Desktop`: Windows x64 desktop entry point.

The source was imported from `toolsrc/MetahookInstaller` in MetaHookSv. The original project remains in that repository.

## Run a Release

Download `MetahookInstaller-windows-x64.7z` from this repository's releases and extract it. The installer is self-contained; installing the .NET runtime is not required.

The standalone installer archive contains the installer and its runtime dependencies. Obtain a MetaHookSv release separately and place its complete `install/output/` tree next to `MetahookInstaller.exe`. The MetaHookSv aggregate archive already supplies this layout:

```text
MetahookInstaller-Output/
  MetahookInstaller.exe
  MetahookInstallerCLI.exe
  README.md
  LICENSE
  ...other installer runtime files...
  install/
    output/
      MetaHook.exe and/or MetaHook_blob.exe
      svencoop/
      ...other MetaHook release files...
```

Start the installer:

```powershell
Set-Location path\to\MetahookInstaller-Output
.\MetahookInstaller.exe
```

Select a game, or specify a custom game root and mod directory, then click **Install**. The **Editor** tab manages an installed game's plugin list.

Release builds resolve `install/output/` from the executable's directory, independently of the working directory. Debug builds also search its ancestors. Language (`lang`), theme (`theme`) and generated launch shortcuts are stored in the current working directory.

The installer maps common `svencoop/` resources to the selected mod, selects the normal or blob launcher, and installs root runtime DLLs such as libcurl and Steam API. SDL2/SDL3 are replaced only for normal engines that import SDL2. Existing user plugin lists are retained; root tools and debug symbols are not copied to the game.

Installation overwrites matching root runtime DLLs. Uninstall removes MetaHook launcher and mod files; root DLLs remain in the game directory, and original DLL versions are not restored automatically.

## CLI Usage

`MetahookInstallerCLI.exe` performs the same installation and uninstallation as the GUI. Like the GUI, it resolves `install/output/` from its own directory and writes the launch shortcut to the current working directory.

```powershell
# Install into the Steam copy of Sven Co-op
.\MetahookInstallerCLI.exe -appid 225840
# Install into a mod of a specific Half-Life directory
.\MetahookInstallerCLI.exe -appid 70 -gamedir "D:\Games\Half-Life" -moddir gearbox
# Uninstall
.\MetahookInstallerCLI.exe -appid 225840 -uninstall
```

Arguments (case-insensitive):

- `-appid <appid>`: required Steam app ID of the game.
- `-gamedir <gamedir>`: optional game root directory. Defaults to the app's Steam install directory.
- `-moddir <moddir>`: optional mod directory under the game root. Defaults to the app's base mod in the supported games list, for example `svencoop` for 225840 and `valve` for 70; required for other app IDs.
- `-uninstall`: remove MetaHook instead of installing it.
- `-help`: print usage.

Both installation and uninstallation require `<gamedir>/<moddir>/liblist.gam`, so a mistyped mod directory cannot remove the root launchers. The exit code is `0` on success and `1` on invalid arguments, errors, or files that could not be deleted during uninstallation.

## Build

Use Windows x64 with the .NET 8 SDK. NativeAOT publishing also requires the Visual Studio 2022 **Desktop development with C++** workload and a Windows SDK.

From the repository root:

```powershell
dotnet restore MetahookInstaller.sln
dotnet build MetahookInstaller.sln -c Debug --no-restore
dotnet build MetahookInstaller.sln -c Release --no-restore
dotnet run --project src\MetahookInstallerAvalonia.Desktop
dotnet run --project src\MetahookInstallerCLI -- -appid 225840
```

Place a MetaHookSv `install/output/` tree in the repository root, or an ancestor of the Debug executable, to exercise installation while developing.

## File-system regression tests

The test harness uses .NET without additional test packages. It covers source resolution, resource mapping, launcher selection, runtime DLL copying, SDL conditions, existing plugin list preservation, uninstallation, shortcuts and CLI argument handling:

```powershell
dotnet run --project tests/MetahookInstaller.FileTests -c Release
# Also install a real CMake output tree into temporary simulated game directories:
dotnet run --project tests/MetahookInstaller.FileTests -c Release -- --payload D:/MetaHookSv/install/output
```

## Publish

The existing folder profile publishes a self-contained Windows x64 NativeAOT program:

```powershell
dotnet publish src\MetahookInstallerAvalonia.Desktop\MetahookInstallerAvalonia.Desktop.csproj -p:PublishProfile=FolderProfile -o MetahookInstaller-Output
Move-Item -LiteralPath MetahookInstaller-Output\MetahookInstallerAvalonia.Desktop.exe -Destination MetahookInstaller-Output\MetahookInstaller.exe -Force
Copy-Item -LiteralPath README.md,LICENSE -Destination MetahookInstaller-Output
```

The current dependency versions emit trimming and AOT analysis warnings. Check the published program's behavior as well as build success.

The CLI is published as a compressed single-file executable and placed next to `MetahookInstaller.exe`:

```powershell
dotnet publish src/MetahookInstallerCLI/MetahookInstallerCLI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o build/cli-publish
Copy-Item -LiteralPath build\cli-publish\MetahookInstallerCLI.exe -Destination MetahookInstaller-Output
```

MetaHookSv publishes the GUI as a single self-contained executable, including native dependencies, using the following command, and ships the CLI next to it. Native DLLs are extracted automatically at runtime:

```powershell
dotnet publish src/MetahookInstallerAvalonia.Desktop/MetahookInstallerAvalonia.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -p:PublishTrimmed=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugSymbols=false -p:DebugType=None -o MetahookInstaller-Output
```

## CI and Releases

- Pushes and pull requests to `main`, and manual runs, build and package Windows x64 with .NET 8 on `windows-2022`.
- Tags matching `v*` create a GitHub Release with `MetahookInstaller-windows-x64.7z`.
- Both workflows use the same composite action to restore, build, run the file-system tests, publish the GUI and CLI, include documentation, exclude debug symbols and verify the 7z archive.

## License

MIT; see [LICENSE](LICENSE) for the original copyright notice.
