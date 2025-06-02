using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using Phonexis.Services;
using Phonexis.ViewModels;
using Phonexis.Views;

namespace Phonexis
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public App()
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Registra i servizi per la dependency injection
            services.AddSingleton<IYouTubeApiService, YouTubeApiService>();
            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<IStateService, StateService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IAutoSaveService, AutoSaveService>();
            
            // Registra i ViewModels
            services.AddTransient<MainViewModel>();
            
            // Registra la finestra principale
            services.AddTransient<MainWindow>();
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
                    var youTubeApiService = _serviceProvider.GetService<IYouTubeApiService>();
                    var wizard = new WelcomeWizard(youTubeApiService!, databaseService);
                    
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
                var mainWindow = _serviceProvider.GetService<MainWindow>();
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
                var loggingService = _serviceProvider.GetService<ILoggingService>();
                
                // Dispose the service provider which will dispose all services
                _serviceProvider.Dispose();
                
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
