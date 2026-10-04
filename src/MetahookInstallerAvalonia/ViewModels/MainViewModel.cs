using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MetahookInstaller;
using MetahookInstallerAvalonia.Handler;
using MetahookInstallerAvalonia.Lang;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Ursa.Controls;
using Notification = Ursa.Controls.Notification;
using Path = System.IO.Path;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace MetahookInstallerAvalonia.ViewModels;

public class MainViewModel : ViewModelBase
{
    public WindowNotificationManager? NotificationManager { get; set; }
    public WindowToastManager? ToastManager { get; set; }
    #region Page 1
    private void InstallMod(string basePath)
    {
        if (Selected == null || Selected.GamePath == null || !MetahookSetup.IsValidInstallTarget(Selected.GamePath, Selected.Directory))
        {
            MessageBox.ShowAsync(Resources.InvalidInstallPath, Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
            return;
        }
        string modName = Selected.Directory;
        string gamePath = Selected.GamePath;
        var targetMetaHookPath = MetahookSetup.Install(basePath, gamePath, modName);

        // 8. 为 targetMetaHookPath 创建快捷方式至当前MetahookInstaller.exe所在目录
        MetahookSetup.CreateShortcut(Path.GetFullPath("."), Selected.Name, targetMetaHookPath, gamePath, modName);
        NotificationManager?.Show(new Notification(
                            Resources.Success,
                            Resources.InstallDone),
                        NotificationType.Success,
                        new TimeSpan(0, 0, 5), true,
                        classes: ["Light"]);
        _pluginListInitialized = false;
        this.RaisePropertyChanged(nameof(EditorUsable));
    }
    private readonly ICommand _install;
    public ICommand InstallCommand => _install;

    private void UninstallMod()
    {
        if (Selected == null || Selected.GamePath == null || !MetahookSetup.IsValidInstallTarget(Selected.GamePath, Selected.Directory))
        {
            MessageBox.ShowAsync(Resources.InvalidInstallPath, Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
            return;
        }
        foreach (var failure in MetahookSetup.Uninstall(Selected.GamePath, Selected.Directory))
        {
            NotificationManager?.Show(new Notification(
                    Resources.Warning,
                    string.Format(Resources.DeleteFailed, failure.Path, failure.Message)),
                NotificationType.Warning,
                new TimeSpan(0, 0, 5), true,
                classes: ["Light"]);
        }
        // Delete desktop shortcut
        MetahookSetup.DeleteShortcut(Path.GetFullPath("."), Selected.Name);

        NotificationManager?.Show(new Notification(
                                Resources.Success,
                                Resources.UninstallDone
                            ),
                            NotificationType.Success,
                            new TimeSpan(0, 0, 5), true,
                            classes: ["Light"]);
        _pluginListInitialized = false;
        this.RaisePropertyChanged(nameof(EditorUsable));
    }
    private readonly ICommand _uninstall;
    public ICommand UninstallCommand => _uninstall;

    public class ModInfo(string name, string directory, uint appid, string path)
    {
        private string _name = name;
        private string _directory = directory;
        private uint _appid = appid;
        private string? _path = path;
        private readonly Bitmap? _icon = (!string.IsNullOrEmpty(Path.Combine(path, directory, "game.ico")) &&
            File.Exists(Path.Combine(path, directory, "game.ico"))) ? new Bitmap(Path.Combine(path, directory, "game.ico")) : null;
        private bool _readonly = true;

        public string Name { get => _name; set => _name = value; }
        public string Directory { get => _directory; set => _directory = value; }
        public uint AppID { get => _appid; set => _appid = value; }
        public string? GamePath { get => _path; set => _path = value; }
        public string InstallPath { get => Path.Combine(_path ?? "", _directory); }
        public Bitmap? GameIcon => _icon;
        public bool ReadOnly { get => _readonly; set => _readonly = value; }
    }
    private readonly ObservableCollection<ModInfo> _modInfos = [];
    private ModInfo? _selected = null;
    public ObservableCollection<ModInfo> ModInfos => _modInfos;
    public ModInfo? Selected
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }
    #endregion


    #region Page 2
    public class PluginInfoComparer : IEqualityComparer<PluginInfo>
    {
        public bool Equals(PluginInfo? x, PluginInfo? y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;
            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        }
        public int GetHashCode(PluginInfo obj)
        {
            return obj?.Name?.ToLowerInvariant().GetHashCode() ?? 0;
        }
    }
    private readonly ObservableCollection<PluginInfo> _plugins = [];
    public ObservableCollection<PluginInfo> Plugins => _plugins;
    private readonly ObservableCollection<PluginInfo> _avaliable = [];
    public ObservableCollection<PluginInfo> Avaliable => _avaliable;
    private PluginInfo? _selectedPlugin = null;
    private PluginInfo? _selectedAvaliable = null;
    public PluginInfo? SelectedPlugin
    {
        get => _selectedPlugin;
        set => this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
    }
    public PluginInfo? SelectedAvaliable
    {
        get => _selectedAvaliable;
        set => this.RaiseAndSetIfChanged(ref _selectedAvaliable, value);
    }
    public bool EditorUsable => IsEditorUsable();

    private int _selectedTabIndex;
    private bool _pluginListInitialized = false;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    private readonly ICommand _toAvaliable;
    private readonly ICommand _toPlugins;
    public ICommand ToAvaliableCommand => _toAvaliable;
    public ICommand ToPluginsCommand => _toPlugins;

    public void RecaculatePluginIndex()
    {
        for (var i = 0; i < _plugins.Count; i++)
        {
            _plugins[i].Index = i + 1;
        }
    }

    private void InsertAvailablePlugin(PluginInfo plugin)
    {
        var index = 0;
        while (index < _avaliable.Count &&
               StringComparer.OrdinalIgnoreCase.Compare(_avaliable[index].Name, plugin.Name) < 0)
        {
            index++;
        }
        _avaliable.Insert(index, plugin);
    }

    private bool IsEditorUsable()
    {
        if (Selected == null || Selected.GamePath == null)
            return false;
        string _gamePath = Selected.GamePath;
        string _modName = Selected.Directory;
        var pluginsLstPath = Path.Combine(_gamePath, _modName, "metahook", "configs", "plugins.lst");
        var pluginsDir = Path.Combine(_gamePath, _modName, "metahook", "plugins");
        if (!File.Exists(pluginsLstPath) || !Directory.Exists(pluginsDir))
            return false;
        return true;
    }

    public bool InitPluginList()
    {
        _plugins.Clear();
        _avaliable.Clear();
        if (Selected == null || Selected.GamePath == null)
        {
            MessageBox.ShowAsync(Resources.SelectFirst, Resources.Warning, MessageBoxIcon.Warning, MessageBoxButton.OK);
            return false;
        }
        string _gamePath = Selected.GamePath;
        string _modName = Selected.Directory;
        var pluginsLstPath = Path.Combine(_gamePath, _modName, "metahook", "configs", "plugins.lst");
        var pluginsDir = Path.Combine(_gamePath, _modName, "metahook", "plugins");

        if (!File.Exists(pluginsLstPath) || !Directory.Exists(pluginsDir))
        {
            MessageBox.ShowAsync(Resources.SelectFirst, Resources.Warning, MessageBoxIcon.Warning, MessageBoxButton.OK);
            return false;
        }

        var lines = File.ReadAllLines(pluginsLstPath);
        List<PluginInfo> ps = [];
        foreach (var line in lines)
        {
            if (line != null && !string.IsNullOrWhiteSpace(line))
            {
                var plugininfo = new PluginInfo(line.TrimStart(';').Trim(), !line.StartsWith(';'));
                ps.Add(plugininfo);
            }
        }
        ps = [.. ps.Distinct(new PluginInfoComparer())];

        List<PluginInfo> aps = [];
        if (Directory.Exists(pluginsDir))
        {
            foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll"))
            {
                var pluginName = Path.GetFileName(dll);
                if (pluginName.EndsWith("_AVX2.dll"))
                {
                    pluginName = pluginName.Replace("_AVX2.dll", ".dll");
                }
                var plugininfo = new PluginInfo(pluginName.Trim(), false);
                aps.Add(plugininfo);
            }
        }
        aps = [.. aps.Distinct(new PluginInfoComparer()).Where(a => !ps.Any(p => a.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))];

        foreach (var p in ps)
        {
            _plugins.Add(p);
        }
        foreach (var p in aps)
        {
            InsertAvailablePlugin(p);
        }
        RecaculatePluginIndex();
        NotificationManager?.Show(new Notification(
                            Resources.Success,
                            Resources.ResetDone),
                        NotificationType.Success,
                        new TimeSpan(0, 0, 5), true,
                        classes: ["Light"]);
        return true;
    }
    private void SavePluginList()
    {
        if (Selected == null || Selected.GamePath == null)
        {
            MessageBox.ShowAsync(Resources.SelectFirst, Resources.Warning, MessageBoxIcon.Warning, MessageBoxButton.OK);
            return;
        }
        string _gamePath = Selected.GamePath;
        string _modName = Selected.Directory;
        var pluginsLstPath = Path.Combine(_gamePath, _modName, "metahook", "configs", "plugins.lst");

        if (!File.Exists(pluginsLstPath))
        {
            MessageBox.ShowAsync(Resources.SelectFirst, Resources.Warning, MessageBoxIcon.Warning, MessageBoxButton.OK);
            return;
        }

        using StreamWriter sw = new(pluginsLstPath);
        foreach (var p in _plugins)
        {
            string text = $"{(p.Enabled ? "" : ';')}{p.Name}";
            sw.WriteLine(text);
        }
        NotificationManager?.Show(new Notification(
                            Resources.Success,
                            Resources.SaveDone),
                        NotificationType.Success,
                        new TimeSpan(0, 0, 5), true,
                        classes: ["Light"]);
    }
    private readonly ICommand _save;
    public ICommand SaveCommand => _save;

    private readonly ICommand _reset;
    public ICommand ResetCommand => _reset;
    #endregion

    private readonly ICommand _changeLanguage;
    public ICommand ChangeLanguageCommand => _changeLanguage;

    private readonly ICommand _changeTheme;
    public ICommand ChangeThemeCommand => _changeTheme;

    private readonly ICommand _toastWarning;
    public ICommand ToastWarningCommand => _toastWarning;

    private readonly ICommand _openFolder;
    public ICommand OpenFolderCommand => _openFolder;

    public MainViewModel()
    {
        #region Setup Games
        var steamLibrary = new SteamLibrary();
        foreach (var game in KnownGames.All)
        {
            if (steamLibrary.FindGameDirectory(game.AppId) is string path)
            {
                var info = new ModInfo(game.Name, game.ModDirectory, game.AppId, path);
                if (Directory.Exists(info.InstallPath))
                {
                    _modInfos.Add(info);
                }
            }
        }
        var custom = new ModInfo(Resources.CustomGame, "", 0, "")
        {
            ReadOnly = false
        };
        _modInfos.Add(custom);
        _selected = _modInfos.FirstOrDefault();
        this.WhenAnyValue(x => x.Selected)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(EditorUsable));
                _pluginListInitialized = false;
            });
        this.WhenAnyValue(x => x.SelectedTabIndex)
            .Where(idx => idx == 1) // 第二个 Tab 的索引为 1
            .Subscribe(_ =>
            {
                if (!_pluginListInitialized)
                {
                    InitPluginList();
                    _pluginListInitialized = true;
                }
            });
        #endregion

        #region Setup Commands
        _install = new Command(
            _ =>
            {
                try
                {
                    var sourcePath = InstallPayload.FindApplicationSourceDirectory();
                    if (string.IsNullOrEmpty(sourcePath))
                    {
                        MessageBox.ShowAsync(Resources.BuildDirectoryNotFound, Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
                        return;
                    }
                    InstallMod(sourcePath);
                }
                catch (IOException ex)
                {
                    MessageBox.ShowAsync($"{Resources.InstallationFailed}\n\n{ex.Message}", Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.ShowAsync($"{Resources.InstallationFailed}\n\n{ex.Message}", Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
                }
                catch (Exception ex)
                {
                    MessageBox.ShowAsync($"{Resources.InstallationFailed}\n\n{ex.Message}", Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
                }
            },
            _ => true
        );
        _uninstall = new Command(
            _ =>
            {
                try
                {
                    UninstallMod();
                }
                catch (IOException ex)
                {
                    MessageBox.ShowAsync($"{Resources.UninstallationFailed}\n\n{ex.Message}", Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.ShowAsync($"{Resources.UninstallationFailed}\n\n{ex.Message}", Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
                }
                catch (Exception ex)
                {
                    MessageBox.ShowAsync($"{Resources.UninstallationFailed}\n\n{ex.Message}", Resources.CriticalError, MessageBoxIcon.Error, MessageBoxButton.OK);
                }
            },
            _ => true
        );
        _toAvaliable = new Command(
            _ =>
            {
                if (SelectedPlugin is PluginInfo plugin)
                {
                    Plugins.Remove(plugin);
                    InsertAvailablePlugin(plugin);
                    RecaculatePluginIndex();
                }
            },
            _ => true
         );
        _toPlugins = new Command(
            _ =>
            {
                if (SelectedAvaliable is PluginInfo plugin)
                {
                    Avaliable.Remove(plugin);
                    Plugins.Add(plugin);
                    RecaculatePluginIndex();
                }
            },
            _ => true
        );
        _save = new Command(
            _ =>
            {
                SavePluginList();
            },
            _ => true
            );
        _reset = new Command(
            _ =>
            {
                InitPluginList();
            },
            _ => true
        );
        _changeLanguage = new Command(
           obj =>
           {
               if (obj is string lang)
               {
                   var settingPath = Path.Combine(".", "lang");
                   using StreamWriter sw = new(settingPath);
                   sw.Write(lang);
                   sw.Flush();
                   string? currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                   if (currentExePath == null || string.IsNullOrEmpty(currentExePath))
                   {
                       currentExePath = Environment.GetCommandLineArgs()[0];
                   }
                   string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
                   string arguments = string.Join(" ", args);
                   var startInfo = new ProcessStartInfo
                   {
                       FileName = currentExePath,
                       Arguments = arguments,
                       UseShellExecute = true
                   };
                   Process.Start(startInfo);
                   Environment.Exit(0);
               }
           },
           _ => true
        );
        _changeTheme = new Command(
           obj =>
           {
               if (obj is not string theme)
               {
                   return;
               }
               if (Application.Current is { } app)
               {
                   app.RequestedThemeVariant = theme switch
                   {
                       "Light" => ThemeVariant.Light,
                       "Dark" => ThemeVariant.Dark,
                       _ => ThemeVariant.Default,
                   };
               }
               var settingPath = Path.Combine(".", "theme");
               File.WriteAllText(settingPath, theme);
           },
           _ => true
        );
        _toastWarning = new Command(
           arg =>
           {
               if (arg is not string msg)
                   return;
               ToastManager?.Show(new Toast(
                   msg),
                   showIcon: true,
                   showClose: false,
                   type: NotificationType.Warning,
                   expiration: new TimeSpan(0, 0, 3),
                   classes: ["Light"]
               );
           },
           _ => true
        );
        _openFolder = new Command(
            arg =>
            {
                if (arg is not string target || Selected == null)
                    return;
                if (target == "{SOURCE}")
                    target = InstallPayload.FindApplicationSourceDirectory() ?? string.Empty;
                target = target.Replace("{GAME}", Selected.GamePath);
                target = target.Replace("{INSTALLED}", Selected.InstallPath);
                if (!Directory.Exists(target))
                {
                    ToastManager?.Show(new Toast(
                       string.Format(Resources.FileNotFound, target)),
                       showIcon: true,
                       showClose: false,
                       type: NotificationType.Warning,
                       expiration: new TimeSpan(0, 0, 3),
                       classes: ["Light"]
                   );
                    return;
                }
                Process.Start("explorer.exe", Path.GetFullPath(target));
            },
            _ => true
        );
        #endregion
    }
}
