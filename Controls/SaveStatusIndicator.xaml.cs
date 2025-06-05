using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Phonexis.Controls
{
    /// <summary>
    /// Indicatore visivo dello stato di salvataggio automatico
    /// </summary>
    public partial class SaveStatusIndicator : UserControl
    {
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(SaveStatus), typeof(SaveStatusIndicator),
                new PropertyMetadata(SaveStatus.Idle, OnStatusChanged));

        public static readonly DependencyProperty StatusMessageProperty =
            DependencyProperty.Register("StatusMessage", typeof(string), typeof(SaveStatusIndicator),
                new PropertyMetadata("Pronto"));

        public SaveStatus Status
        {
            get { return (SaveStatus)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        public string StatusMessage
        {
            get { return (string)GetValue(StatusMessageProperty); }
            set { SetValue(StatusMessageProperty, value); }
        }

        public SaveStatusIndicator()
        {
            InitializeComponent();
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SaveStatusIndicator indicator)
            {
                indicator.UpdateStatus((SaveStatus)e.NewValue);
            }
        }

        private void UpdateStatus(SaveStatus status)
        {
            // Ferma tutte le animazioni in corso
            BeginStoryboard(Resources["ResetAnimation"] as Storyboard);

            switch (status)
            {
                case SaveStatus.Idle:
                    StatusText.Text = "Pronto";
                    StatusMessage = "Pronto per il salvataggio";
                    break;

                case SaveStatus.Saving:
                    StatusText.Text = "Salvando...";
                    StatusMessage = "Salvataggio in corso...";
                    BeginStoryboard(Resources["SavingAnimation"] as Storyboard);
                    break;

                case SaveStatus.Saved:
                    StatusText.Text = "Salvato";
                    StatusMessage = $"Ultimo salvataggio: {DateTime.Now:HH:mm:ss}";
                    BeginStoryboard(Resources["SavedAnimation"] as Storyboard);
                    
                    // Torna allo stato idle dopo 3 secondi
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        if (Status == SaveStatus.Saved) // Solo se non è cambiato nel frattempo
                        {
                            Status = SaveStatus.Idle;
                        }
                    };
                    timer.Start();
                    break;

                case SaveStatus.Error:
                    StatusText.Text = "Errore";
                    StatusMessage = "Errore durante il salvataggio";
                    BeginStoryboard(Resources["ErrorAnimation"] as Storyboard);
                    
                    // Torna allo stato idle dopo 5 secondi
                    var errorTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    errorTimer.Tick += (s, e) =>
                    {
                        errorTimer.Stop();
                        if (Status == SaveStatus.Error) // Solo se non è cambiato nel frattempo
                        {
                            Status = SaveStatus.Idle;
                        }
                    };
                    errorTimer.Start();
                    break;
            }
        }
    }

    /// <summary>
    /// Stati possibili dell'indicatore di salvataggio
    /// </summary>
    public enum SaveStatus
    {
        Idle,       // Pronto
        Saving,     // Salvataggio in corso
        Saved,      // Salvato con successo
        Error       // Errore durante il salvataggio
    }
}