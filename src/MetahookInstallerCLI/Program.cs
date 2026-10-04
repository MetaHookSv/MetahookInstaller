using MetahookInstaller;
using MetahookInstaller.CLI;

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

    var source = InstallPayload.FindApplicationSourceDirectory()
        ?? throw new DirectoryNotFoundException(
            "The install/output folder cannot be located next to MetahookInstallerCLI.exe. Please extract the complete archive before running the programme.");
    var launcher = MetahookSetup.Install(source, target.GameDirectory, target.ModDirectory);
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
