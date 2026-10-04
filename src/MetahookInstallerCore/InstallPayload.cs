using System;
using System.IO;
using System.Linq;

namespace MetahookInstaller;

public static class InstallPayload
{
    // Release builds only look next to the executable; Debug builds also search its ancestors.
    public static string? FindApplicationSourceDirectory()
    {
#if DEBUG
        return FindSourceDirectory(AppContext.BaseDirectory, true);
#else
        return FindSourceDirectory(AppContext.BaseDirectory, false);
#endif
    }

    public static string? FindSourceDirectory(string applicationDirectory, bool searchParents)
    {
        DirectoryInfo? directory = new(Path.GetFullPath(applicationDirectory));
        while (directory != null)
        {
            var source = Path.Combine(directory.FullName, "install", "output");
            if (File.Exists(Path.Combine(source, "MetaHook.exe")) ||
                File.Exists(Path.Combine(source, "MetaHook_blob.exe")))
                return source;
            if (!searchParents)
                break;
            directory = directory.Parent;
        }
        return null;
    }

    public static string Install(string source, string gameDirectory, string modDirectory,
        bool isNonBlobEngine, bool needsSdl)
    {
        var normalLauncher = Path.Combine(source, "MetaHook.exe");
        isNonBlobEngine = isNonBlobEngine && File.Exists(normalLauncher);
        var sourceLauncher = isNonBlobEngine ? normalLauncher : Path.Combine(source, "MetaHook_blob.exe");
        if (!File.Exists(sourceLauncher))
            throw new FileNotFoundException("Could not find the required MetaHook launcher.", sourceLauncher);

        var isSvenCoop = modDirectory.Equals("svencoop", StringComparison.OrdinalIgnoreCase);
        var targetLauncher = Path.Combine(gameDirectory,
            isNonBlobEngine ? (isSvenCoop ? "svencoop.exe" : "MetaHook.exe") : "MetaHook_blob.exe");
        var commonResources = Path.Combine(source, "svencoop");
        if (Directory.Exists(commonResources))
            CopyDirectory(commonResources, Path.Combine(gameDirectory, modDirectory));

        foreach (var folder in Directory.GetDirectories(source).Where(directory =>
            Path.GetFileName(directory).Equals(modDirectory, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(directory).StartsWith(modDirectory + "_", StringComparison.OrdinalIgnoreCase)))
        {
            if (isSvenCoop && Path.GetFileName(folder).Equals("svencoop", StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(folder, Path.Combine(gameDirectory, Path.GetFileName(folder)));
        }

        var platform = Path.Combine(source, "platform");
        if (isSvenCoop && Directory.Exists(platform))
            CopyDirectory(platform, Path.Combine(gameDirectory, "platform"));

        File.Copy(sourceLauncher, targetLauncher, true);
        foreach (var file in Directory.GetFiles(source).Where(file =>
            Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var name = Path.GetFileName(file);
            var isSdl = name.Equals("SDL2.dll", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("SDL3.dll", StringComparison.OrdinalIgnoreCase);
            if (!isSdl || (isNonBlobEngine && needsSdl))
                File.Copy(file, Path.Combine(gameDirectory, name), true);
        }

        var configs = Path.Combine(gameDirectory, modDirectory, "metahook", "configs");
        var pluginList = Path.Combine(configs, "plugins.lst");
        var template = Path.Combine(configs, isSvenCoop ? "plugins_svencoop.lst" : "plugins_goldsrc.lst");
        if (!File.Exists(pluginList) && File.Exists(template))
            File.Copy(template, pluginList);
        foreach (var name in new[] { "plugins_svencoop.lst", "plugins_goldsrc.lst" })
        {
            var path = Path.Combine(configs, name);
            if (File.Exists(path))
                File.Delete(path);
        }
        return targetLauncher;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
