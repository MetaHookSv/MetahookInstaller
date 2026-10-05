using MetahookInstaller;
using MetahookInstaller.CLI;
using System.Text.Json;

CommandLineOptions options;
try
{
    options = CommandLineOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    Console.Error.WriteLine(CommandLineOptions.Usage);
    return 1;
}

if (options.ShowHelp)
{
    Console.WriteLine(CommandLineOptions.Usage);
    return 0;
}

try
{
    var steamLibrary = new SteamLibrary();
    var target = options.ResolveTarget(steamLibrary.FindGameDirectory);
    if (options.DescribeTarget)
    {
        var gameDirectory = Path.GetFullPath(target.GameDirectory);
        var launcherPath = MetahookSetup.DescribeLauncherPath(gameDirectory, target.ModDirectory, options.PluginsOnly);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("GameDirectory", gameDirectory);
            writer.WriteString("ModDirectory", target.ModDirectory);
            writer.WriteString("LauncherPath", launcherPath);
            writer.WriteEndObject();
        }
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(output.ToArray()));
        return 0;
    }

    var shortcutDirectory = Path.GetFullPath(".");

    if (options.Uninstall)
    {
        var failures = MetahookSetup.Uninstall(target.GameDirectory, target.ModDirectory);
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"Warning: failed to delete {failure.Path}: {failure.Message}");
        }

        MetahookSetup.DeleteShortcut(shortcutDirectory, target.GameName);
        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"Error: uninstalled MetaHook from {target.GameName} with {failures.Count} failure(s).");
            return 1;
        }

        Console.WriteLine($"Uninstalled MetaHook from {target.GameName}: {Path.Combine(target.GameDirectory, target.ModDirectory)}");
        return 0;
    }

    var source = InstallPayload.FindApplicationSourceDirectory(options.PluginsOnly)
        ?? throw new DirectoryNotFoundException(
            "The install/output folder cannot be located next to MetahookInstallerCLI.exe. Please extract the complete archive before running the programme.");
    if (options.PluginsOnly)
    {
        var existingLauncher = MetahookSetup.InstallPlugins(source, target.GameDirectory, target.ModDirectory);
        Console.WriteLine($"Updated plugins for {target.GameName}: {Path.Combine(target.GameDirectory, target.ModDirectory)}");
        Console.WriteLine($"Launcher: {existingLauncher}");
        return 0;
    }
    var launcher = MetahookSetup.Install(source, target.GameDirectory, target.ModDirectory, options.IncludeDebugSymbols);
    var shortcut = MetahookSetup.CreateShortcut(shortcutDirectory, target.GameName, launcher,
        target.GameDirectory, target.ModDirectory);
    Console.WriteLine($"Installed MetaHook for {target.GameName}: {Path.Combine(target.GameDirectory, target.ModDirectory)}");
    Console.WriteLine($"Launcher: {launcher}");
    Console.WriteLine($"Shortcut: {shortcut}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    return 1;
}
