using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Phonexis.Services;
using Phonexis.ViewModels;

namespace Phonexis;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureDirectories();

        var services = new ServiceCollection();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IApiKeyStore, ApiKeyStore>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IYouTubeService, YouTubeService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<Services.Downloads.DownloaderRegistry>();
        services.AddSingleton<MainViewModel>();
        Services = services.BuildServiceProvider();

        var log = Services.GetRequiredService<ILogService>();
        log.Info($"Phonexis {AppInfo.Version} starting");

        DispatcherUnhandledException += OnUnhandledException;

        Services.GetRequiredService<ICacheService>().Initialize();

        var settings = Services.GetRequiredService<ISettingsService>().Settings;
        I18n.Instance.SetLanguage(settings.Language);
        Services.GetRequiredService<IThemeService>().Apply(settings.Theme);

        var window = new MainWindow(Services.GetRequiredService<MainViewModel>(),
                                    Services.GetRequiredService<ISettingsService>());
        MainWindow = window;
        window.Show();

        // "Open with Phonexis" / double-click on a .phonexis or .txt list.
        var file = e.Args.FirstOrDefault(File.Exists);
        if (file != null)
            window.Dispatcher.BeginInvoke(() => Services.GetRequiredService<MainViewModel>().LoadFileFromPath(file));
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Services.GetService<ILogService>()?.Error("Unhandled exception", e.Exception);
        MessageBox.Show(e.Exception.Message, "Phonexis", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

public static class AppInfo
{
    public const string Version = "3.0.0";
    public const string GitHubUrl = "https://github.com/TX-Breaker/Phonexis";
    public const string ApiKeyHelpUrl = "https://developers.google.com/youtube/v3/getting-started";
}
