using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Phonexis.Helpers; // Already has LocalizationHelper namespace
using Phonexis.Models;

namespace Phonexis.Controls
{
    /// <summary>
    /// Logica di interazione per VideoResultControl.xaml
    /// </summary>
    public partial class VideoResultControl : UserControl
    {
        private SearchResult _searchResult = null!; // Initialized

        public event EventHandler<SearchResult>? VideoSelected = null; // Made nullable and initialized
        
        public VideoResultControl()
        {
            InitializeComponent();
            
            // Aggiungi gestori eventi
            SelectButton.Click += SelectButton_Click;
            OpenButton.Click += OpenButton_Click;

            // Set localized strings
            SetLocalizedStrings();
        }
        
        /// <summary>
        /// Imposta il risultato di ricerca da visualizzare
        /// </summary>
        /// <param name="searchResult">Il risultato di ricerca</param>
        public async Task SetSearchResult(SearchResult searchResult)
        {
            _searchResult = searchResult;
            
            // Imposta i dati del video
            TitleTextBlock.Text = searchResult.Title;
            ChannelTextBlock.Text = string.Format(LocalizationHelper.GetString("ChannelLabel"), searchResult.ChannelTitle);
            PublishedTextBlock.Text = string.Format(LocalizationHelper.GetString("PublishedLabel"), searchResult.PublishedAt.ToString("dd/MM/yyyy"));
            // Rimosso Durata e Visualizzazioni poiché l'API di YouTube non fornisce questi valori
            StatsTextBlock.Text = string.Empty;
            DescriptionTextBlock.Text = searchResult.Description;
            
            // Imposta l'indicatore della fonte
            SourceTextBlock.Text = searchResult.SourceDescription;
            
            // Imposta il colore dell'indicatore in base alla fonte
            if (searchResult.IsDummy)
            {
                SourceIndicator.Background = new SolidColorBrush(Colors.Orange); // Arancione per i risultati di test
            }
            else if (searchResult.IsFromCache)
            {
                SourceIndicator.Background = new SolidColorBrush(Colors.Green); // Verde per i risultati dalla cache
            }
            else
            {
                SourceIndicator.Background = new SolidColorBrush(Colors.Blue); // Blu per i risultati dall'API
            }
            
            // Carica la miniatura
            if (!string.IsNullOrEmpty(searchResult.ThumbnailUrl))
            {
                await LoadThumbnail(searchResult.ThumbnailUrl);
            }
        }
        
        /// <summary>
        /// Carica la miniatura del video
        /// </summary>
        /// <param name="url">URL della miniatura</param>
        private async Task LoadThumbnail(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
                
            try
            {
                var bitmap = await ImageHelper.LoadImageFromUrlAsync(url);
                ThumbnailImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                // In caso di errore, usa un'immagine segnaposto
                Console.WriteLine($"Errore nel caricamento della miniatura: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Formatta il conteggio delle visualizzazioni
        /// </summary>
        /// <param name="viewCount">Conteggio delle visualizzazioni</param>
        /// <returns>Conteggio formattato</returns>
        private string FormatViewCount(int viewCount)
        {
            if (viewCount >= 1000000)
                return $"{viewCount / 1000000.0:0.#}M";
            else if (viewCount >= 1000)
                return $"{viewCount / 1000.0:0.#}K";
            else
                return viewCount.ToString();
        }
        
        /// <summary>
        /// Gestore dell'evento Click per il pulsante Seleziona
        /// </summary>
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            VideoSelected?.Invoke(this, _searchResult);
        }
        
        /// <summary>
        /// Gestore dell'evento Click per il pulsante Apri in Browser
        /// </summary>
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            // Apri il video nel browser predefinito
            Process.Start(new ProcessStartInfo
            {
                FileName = _searchResult.VideoUrl,
                UseShellExecute = true
            });
        }

        private void SetLocalizedStrings()
        {
            SelectButton.Content = LocalizationHelper.GetString("SelectButton");
            OpenButton.Content = LocalizationHelper.GetString("OpenBrowserButton");
        }
    }
}
