using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Phonexis.Models;

namespace Phonexis.Services;

/// <summary>
/// ".phonexis" list files: the session snapshot as readable JSON, so a list keeps what a TXT loses —
/// audio file paths, edited queries, picks and download state.
/// </summary>
public static class ListFileService
{
    public const string Extension = ".phonexis";
    public const string FormatName = "phonexis-list";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static bool IsListFile(string path) =>
        Path.GetExtension(path).Equals(Extension, StringComparison.OrdinalIgnoreCase);

    public static void Save(string path, SessionState state)
    {
        state.Format = FormatName;
        state.SavedAt = DateTime.Now;
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Reads a list; throws <see cref="InvalidDataException"/> when the file is not a Phonexis list.</summary>
    public static SessionState Load(string path)
    {
        SessionState? state;
        try
        {
            state = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Not a Phonexis list.", ex);
        }
        if (state?.Songs == null || state.Songs.Count == 0 || state.Songs.Any(s => string.IsNullOrWhiteSpace(s.OriginalTitle)))
            throw new InvalidDataException("Not a Phonexis list.");

        foreach (var s in state.Songs.Where(s => string.IsNullOrWhiteSpace(s.Query)))
            s.Query = s.OriginalTitle;
        return state;
    }

    /// <summary>A fresh list from a folder scan: titles with the audio files they came from.</summary>
    public static SessionState FromTitles(IReadOnlyList<string> titles, IReadOnlyList<string>? audioPaths) => new()
    {
        Songs = titles.Select((t, i) => new SessionSong
        {
            OriginalTitle = t,
            Query = t,
            Status = SongStatus.Pending,
            SourceAudioPath = audioPaths != null && i < audioPaths.Count ? audioPaths[i] : null
        }).ToList()
    };
}
