using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Windows;
using Phonexis.Services;
using Phonexis.ViewModels;
using Phonexis.Views;

namespace Phonexis
{
    /// <summary>
    /// Classe principale dell'applicazione con dependency injection professionale
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;
        private ILogger<App>? _logger;

        public App()
        {
            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();
                
                // Inizializza il logger
                _logger = _serviceProvider.GetService<ILogger<App>>();
                _logger?.LogInformation("Application initialized successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Critical error during application initialization: {ex.Message}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Configurazione
            var configuration = BuildConfiguration();
            services.AddSingleton<IConfiguration>(configuration);
            
            // Inizializza Config con la configurazione
            Config.Initialize(configuration);
            Config.ValidateConfiguration();

            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
                builder.AddDebug();
            });

            // Servizi core (Singleton per stato condiviso)
            services.AddSingleton<IYouTubeApiService, YouTubeApiService>();
            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<IStateService, StateService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IAutoSaveService, AutoSaveService>();
            
            // ViewModels (Transient per istanze fresche)
            services.AddTransient<MainViewModel>();
            
            // Views (Transient per istanze fresche)
            services.AddTransient<MainWindow>();
            
            // Note: ApiKeyManager è una classe statica, non serve registrarla
        }

        private static IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Inizializza i servizi
                var loggingService = _serviceProvider.GetService<ILoggingService>();
                if (loggingService == null)
                {
                    MessageBox.Show("Errore critico: Impossibile risolvere il servizio di logging.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                loggingService.Initialize();
                loggingService.Info("Applicazione avviata");

                var databaseService = _serviceProvider.GetService<IDatabaseService>();
                if (databaseService == null)
                {
                    loggingService.Critical("Errore critico: Impossibile risolvere il servizio database.");
                    MessageBox.Show("Errore critico: Impossibile risolvere il servizio database.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                databaseService.Initialize();

                // Controlla se mostrare il wizard di configurazione
                if (WelcomeWizard.ShouldShowWizard())
                {
                    var youTubeApiService = _serviceProvider?.GetService<IYouTubeApiService>();
                    if (youTubeApiService == null)
                    {
                        loggingService.Critical("Errore critico: Impossibile risolvere il servizio YouTube API.");
                        MessageBox.Show("Errore critico: Impossibile risolvere il servizio YouTube API.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                        return;
                    }
                    
                    var wizard = new WelcomeWizard(youTubeApiService, databaseService);
                    
                    var wizardResult = wizard.ShowDialog();
                    if (wizardResult != true)
                    {
                        // L'utente ha chiuso il wizard senza completarlo
                        Shutdown();
                        return;
                    }
                    
                    loggingService.Info("Wizard di configurazione completato");
                }

                // Mostra la finestra principale
                var mainWindow = _serviceProvider?.GetService<MainWindow>();
                if (mainWindow == null)
                {
                    loggingService.Critical("Errore critico: Impossibile risolvere la finestra principale.");
                    MessageBox.Show("Errore critico: Impossibile risolvere la finestra principale.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante l'avvio dell'applicazione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Get the logging service before disposing the service provider
                var loggingService = _serviceProvider?.GetService<ILoggingService>();
                
                // Dispose the service provider which will dispose all services
                _serviceProvider?.Dispose();
                
                // Explicitly dispose the logging service if it's IDisposable
                if (loggingService is IDisposable disposableLogger)
                {
                    disposableLogger.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Can't use logging service here as we're shutting down
                Console.WriteLine($"Error during application shutdown: {ex.Message}");
            }
            
            base.OnExit(e);
        }
    }
}
