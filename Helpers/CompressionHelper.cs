using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Phonexis.Helpers
{
    /// <summary>
    /// Helper class for compression and decompression operations
    /// </summary>
    public static class CompressionHelper
    {
        /// <summary>
        /// Compresses a string using GZip compression
        /// </summary>
        /// <param name="text">The string to compress</param>
        /// <returns>The compressed data as a byte array</returns>
        public static byte[] CompressString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new byte[0];

            byte[] buffer = Encoding.UTF8.GetBytes(text);
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gzipStream.Write(buffer, 0, buffer.Length);
                }
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Decompresses a GZip compressed byte array to a string
        /// </summary>
        /// <param name="compressedData">The compressed data</param>
        /// <returns>The decompressed string</returns>
        public static string DecompressString(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return string.Empty;

            using (var memoryStream = new MemoryStream(compressedData))
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        gzipStream.CopyTo(resultStream);
                        return Encoding.UTF8.GetString(resultStream.ToArray());
                    }
                }
            }
        }

        /// <summary>
        /// Compresses a string and writes it to a file
        /// </summary>
        /// <param name="text">The string to compress</param>
        /// <param name="filePath">The path to the output file</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool CompressStringToFile(string text, string filePath)
        {
            try
            {
                byte[] compressedData = CompressString(text);
                File.WriteAllBytes(filePath, compressedData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads a compressed file and decompresses it to a string
        /// </summary>
        /// <param name="filePath">The path to the compressed file</param>
        /// <returns>The decompressed string</returns>
        public static string DecompressFileToString(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            byte[] compressedData = File.ReadAllBytes(filePath);
            return DecompressString(compressedData);
        }
    }
}
