using ShellLink;
using System;
using System.Collections.Generic;
using System.IO;

namespace MetahookInstaller;

public sealed record UninstallFailure(string Path, string Message);

public static class MetahookSetup
{
    public static bool IsValidInstallTarget(string gameDirectory, string modDirectory)
    {
        return !string.IsNullOrEmpty(gameDirectory) &&
            File.Exists(Path.Combine(gameDirectory, modDirectory, "liblist.gam"));
    }

    private static void EnsureValidInstallTarget(string gameDirectory, string modDirectory)
    {
        if (!IsValidInstallTarget(gameDirectory, modDirectory))
            throw new InvalidOperationException(
                $"Invalid install target: {Path.Combine(gameDirectory, modDirectory, "liblist.gam")} does not exist.");
    }

    // Describes a complete payload (both launchers) without requiring or deploying it.
    public static string DescribeLauncherPath(string gameDirectory, string modDirectory)
    {
        EnsureValidInstallTarget(gameDirectory, modDirectory);
        return InstallPayload.GetLauncherPath(Path.GetFullPath(gameDirectory), modDirectory,
            PortableExecutable.IsLegitimatePE(Path.Combine(gameDirectory, "hw.dll")));
    }

    // Returns the installed launcher path.
    public static string Install(string source, string gameDirectory, string modDirectory, bool includeDebugSymbols = false)
    {
        EnsureValidInstallTarget(gameDirectory, modDirectory);
        var hwDllPath = Path.Combine(gameDirectory, "hw.dll");
        return InstallPayload.Install(source, gameDirectory, modDirectory,
            PortableExecutable.IsLegitimatePE(hwDllPath), PortableExecutable.HasImportedModule(hwDllPath, "sdl2.dll"), includeDebugSymbols);
    }

    // Deletes every MetaHook file it can and reports the rest; root runtime DLLs are left in place.
    // The target is validated first so that a mistyped mod directory cannot remove the root launchers.
    public static IReadOnlyList<UninstallFailure> Uninstall(string gameDirectory, string modDirectory)
    {
        EnsureValidInstallTarget(gameDirectory, modDirectory);
        string[] list = [
            $"{modDirectory}/metahook/",
            $"{modDirectory}/renderer/",
            $"{modDirectory}/scmodeldownloader/",
            $"{modDirectory}/vgui2ext/",
            $"{modDirectory}/captionmod/",
            $"{modDirectory}/bulletphysics/",
            $"{modDirectory}/sprites/radio_external.txt",
            $"{modDirectory}/sprites/voiceicon_external.txt",
            "MetaHook.exe",
            "MetaHook_blob.exe"
            ];
        var failures = new List<UninstallFailure>();
        foreach (var item in list)
        {
            if (item.EndsWith('/'))
            {
                var dirPath = Path.Combine(gameDirectory, item.TrimEnd('/'));
                if (Directory.Exists(dirPath))
                {
                    try
                    {
                        Directory.Delete(dirPath, true);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new UninstallFailure(dirPath, ex.Message));
                    }
                }
            }
            else
            {
                var filePath = Path.Combine(gameDirectory, item);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new UninstallFailure(filePath, ex.Message));
                    }
                }
            }
        }
        return failures;
    }

    public static string GetShortcutPath(string directory, string gameName)
    {
        return Path.Combine(directory, $"MetaHook for {gameName}.lnk");
    }

    public static string CreateShortcut(string directory, string gameName, string launcherPath,
        string gameDirectory, string modDirectory)
    {
        var shortcutPath = GetShortcutPath(directory, gameName);
        Shortcut lnk = Shortcut.CreateShortcut(
            launcherPath,
            $"-insecure -game {modDirectory}",
            gameDirectory,
            launcherPath,
            0);
        lnk.WriteToFile(shortcutPath);
        return shortcutPath;
    }

    public static void DeleteShortcut(string directory, string gameName)
    {
        var shortcutPath = GetShortcutPath(directory, gameName);
        if (File.Exists(shortcutPath))
        {
            try
            {
                File.Delete(shortcutPath);
            }
            catch
            {
                // Ignore shortcut deletion failure
            }
        }
    }
}
