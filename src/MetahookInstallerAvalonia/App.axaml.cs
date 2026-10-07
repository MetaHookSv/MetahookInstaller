using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using MetahookInstallerAvalonia.ViewModels;
using MetahookInstallerAvalonia.Views;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MetahookInstallerAvalonia;

public partial class App : Application
{
    // Ursa only ships en-US / fr-FR / zh-CN locale resources. Map the effective
    // culture onto one of those: Chinese keeps zh-CN, French keeps fr-FR, and
    // everything else (including the "no lang file" fallback) uses en-US so the
    // built-in control strings start out in English rather than Ursa's zh-CN default.
    private static string NormalizeUrsaLocale(string? lang)
    {
        if (string.IsNullOrEmpty(lang))
        {
            return "en-US";
        }
        if (lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-CN";
        }
        if (lang.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            return "fr-FR";
        }
        return "en-US";
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var settingPath = Path.Combine(".", "lang");
        string? lang = File.Exists(settingPath) ? File.ReadAllText(settingPath).Trim() : null;
        if (!string.IsNullOrEmpty(lang))
        {
            Lang.Resources.Culture = new CultureInfo(lang);
        }

        // Keep Ursa's built-in control strings (PopConfirm / MessageBox buttons,
        // etc.) in sync with the installer language. Without this the theme keeps
        // its zh-CN default and shows Chinese buttons in English mode.
        var ursaTheme = Styles.OfType<Ursa.Themes.Semi.SemiTheme>().FirstOrDefault();
        if (ursaTheme != null)
        {
            ursaTheme.Locale = new CultureInfo(NormalizeUrsaLocale(lang));
        }

        var themePath = Path.Combine(".", "theme");
        if (File.Exists(themePath))
        {
            RequestedThemeVariant = File.ReadAllText(themePath).Trim() switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
