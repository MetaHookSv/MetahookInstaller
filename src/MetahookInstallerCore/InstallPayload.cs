using System;
using System.IO;
using System.Linq;

namespace MetahookInstaller;

public static class InstallPayload
{
    // Release builds only look next to the executable; Debug builds also search its ancestors.
    // An explicitSource overrides the search so callers with a payload elsewhere need no lookup.
    public static string? FindApplicationSourceDirectory(bool pluginsOnly = false, string? explicitSource = null)
    {
#if DEBUG
        return FindSourceDirectory(AppContext.BaseDirectory, true, pluginsOnly, explicitSource);
#else
        return FindSourceDirectory(AppContext.BaseDirectory, false, pluginsOnly, explicitSource);
#endif
    }

    public static string? FindSourceDirectory(string applicationDirectory, bool searchParents,
        bool pluginsOnly = false, string? explicitSource = null)
    {
        if (explicitSource != null)
        {
            var explicitPath = Path.GetFullPath(explicitSource);
            return IsSourceDirectory(explicitPath, pluginsOnly) ? explicitPath : null;
        }

        DirectoryInfo? directory = new(Path.GetFullPath(applicationDirectory));
        while (directory != null)
        {
            var source = Path.Combine(directory.FullName, "install", "output");
            if (IsSourceDirectory(source, pluginsOnly))
                return source;
            if (!searchParents)
                break;
            directory = directory.Parent;
        }
        return null;
    }

    // A payload directory holds either the plugin tree or the launcher the deployment needs.
    private static bool IsSourceDirectory(string source, bool pluginsOnly) =>
        pluginsOnly ? HasPlugins(source) :
        File.Exists(Path.Combine(source, "MetaHook.exe")) || File.Exists(Path.Combine(source, "MetaHook_blob.exe"));

    public static string GetLauncherPath(string gameDirectory, string modDirectory, bool isNonBlobEngine)
    {
        var isSvenCoop = modDirectory.Equals("svencoop", StringComparison.OrdinalIgnoreCase);
        return Path.Combine(gameDirectory,
            isNonBlobEngine ? (isSvenCoop ? "svencoop.exe" : "MetaHook.exe") : "MetaHook_blob.exe");
    }

    private static bool HasPlugins(string source)
    {
        var plugins = Path.Combine(source, "svencoop", "metahook", "plugins");
        return Directory.Exists(plugins) && Directory.EnumerateFiles(plugins)
            .Any(file => Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase));
    }

    public static void InstallPlugins(string source, string gameDirectory, string modDirectory)
    {
        if (!HasPlugins(source))
            throw new DirectoryNotFoundException("Plugin payload must contain svencoop/metahook/plugins/*.dll.");
        CopyResources(source, gameDirectory, modDirectory, preservePluginLists: true);
    }

    private static void CopyResources(string source, string gameDirectory, string modDirectory, bool preservePluginLists = false)
    {
        var isSvenCoop = modDirectory.Equals("svencoop", StringComparison.OrdinalIgnoreCase);
        var commonResources = Path.Combine(source, "svencoop");
        if (Directory.Exists(commonResources))
            CopyDirectory(commonResources, Path.Combine(gameDirectory, modDirectory), preservePluginLists);

        foreach (var folder in Directory.GetDirectories(source).Where(directory =>
            Path.GetFileName(directory).Equals(modDirectory, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(directory).StartsWith(modDirectory + "_", StringComparison.OrdinalIgnoreCase)))
        {
            if (isSvenCoop && Path.GetFileName(folder).Equals("svencoop", StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(folder, Path.Combine(gameDirectory, Path.GetFileName(folder)), preservePluginLists);
        }

        var platform = Path.Combine(source, "platform");
        if (isSvenCoop && Directory.Exists(platform))
            CopyDirectory(platform, Path.Combine(gameDirectory, "platform"), preservePluginLists);
    }

    public static string Install(string source, string gameDirectory, string modDirectory,
        bool isNonBlobEngine, bool needsSdl, bool includeDebugSymbols = false)
    {
        var normalLauncher = Path.Combine(source, "MetaHook.exe");
        isNonBlobEngine = isNonBlobEngine && File.Exists(normalLauncher);
        var sourceLauncher = isNonBlobEngine ? normalLauncher : Path.Combine(source, "MetaHook_blob.exe");
        if (!File.Exists(sourceLauncher))
            throw new FileNotFoundException("Could not find the required MetaHook launcher.", sourceLauncher);

        var isSvenCoop = modDirectory.Equals("svencoop", StringComparison.OrdinalIgnoreCase);
        var targetLauncher = GetLauncherPath(gameDirectory, modDirectory, isNonBlobEngine);
        CopyResources(source, gameDirectory, modDirectory);

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

        if (includeDebugSymbols)
        {
            foreach (var file in Directory.GetFiles(source).Where(file =>
                Path.GetExtension(file).Equals(".pdb", StringComparison.OrdinalIgnoreCase)))
                File.Copy(file, Path.Combine(gameDirectory, Path.GetFileName(file)), true);
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

    private static void CopyDirectory(string source, string destination, bool preservePluginLists = false)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            var name = Path.GetFileName(file);
            if (preservePluginLists && (name.Equals("plugins.lst", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("plugins_svencoop.lst", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("plugins_goldsrc.lst", StringComparison.OrdinalIgnoreCase)))
                continue;
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), preservePluginLists);
    }
}
