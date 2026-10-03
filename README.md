# MetahookInstaller

Windows installer and plugin list editor for [MetaHookSv](https://github.com/hzqst/MetaHookSv), built with Avalonia and ReactiveUI on .NET 8.

## Features

- Discover supported Steam games and select custom GoldSrc game directories.
- Install or uninstall MetaHook runtime files from a supplied `Build/` directory.
- Enable, disable and reorder plugins in `metahook/configs/plugins.lst`.
- Switch between English and Simplified Chinese, and System, Light and Dark themes.

## Projects

- `src/MetahookInstallerAvalonia`: UI, resources and installation logic.
- `src/MetahookInstallerAvalonia.Desktop`: Windows x64 desktop entry point.

The source was imported from `toolsrc/MetahookInstaller` in MetaHookSv. The original project remains in that repository.

## Run a Release

Download `MetahookInstaller-windows-x64.7z` from this repository's releases and extract it. The installer is self-contained; installing the .NET runtime is not required.

The installer archive contains only the installer. Obtain a MetaHookSv release separately and copy its complete `Build/` directory next to `MetahookInstaller.exe`:

```text
MetahookInstaller-Output/
  MetahookInstaller.exe
  README.md
  LICENSE
  ...other installer runtime files...
  Build/
    MetaHook.exe and/or MetaHook_blob.exe
    svencoop/
    ...other MetaHook release files...
```

Start the installer with that directory as the working directory:

```powershell
Set-Location path\to\MetahookInstaller-Output
.\MetahookInstaller.exe
```

Select a game, or specify a custom game root and mod directory, then click **Install**. The **Editor** tab manages an installed game's plugin list.

The existing installer searches for `Build/` in the current directory or its parent for Release builds, and additional parents for Debug builds. Language (`lang`), theme (`theme`) and generated launch shortcuts are also stored in the current directory.

## Build

Use Windows x64 with the .NET 8 SDK. NativeAOT publishing also requires the Visual Studio 2022 **Desktop development with C++** workload and a Windows SDK.

From the repository root:

```powershell
dotnet restore MetahookInstaller.sln
dotnet build MetahookInstaller.sln -c Debug --no-restore
dotnet build MetahookInstaller.sln -c Release --no-restore
dotnet run --project src\MetahookInstallerAvalonia.Desktop
```

Place a MetaHookSv `Build/` directory in the repository root to exercise installation while developing.

## Publish

The existing folder profile publishes a self-contained Windows x64 NativeAOT program:

```powershell
dotnet publish src\MetahookInstallerAvalonia.Desktop\MetahookInstallerAvalonia.Desktop.csproj -p:PublishProfile=FolderProfile -o MetahookInstaller-Output
Move-Item -LiteralPath MetahookInstaller-Output\MetahookInstallerAvalonia.Desktop.exe -Destination MetahookInstaller-Output\MetahookInstaller.exe -Force
Copy-Item -LiteralPath README.md,LICENSE -Destination MetahookInstaller-Output
```

The current dependency versions emit trimming and AOT analysis warnings. Check the published program's behavior as well as build success.

## CI and Releases

- Pushes and pull requests to `main`, and manual runs, build and package Windows x64 with .NET 8 on `windows-2022`.
- Tags matching `v*` create a GitHub Release with `MetahookInstaller-windows-x64.7z`.
- Both workflows use the same composite action to restore, build, publish, include documentation, exclude debug symbols and verify the 7z archive.

## License

MIT; see [LICENSE](LICENSE) for the original copyright notice.
