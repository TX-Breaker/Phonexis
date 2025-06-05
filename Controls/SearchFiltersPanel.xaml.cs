using System;
using System.Windows;
using System.Windows.Controls;
using Phonexis.Models;

namespace Phonexis.Controls
{
    /// <summary>
    /// Controllo per il pannello dei filtri di ricerca
    /// </summary>
    public partial class SearchFiltersPanel : UserControl
    {
        /// <summary>
        /// Evento scatenato quando i filtri cambiano
        /// </summary>
        public event EventHandler<SearchFilters>? FiltersChanged;

        /// <summary>
        /// Filtri correnti
        /// </summary>
        public SearchFilters CurrentFilters { get; private set; }

        public SearchFiltersPanel()
        {
            InitializeComponent();
            CurrentFilters = new SearchFilters();
            InitializeComboBoxes();
        }

        /// <summary>
        /// Inizializza i ComboBox con i valori predefiniti
        /// </summary>
        private void InitializeComboBoxes()
        {
            DurationComboBox.SelectedIndex = 0; // Any
            DateComboBox.SelectedIndex = 0;     // Any
            QualityComboBox.SelectedIndex = 0;  // Any
        }

        /// <summary>
        /// Gestore per il cambio del filtro durata
        /// </summary>
        private void DurationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DurationComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                CurrentFilters.Duration = tag switch
                {
                    "Short" => DurationFilter.Short,
                    "Medium" => DurationFilter.Medium,
                    "Long" => DurationFilter.Long,
                    _ => DurationFilter.Any
                };
                OnFiltersChanged();
            }
        }

        /// <summary>
        /// Gestore per il cambio del filtro data
        /// </summary>
        private void DateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                CurrentFilters.Date = tag switch
                {
                    "LastHour" => DateFilter.LastHour,
                    "Today" => DateFilter.Today,
                    "ThisWeek" => DateFilter.ThisWeek,
                    "ThisMonth" => DateFilter.ThisMonth,
                    "ThisYear" => DateFilter.ThisYear,
                    _ => DateFilter.Any
                };
                OnFiltersChanged();
            }
        }

        /// <summary>
        /// Gestore per il cambio del filtro qualità
        /// </summary>
        private void QualityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QualityComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                CurrentFilters.Quality = tag switch
                {
                    "High" => QualityFilter.High,
                    "Medium" => QualityFilter.Medium,
                    "Low" => QualityFilter.Low,
                    _ => QualityFilter.Any
                };
                OnFiltersChanged();
            }
        }

        /// <summary>
        /// Scatena l'evento FiltersChanged
        /// </summary>
        private void OnFiltersChanged()
        {
            FiltersChanged?.Invoke(this, CurrentFilters);
        }

        /// <summary>
        /// Resetta tutti i filtri ai valori predefiniti
        /// </summary>
        public void ResetFilters()
        {
            CurrentFilters = new SearchFilters();
            InitializeComboBoxes();
            OnFiltersChanged();
        }

        /// <summary>
        /// Imposta i filtri programmaticamente
        /// </summary>
        public void SetFilters(SearchFilters filters)
        {
            CurrentFilters = filters ?? new SearchFilters();
            
            // Aggiorna i ComboBox
            DurationComboBox.SelectedIndex = (int)CurrentFilters.Duration;
            DateComboBox.SelectedIndex = (int)CurrentFilters.Date;
            QualityComboBox.SelectedIndex = (int)CurrentFilters.Quality;
        }
    }
}