using System.IO;

namespace Phonexis.Services.Downloads;

/// <summary>Gives a video the same creation and modification dates as its audio file (optional).</summary>
public static class FileDates
{
    /// <summary>Copies creation and last-write time from <paramref name="audioPath"/>; false when it cannot.</summary>
    public static bool CopyFromAudio(string? audioPath, string videoPath)
    {
        if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath) || !File.Exists(videoPath)) return false;
        try
        {
            File.SetCreationTimeUtc(videoPath, File.GetCreationTimeUtc(audioPath));
            File.SetLastWriteTimeUtc(videoPath, File.GetLastWriteTimeUtc(audioPath));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
