using MetahookInstaller;
using MetahookInstaller.CLI;

var cases = new (string Name, Action<Fixture> Run)[]
{
    ("Plugin payload discovery does not require or accept only a launcher", fixture =>
    {
        var source = fixture.Payload(normal: false, blob: false);
        Equal<string?>(null, InstallPayload.FindSourceDirectory(fixture.Path("package"), false));
        Equal(source, InstallPayload.FindSourceDirectory(fixture.Path("package"), false, pluginsOnly: true));
        File.Delete(fixture.Path("package/install/output/svencoop/metahook/plugins/Plugin.dll"));
        fixture.Write("package/install/output/MetaHook.exe", "normal");
        Equal<string?>(null, InstallPayload.FindSourceDirectory(fixture.Path("package"), false, pluginsOnly: true));
    }),
    ("Plugin deployment maps resources and preserves launchers, runtime files and lists", fixture =>
    {
        foreach (var mod in new[] { "svencoop", "valve" })
        {
            var source = fixture.Payload(normal: false, blob: false);
            var game = fixture.Game(mod);
            fixture.NormalEngine();
            var launcher = fixture.Write("game/" + (mod == "svencoop" ? "svencoop.exe" : "MetaHook.exe"), "installed launcher");
            fixture.Write("game/libcurl.dll", "existing runtime");
            fixture.Write("game/MetaHook.pdb", "existing symbols");
            fixture.Write($"game/{mod}/metahook/configs/plugins.lst", "user list");
            fixture.Write($"game/{mod}/metahook/configs/plugins_svencoop.lst", "existing template");
            fixture.Write($"game/{mod}/metahook/plugins/Other.dll", "other plugin");
            fixture.Write("package/install/output/svencoop/metahook/configs/PLUGINS.LST", "must not overwrite");
            fixture.Write("package/install/output/svencoop/metahook/plugins/Plugin.pdb", "plugin symbols");
            fixture.Write("package/install/output/svencoop/metahook/dlls/dependency.dll", "dependency");
            Equal(launcher, MetahookSetup.InstallPlugins(source, game, mod));
            Equal("installed launcher", File.ReadAllText(launcher));
            Equal("existing runtime", File.ReadAllText(fixture.Path("game/libcurl.dll")));
            Equal("existing symbols", File.ReadAllText(fixture.Path("game/MetaHook.pdb")));
            Equal("user list", File.ReadAllText(fixture.Path($"game/{mod}/metahook/configs/plugins.lst")));
            Equal("existing template", File.ReadAllText(fixture.Path($"game/{mod}/metahook/configs/plugins_svencoop.lst")));
            Absent(fixture.Path($"game/{mod}/metahook/configs/plugins_goldsrc.lst"));
            Equal("other plugin", File.ReadAllText(fixture.Path($"game/{mod}/metahook/plugins/Other.dll")));
            Equal("plugin", File.ReadAllText(fixture.Path($"game/{mod}/metahook/plugins/Plugin.dll")));
            Equal("plugin symbols", File.ReadAllText(fixture.Path($"game/{mod}/metahook/plugins/Plugin.pdb")));
            Exists(fixture.Path($"game/{mod}/metahook/dlls/dependency.dll"));
            Exists(fixture.Path(mod == "svencoop" ? "game/svencoop_downloads/resource.dat" : "game/valve_hidpi/resource.dat"));
        }
    }),
    ("Plugin query requires an installed launcher and MetaHook directory without writing", fixture =>
    {
        var game = fixture.Game("valve");
        fixture.NormalEngine();
        Throws<InvalidOperationException>(() => MetahookSetup.DescribeLauncherPath(game, "valve", pluginsOnly: true));
        fixture.Directory("game/valve/metahook");
        Throws<InvalidOperationException>(() => MetahookSetup.DescribeLauncherPath(game, "valve", pluginsOnly: true));
        fixture.Write("game/MetaHook.exe", "normal");
        Equal(fixture.Path("game/MetaHook.exe"), MetahookSetup.DescribeLauncherPath(game, "valve", pluginsOnly: true));
        File.Delete(fixture.Path("game/hw.dll"));
        fixture.Write("game/MetaHook_blob.exe", "blob");
        var before = fixture.Snapshot();
        Equal(fixture.Path("game/MetaHook_blob.exe"), MetahookSetup.DescribeLauncherPath(game, "valve", pluginsOnly: true));
        Equal(before, fixture.Snapshot());
        Throws<InvalidOperationException>(() => MetahookSetup.DescribeLauncherPath(game, "missing", pluginsOnly: true));
    }),
    ("Plugin install rejects invalid payloads and missing installations before copying", fixture =>
    {
        var source = fixture.Payload(normal: false, blob: false);
        var game = fixture.Game("valve");
        Throws<InvalidOperationException>(() => MetahookSetup.InstallPlugins(source, game, "valve"));
        Absent(fixture.Path("game/valve/metahook"));
        fixture.Write("game/MetaHook_blob.exe", "blob");
        fixture.Directory("game/valve/metahook");
        Throws<DirectoryNotFoundException>(() => MetahookSetup.InstallPlugins(fixture.Directory("empty payload"), game, "valve"));
        Equal(fixture.Path("game/MetaHook_blob.exe"), MetahookSetup.InstallPlugins(source, game, "valve"));
        Absent(fixture.Path("game/valve/metahook/configs/plugins.lst"));
        Absent(fixture.Path("game/valve/metahook/configs/plugins_goldsrc.lst"));
    }),
    ("Plugin options compose with query and symbols but not uninstall", _ =>
    {
        Equal(true, CommandLineOptions.Parse(["-appid", "70", "-PLUGINS-ONLY", "-describe-target"]).PluginsOnly);
        Equal(true, CommandLineOptions.Parse(["-appid", "70", "-plugins-only", "-include-debug-symbols"]).IncludeDebugSymbols);
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "70", "-plugins-only", "-uninstall"]));
    }),
    ("CLI plugin query and install work without payload launchers or shortcuts", fixture =>
    {
        var game = fixture.Game("valve");
        fixture.Write("game/MetaHook_blob.exe", "installed blob");
        fixture.Directory("game/valve/metahook");
        fixture.CopyCli();
        var before = fixture.Snapshot();
        var query = fixture.RunCli("-appid", "70", "-gamedir", game, "-plugins-only", "-describe-target");
        Equal(0, query.ExitCode);
        Equal("", query.Error);
        using var json = System.Text.Json.JsonDocument.Parse(query.Output);
        Equal(fixture.Path("game/MetaHook_blob.exe"), json.RootElement.GetProperty("LauncherPath").GetString());
        Equal(before, fixture.Snapshot());
        fixture.Payload(normal: false, blob: false);
        var install = fixture.RunCli("-appid", "70", "-gamedir", game, "-plugins-only", "-include-debug-symbols");
        Equal("", install.Error);
        Equal(0, install.ExitCode);
        Exists(fixture.Path("game/valve/metahook/plugins/Plugin.dll"));
        Equal("installed blob", File.ReadAllText(fixture.Path("game/MetaHook_blob.exe")));
        Absent(fixture.Path("working/MetaHook for Half-Life.lnk"));
        Absent(fixture.Path("game/libcurl.dll"));
        File.Delete(fixture.Path("game/MetaHook_blob.exe"));
        var invalid = fixture.RunCli("-appid", "70", "-gamedir", game, "-plugins-only", "-describe-target");
        Equal(1, invalid.ExitCode);
        Equal("", invalid.Output);
        Equal(true, invalid.Error.Contains("Install MetaHook"));
    }),
    ("Locked plugin deployment reports failure", fixture =>
    {
        var source = fixture.Payload(normal: false, blob: false);
        var game = fixture.Game("valve");
        fixture.Write("game/MetaHook_blob.exe", "blob");
        var plugin = fixture.Write("game/valve/metahook/plugins/Plugin.dll", "old");
        using (new FileStream(plugin, FileMode.Open, FileAccess.Read, FileShare.None))
            Throws<IOException>(() => MetahookSetup.InstallPlugins(source, game, "valve"));
        Equal("old", File.ReadAllText(plugin));
    }),
    ("Describe target selects the same launcher without changing the game", fixture =>
    {
        foreach (var mod in new[] { "svencoop", "valve" })
        {
            var game = fixture.Game(mod);
            fixture.NormalEngine();
            var expected = fixture.Path("game/" + (mod == "svencoop" ? "svencoop.exe" : "MetaHook.exe"));
            Equal(expected, MetahookSetup.DescribeLauncherPath(game, mod));
            Absent(expected);
            Absent(fixture.Path($"game/{mod}/metahook"));
            File.Delete(fixture.Path("game/hw.dll"));
            Equal(fixture.Path("game/MetaHook_blob.exe"), MetahookSetup.DescribeLauncherPath(game, mod));
        }
        Throws<InvalidOperationException>(() => MetahookSetup.DescribeLauncherPath(fixture.Path("game"), "missing"));
    }),
    ("Debug symbols are opt-in and retain their names when the launcher is renamed", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("svencoop");
        fixture.NormalEngine();
        fixture.Write("package/install/output/MetaHook_blob.pdb", "blob symbols");
        fixture.Write("package/install/output/runtime.PDB", "runtime symbols");
        Equal(fixture.Path("game/svencoop.exe"), MetahookSetup.Install(source, game, "svencoop", includeDebugSymbols: true));
        Equal("symbols", File.ReadAllText(fixture.Path("game/MetaHook.pdb")));
        Equal("blob symbols", File.ReadAllText(fixture.Path("game/MetaHook_blob.pdb")));
        Equal("runtime symbols", File.ReadAllText(fixture.Path("game/runtime.PDB")));
        Absent(fixture.Path("game/svencoop.pdb"));
    }),
    ("CLI accepts describe and debug symbols and rejects conflicting modes", _ =>
    {
        Equal(true, CommandLineOptions.Parse(["-appid", "70", "-DESCRIBE-TARGET"]).DescribeTarget);
        Equal(true, CommandLineOptions.Parse(["-appid", "70", "-include-debug-symbols"]).IncludeDebugSymbols);
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "70", "-describe-target", "-uninstall"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "70", "-include-debug-symbols", "-uninstall"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "70", "-describe-target", "-include-debug-symbols"]));
    }),
    ("CLI describe emits JSON without a payload or filesystem side effects", fixture =>
    {
        var game = fixture.Game("svencoop");
        fixture.NormalEngine();
        var workingDirectory = fixture.Directory("empty working directory");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { typeof(CommandLineOptions).Assembly.Location,
            "-appid", "225840", "-gamedir", game, "-describe-target" })
            start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Equal("", error.GetAwaiter().GetResult());
        Equal(0, process.ExitCode);
        using var json = System.Text.Json.JsonDocument.Parse(output.GetAwaiter().GetResult());
        Equal(game, json.RootElement.GetProperty("GameDirectory").GetString());
        Equal("svencoop", json.RootElement.GetProperty("ModDirectory").GetString());
        Equal(fixture.Path("game/svencoop.exe"), json.RootElement.GetProperty("LauncherPath").GetString());
        Equal(0, System.IO.Directory.GetFileSystemEntries(workingDirectory).Length);
        Absent(fixture.Path("game/svencoop.exe"));
        Absent(fixture.Path("game/svencoop/metahook"));
    }),
    ("Source is relative to the application, independent of the working directory", fixture =>
    {
        var source = fixture.Payload();
        var previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = fixture.Directory("elsewhere");
            Equal(source, InstallPayload.FindSourceDirectory(fixture.Path("package"), false));
        }
        finally { Environment.CurrentDirectory = previous; }
    }),
    ("Debug finds an ancestor's install/output", fixture =>
    {
        var source = fixture.Payload();
        var application = fixture.Directory("package/src/Desktop/bin/Debug/net8.0");
        Equal(source, InstallPayload.FindSourceDirectory(application, true));
        Equal<string?>(null, InstallPayload.FindSourceDirectory(application, false));
    }),
    ("Missing payload and the old Build directory are rejected", fixture =>
    {
        fixture.Write("package/Build/MetaHook.exe", "old");
        fixture.Directory("package/install/output");
        Equal<string?>(null, InstallPayload.FindSourceDirectory(fixture.Path("package"), false));
    }),
    ("A blob-only payload is accepted", fixture =>
    {
        var source = fixture.Payload(normal: false);
        Equal(source, InstallPayload.FindSourceDirectory(fixture.Path("package"), false));
    }),
    ("Sven Co-op installs resources and renames the normal launcher", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("svencoop");
        var launcher = InstallPayload.Install(source, game, "svencoop", true, false);
        Equal(fixture.Path("game/svencoop.exe"), launcher);
        Equal("normal", File.ReadAllText(launcher));
        Exists(fixture.Path("game/svencoop/metahook/plugins/Plugin.dll"));
        Exists(fixture.Path("game/svencoop_downloads/resource.dat"));
        Exists(fixture.Path("game/platform/resource.dat"));
        Exists(fixture.Path("game/svencoop/metahook/configs/plugins.lst"));
        Absent(fixture.Path("game/svencoop/metahook/configs/plugins_svencoop.lst"));
        Absent(fixture.Path("game/svencoop/metahook/configs/plugins_goldsrc.lst"));
    }),
    ("A custom mod receives common resources and its own resource directories", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        var launcher = InstallPayload.Install(source, game, "valve", true, false);
        Equal(fixture.Path("game/MetaHook.exe"), launcher);
        Exists(fixture.Path("game/valve/metahook/plugins/Plugin.dll"));
        Exists(fixture.Path("game/valve/custom.dat"));
        Exists(fixture.Path("game/valve_hidpi/resource.dat"));
        Exists(fixture.Path("game/valve/metahook/configs/plugins.lst"));
        Absent(fixture.Path("game/platform"));
        Absent(fixture.Path("game/svencoop_downloads"));
    }),
    ("Blob engines use the blob launcher and leave SDL untouched", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        fixture.Write("game/SDL2.dll", "original SDL");
        var launcher = InstallPayload.Install(source, game, "valve", false, true);
        Equal(fixture.Path("game/MetaHook_blob.exe"), launcher);
        Equal("blob", File.ReadAllText(launcher));
        Equal("original SDL", File.ReadAllText(fixture.Path("game/SDL2.dll")));
    }),
    ("A missing normal launcher falls back to the blob launcher", fixture =>
    {
        var source = fixture.Payload(normal: false);
        var game = fixture.Game("svencoop");
        Equal(fixture.Path("game/MetaHook_blob.exe"), InstallPayload.Install(source, game, "svencoop", true, true));
        Absent(fixture.Path("game/SDL2.dll"));
    }),
    ("A missing selected launcher fails before copying resources", fixture =>
    {
        var source = fixture.Payload(blob: false);
        var game = fixture.Game("valve");
        Throws<FileNotFoundException>(() => InstallPayload.Install(source, game, "valve", false, false));
        Absent(fixture.Path("game/valve/metahook"));
    }),
    ("Root DLLs are updated while tools and debug files remain in the payload", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        fixture.Write("game/libcurl.dll", "previous");
        InstallPayload.Install(source, game, "valve", true, false);
        Equal("curl", File.ReadAllText(fixture.Path("game/libcurl.dll")));
        Equal("steam", File.ReadAllText(fixture.Path("game/steam_api.dll")));
        Absent(fixture.Path("game/SteamAppsLocation.exe"));
        Absent(fixture.Path("game/MetaHook.pdb"));
    }),
    ("A payload without Steam API preserves the host runtime", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        File.Delete(fixture.Path("package/install/output/steam_api.dll"));
        InstallPayload.Install(source, game, "valve", true, false);
        Absent(fixture.Path("game/steam_api.dll"));
        fixture.Write("game/steam_api.dll", "host Steam API");
        InstallPayload.Install(source, game, "valve", true, false);
        Equal("host Steam API", File.ReadAllText(fixture.Path("game/steam_api.dll")));
    }),
    ("SDL is replaced only when required by a normal engine", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        fixture.Write("game/SDL2.dll", "original SDL");
        InstallPayload.Install(source, game, "valve", true, false);
        Equal("original SDL", File.ReadAllText(fixture.Path("game/SDL2.dll")));
        InstallPayload.Install(source, game, "valve", true, true);
        Equal("SDL2", File.ReadAllText(fixture.Path("game/SDL2.dll")));
        Equal("SDL3", File.ReadAllText(fixture.Path("game/SDL3.dll")));
    }),
    ("An existing plugin list retains its timestamp during an upgrade", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        var list = fixture.Write("game/valve/metahook/configs/plugins.lst", "user data");
        File.SetLastWriteTimeUtc(list, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var timestamp = File.GetLastWriteTimeUtc(list);
        InstallPayload.Install(source, game, "valve", true, false);
        Equal(timestamp, File.GetLastWriteTimeUtc(list));
    }),
    ("Setup rejects a target without liblist.gam before copying anything", fixture =>
    {
        var source = fixture.Payload();
        fixture.Directory("game/valve");
        Equal(false, MetahookSetup.IsValidInstallTarget(fixture.Path("game"), "valve"));
        Throws<InvalidOperationException>(() => MetahookSetup.Install(source, fixture.Path("game"), "valve"));
        Absent(fixture.Path("game/valve/metahook"));
        Absent(fixture.Path("game/MetaHook_blob.exe"));
    }),
    ("Setup without hw.dll installs the blob launcher", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        Equal(fixture.Path("game/MetaHook_blob.exe"), MetahookSetup.Install(source, game, "valve"));
    }),
    ("Uninstall removes MetaHook files and keeps root DLLs", fixture =>
    {
        var source = fixture.Payload();
        var game = fixture.Game("valve");
        InstallPayload.Install(source, game, "valve", true, false);
        fixture.Write("game/MetaHook_blob.exe", "blob");
        fixture.Write("game/valve/renderer/shader.txt", "renderer");
        fixture.Write("game/valve/sprites/radio_external.txt", "radio");
        Equal(0, MetahookSetup.Uninstall(game, "valve").Count);
        Absent(fixture.Path("game/valve/metahook"));
        Absent(fixture.Path("game/valve/renderer"));
        Absent(fixture.Path("game/valve/sprites/radio_external.txt"));
        Absent(fixture.Path("game/MetaHook.exe"));
        Absent(fixture.Path("game/MetaHook_blob.exe"));
        Exists(fixture.Path("game/valve/liblist.gam"));
        Exists(fixture.Path("game/libcurl.dll"));
    }),
    ("Uninstall rejects a mistyped mod directory and keeps the root launchers", fixture =>
    {
        var game = fixture.Game("svencoop");
        fixture.Write("game/MetaHook.exe", "normal");
        fixture.Write("game/MetaHook_blob.exe", "blob");
        Throws<InvalidOperationException>(() => MetahookSetup.Uninstall(game, "missing_mod"));
        Exists(fixture.Path("game/MetaHook.exe"));
        Exists(fixture.Path("game/MetaHook_blob.exe"));
    }),
    ("Uninstall reports locked files and continues", fixture =>
    {
        var game = fixture.Game("valve");
        var locked = fixture.Write("game/MetaHook.exe", "normal");
        fixture.Write("game/valve/metahook/plugins/Plugin.dll", "plugin");
        IReadOnlyList<UninstallFailure> failures;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            failures = MetahookSetup.Uninstall(game, "valve");
        Equal(1, failures.Count);
        Equal(locked, failures[0].Path);
        Absent(fixture.Path("game/valve/metahook"));
        Exists(locked);
    }),
    ("Shortcuts are created and deleted by game name", fixture =>
    {
        var game = fixture.Game("valve");
        var launcher = fixture.Write("game/MetaHook.exe", "normal");
        var directory = fixture.Directory("shortcuts");
        var shortcut = MetahookSetup.CreateShortcut(directory, "Half-Life", launcher, game, "valve");
        Equal(fixture.Path("shortcuts/MetaHook for Half-Life.lnk"), shortcut);
        Exists(shortcut);
        MetahookSetup.DeleteShortcut(directory, "Half-Life");
        Absent(shortcut);
        MetahookSetup.DeleteShortcut(directory, "Half-Life");
    }),
    ("CLI parses keys case-insensitively and the uninstall switch", _ =>
    {
        var options = CommandLineOptions.Parse(["-AppID", "70", "-gamedir", "C:/Games/Half-Life", "-MODDIR", "gearbox", "-uninstall"]);
        Equal(new CommandLineOptions(70, "C:/Games/Half-Life", "gearbox", true, false), options);
        Equal(new CommandLineOptions(225840, null, null, false, false), CommandLineOptions.Parse(["-appid", "225840"]));
        Equal(true, CommandLineOptions.Parse(["-help"]).ShowHelp);
        Equal(true, CommandLineOptions.Parse(["-appid", "bad", "-?"]).ShowHelp);
    }),
    ("CLI rejects invalid arguments", _ =>
    {
        Throws<ArgumentException>(() => CommandLineOptions.Parse([]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-gamedir", "C:/Games"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "-moddir", "valve"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "abc"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "-5"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "0"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "70", "-unknown", "x"]));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "70", "valve"]));
    }),
    ("CLI resolves known games, custom mods and Steam directories", fixture =>
    {
        var steam = fixture.Directory("steam/Sven Co-op");
        Equal(new InstallTarget("Sven Co-op", steam, "svencoop"),
            CommandLineOptions.Parse(["-appid", "225840"]).ResolveTarget(_ => steam));
        Equal(new InstallTarget("Half-Life", steam, "valve"),
            CommandLineOptions.Parse(["-appid", "70"]).ResolveTarget(_ => steam));
        Equal(new InstallTarget("Half-Life Echoes", steam, "ECHOES"),
            CommandLineOptions.Parse(["-appid", "70", "-moddir", "ECHOES"]).ResolveTarget(_ => steam));
        Equal(new InstallTarget("mymod", steam, "mymod"),
            CommandLineOptions.Parse(["-appid", "70", "-moddir", "mymod"]).ResolveTarget(_ => steam));
        Func<uint, string?> unused = _ => throw new Exception("Steam must not be queried when -gamedir is given");
        Equal(new InstallTarget("Half-Life", fixture.Path("game"), "valve"),
            CommandLineOptions.Parse(["-appid", "70", "-gamedir", fixture.Path("game/../game")]).ResolveTarget(unused));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "12345", "-gamedir", steam]).ResolveTarget(unused));
        Throws<ArgumentException>(() => CommandLineOptions.Parse(["-appid", "70"]).ResolveTarget(_ => null));
    })
};

