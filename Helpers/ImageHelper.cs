using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Phonexis.Helpers
{
    /// <summary>
    /// Helper per la gestione delle immagini
    /// </summary>
    public static class ImageHelper
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        
        /// <summary>
        /// Carica un'immagine da un URL
        /// </summary>
        /// <param name="url">URL dell'immagine</param>
        /// <returns>BitmapImage dell'immagine</returns>
        public static async Task<BitmapImage> LoadImageFromUrlAsync(string url)
        {
            try
            {
                byte[] imageData = await _httpClient.GetByteArrayAsync(url);
                
                using (var stream = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Importante per l'uso cross-thread
                    
                    return bitmap;
                }
            }
            catch (Exception)
            {
                // In caso di errore, restituisci un'immagine di fallback
                return CreatePlaceholderImage();
            }
        }
        
        /// <summary>
        /// Crea un'immagine segnaposto
        /// </summary>
        /// <returns>BitmapImage dell'immagine segnaposto</returns>
        private static BitmapImage CreatePlaceholderImage()
        {
            // Usa un'immagine segnaposto da una URL pubblica
            var bitmap = new BitmapImage(new Uri("https://via.placeholder.com/120x90?text=No+Image", UriKind.Absolute));
            return bitmap;
        }
        
        /// <summary>
        /// Carica un'immagine da un array di byte
        /// </summary>
        /// <param name="imageData">Dati dell'immagine</param>
        /// <returns>BitmapImage dell'immagine</returns>
        public static BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return CreatePlaceholderImage();
                
            try
            {
                using (var stream = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Importante per l'uso cross-thread
                    
                    return bitmap;
                }
            }
            catch (Exception)
            {
                return CreatePlaceholderImage();
            }
        }
    }
}
