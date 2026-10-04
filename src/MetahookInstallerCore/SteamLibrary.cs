using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

namespace MetahookInstaller;

public class SteamLibrary
{
    private string? _steamPath = null;

    private string[] GetLibraryFolders()
    {
        if (_steamPath == null)
            return [];
        var libraryFolders = new List<string> { _steamPath };
        var configPath = Path.Combine(_steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(configPath))
        {
            try
            {
                var vdf = VdfConvert.Deserialize(File.ReadAllText(configPath));
                foreach (var entry in vdf.Value.Children<VProperty>())
                {
                    var pathToken = entry.Value["path"];
                    if (pathToken != null)
                    {
                        libraryFolders.Add(pathToken.ToString());
                    }
                }
            }
            catch
            {
                // Fallback: ignore parse errors, return what we have
            }
        }

        return [.. libraryFolders];
    }
    private static string? GetInstallDirFromManifest(string manifest)
    {
        try
        {
            var vdf = VdfConvert.Deserialize(manifest);
            var installDir = vdf.Value["installdir"];
            return installDir?.ToString();
        }
        catch
        {
            return null;
        }
    }
    private static string? GetSteamPath()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key == null)
            return null;

        return key.GetValue("SteamPath") as string;
    }

    // Returns the normalized game root directory, or null when the app is not installed.
    public string? FindGameDirectory(uint appId)
    {
        if (_steamPath == null)
        {
            _steamPath = GetSteamPath();
            if (_steamPath == null)
                throw new Exception("Could not found Steam path");
        }

        var libraryFolders = GetLibraryFolders();
        foreach (var library in libraryFolders)
        {
            var gamePath = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(gamePath))
                continue;

            var manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(manifestPath))
                continue;

            var manifest = File.ReadAllText(manifestPath);
            var installDir = GetInstallDirFromManifest(manifest);
            if (string.IsNullOrEmpty(installDir))
                continue;

            var fullPath = Path.Combine(gamePath, installDir);
            if (Directory.Exists(fullPath))
            {
                // Normalize the path to ensure consistent directory separators
                // This fixes the issue where mixed forward and backward slashes break shortcuts
                return Path.GetFullPath(fullPath);
            }
        }

        return null;
    }
}