foreach (var test in cases)
{
    using var fixture = new Fixture();
    test.Run(fixture);
    Console.WriteLine($"PASS: {test.Name}");
}

if (args.Length == 2 && args[0] == "--payload")
{
    var source = System.IO.Path.GetFullPath(args[1]);
    foreach (var mod in new[] { "svencoop", "valve" })
    {
        using var fixture = new Fixture();
        var game = fixture.Game(mod);
        // Default aggregate packages leave Steam API to the game. Older/custom
        // packages may still supply it, in which case normal DLL copying applies.
        var hostSteamApi = fixture.Write("game/steam_api.dll", "host Steam API");
        var payloadSteamApi = System.IO.Path.Combine(source, "steam_api.dll");
        var expectedSteamApi = File.ReadAllBytes(File.Exists(payloadSteamApi) ? payloadSteamApi : hostSteamApi);
        var launcher = InstallPayload.Install(source, game, mod, true, true);
        Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(System.IO.Path.Combine(source, "MetaHook.exe")))),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(launcher))));
        Exists(fixture.Path($"game/{mod}/metahook/plugins/BetterSpray.dll"));
        Exists(fixture.Path($"game/{mod}/metahook/gamedata/betterspray/index.json"));
        Exists(fixture.Path($"game/{mod}/metahook/configs/plugins.lst"));
        Exists(fixture.Path("game/libcurl.dll"));
        Equal(Convert.ToHexString(expectedSteamApi), Convert.ToHexString(File.ReadAllBytes(hostSteamApi)));
        Console.WriteLine($"PASS: installed the actual CMake payload into a simulated {mod} game");
    }
}

