using MetahookInstaller;
using MetahookInstaller.CLI;

var cases = new (string Name, Action<Fixture> Run)[]
{
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
        var launcher = InstallPayload.Install(source, game, mod, true, true);
        Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(System.IO.Path.Combine(source, "MetaHook.exe")))),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(launcher))));
        Exists(fixture.Path($"game/{mod}/metahook/plugins/BetterSpray.dll"));
        Exists(fixture.Path($"game/{mod}/metahook/gamedata/betterspray/index.json"));
        Exists(fixture.Path($"game/{mod}/metahook/configs/plugins.lst"));
        Exists(fixture.Path("game/libcurl.dll"));
        Exists(fixture.Path("game/steam_api.dll"));
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
