using System.Globalization;
using MetahookInstaller;

namespace MetahookInstaller.CLI;

public sealed record InstallTarget(string GameName, string GameDirectory, string ModDirectory);

public sealed record CommandLineOptions(
    uint AppId,
    string? GameDirectory,
    string? ModDirectory,
    bool Uninstall,
    bool ShowHelp)
{
    public const string Usage =
        "Usage: MetahookInstallerCLI -appid <appid> [-gamedir <gamedir>] [-moddir <moddir>] [-uninstall]\n" +
        "  -appid <appid>      Steam app ID of the game, e.g. 225840 (Sven Co-op) or 70 (Half-Life).\n" +
        "  -gamedir <gamedir>  Game root directory. Defaults to the Steam install directory of the app.\n" +
        "  -moddir <moddir>    Mod directory under the game root. Defaults to the app's base mod.\n" +
        "  -uninstall          Remove MetaHook instead of installing it.\n" +
        "  -help               Show this help.";

    private static readonly HashSet<string> ValueKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "appid", "gamedir", "moddir",
    };

    private static readonly HashSet<string> HelpKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "help", "h", "?",
    };

    public static CommandLineOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var uninstall = false;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith('-') || arg.Length == 1)
            {
                throw new ArgumentException($"Invalid argument '{arg}'. Expected -key <value>.");
            }

            var key = arg[1..];
            if (HelpKeys.Contains(key))
            {
                return new CommandLineOptions(0, null, null, false, true);
            }

            if (key.Equals("uninstall", StringComparison.OrdinalIgnoreCase))
            {
                uninstall = true;
                continue;
            }

            if (!ValueKeys.Contains(key))
            {
                throw new ArgumentException($"Unknown argument '{arg}'.");
            }

            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                throw new ArgumentException($"Argument '{arg}' requires a value.");
            }

            values[key] = args[++i].Trim();
        }

        if (!values.TryGetValue("appid", out var appIdText))
        {
            throw new ArgumentException("Required argument '-appid <appid>' is missing.");
        }

        if (!uint.TryParse(appIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) || appId == 0)
        {
            throw new ArgumentException($"Invalid app ID '{appIdText}'.");
        }

        return new CommandLineOptions(
            appId,
            values.GetValueOrDefault("gamedir"),
            values.GetValueOrDefault("moddir"),
            uninstall,
            false);
    }

    // findGameDirectory is only queried when -gamedir is omitted.
    public InstallTarget ResolveTarget(Func<uint, string?> findGameDirectory)
    {
        var modDirectory = ModDirectory ?? KnownGames.Find(AppId)?.ModDirectory
            ?? throw new ArgumentException($"App ID {AppId} is not a known game; specify '-moddir <moddir>'.");
        var gameDirectory = GameDirectory != null
            ? Path.GetFullPath(GameDirectory)
            : findGameDirectory(AppId)
                ?? throw new ArgumentException($"Could not find the Steam install directory of app {AppId}; specify '-gamedir <gamedir>'.");
        var gameName = KnownGames.Find(AppId, modDirectory)?.Name ?? modDirectory;
        return new InstallTarget(gameName, gameDirectory, modDirectory);
    }
}