Console.WriteLine($"Passed {cases.Length} file-system regression cases.");

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; actual {actual}");
}
static void Exists(string path)
{
    if (!File.Exists(path)) throw new Exception($"Missing file: {path}");
}
static void Absent(string path)
{
    if (File.Exists(path) || System.IO.Directory.Exists(path)) throw new Exception($"Unexpected path: {path}");
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

sealed class Fixture : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "metahook-installer-tests-" + Guid.NewGuid());
    public Fixture() => System.IO.Directory.CreateDirectory(root);
    public string Path(string relative) => System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
    public string Directory(string relative)
    {
        var path = Path(relative);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }
    public string Write(string relative, string value)
    {
        var path = Path(relative);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
        return path;
    }
    public string Game(string mod)
    {
        Write($"game/{mod}/liblist.gam", "fixture");
        return Path("game");
    }
    public string Snapshot() => string.Join("\n", System.IO.Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .OrderBy(path => path).Select(path => path + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))));
    public void CopyCli()
    {
        var output = System.IO.Path.GetDirectoryName(typeof(CommandLineOptions).Assembly.Location)!;
        Directory("package");
        foreach (var file in System.IO.Directory.GetFiles(output))
            File.Copy(file, Path("package/" + System.IO.Path.GetFileName(file)), true);
    }
    public (int ExitCode, string Output, string Error) RunCli(params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Directory("working"), UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path("package/MetahookInstallerCLI.dll"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
    public void NormalEngine()
    {
        // Minimal PE signature sufficient for the installer's engine classifier.
        var bytes = new byte[128];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        bytes[0x3c] = 64;
        bytes[64] = (byte)'P';
        bytes[65] = (byte)'E';
        File.WriteAllBytes(Path("game/hw.dll"), bytes);
    }
    public string Payload(bool normal = true, bool blob = true)
    {
        if (normal) Write("package/install/output/MetaHook.exe", "normal");
        if (blob) Write("package/install/output/MetaHook_blob.exe", "blob");
        foreach (var (file, value) in new[]
        {
            ("libcurl.dll", "curl"), ("steam_api.dll", "steam"), ("SDL2.dll", "SDL2"), ("SDL3.dll", "SDL3"),
            ("SteamAppsLocation.exe", "tool"), ("MetaHook.pdb", "symbols"),
            ("svencoop/metahook/plugins/Plugin.dll", "plugin"),
            ("svencoop/metahook/configs/plugins_svencoop.lst", "fixture"),
            ("svencoop/metahook/configs/plugins_goldsrc.lst", "fixture"),
            ("svencoop_downloads/resource.dat", "resource"), ("platform/resource.dat", "platform"),
            ("valve/custom.dat", "custom"), ("valve_hidpi/resource.dat", "hidpi")
        }) Write("package/install/output/" + file, value);
        return Path("package/install/output");
    }
    public void Dispose() => System.IO.Directory.Delete(root, true);
}
