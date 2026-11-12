using System.Windows;
using Microsoft.Win32;
using Phonexis.Models;

namespace Phonexis.Services;

public interface IThemeService
{
    AppTheme Current { get; }
    bool IsDarkActive { get; }
    void Apply(AppTheme theme);
    event EventHandler? ThemeChanged;
}

/// <summary>Swaps the color resource dictionary at runtime (light / dark / follow system).</summary>
public class ThemeService : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.System;
    public bool IsDarkActive { get; private set; }

    public event EventHandler? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        Current = theme;
        var dark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => SystemPrefersDark()
        };
        IsDarkActive = dark;

        var uri = new Uri(dark ? "Themes/Colors.Dark.xaml" : "Themes/Colors.Light.xaml", UriKind.Relative);
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        // The color dictionary is always merged at index 0 (see App.xaml).
        dictionaries[0] = new ResourceDictionary { Source = uri };
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }
}
