using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Phonexis.Models;

namespace Phonexis.Services;

/// <summary>
/// Preselects the most likely result for a song. Signals, strongest first:
/// 1. the audio file name (or the list line) equals the video title;
/// 2. the video's duration matches the audio file's duration;
/// 3. artist and title words appear in the video title / channel;
/// 4. the channel is the artist's own ("Artist - Topic", VEVO, the artist name);
/// 5. penalties for versions the list did not ask for (live, cover, karaoke, slowed, 1 hour…);
/// 6. popularity as a light tie-breaker.
/// </summary>
public static partial class ResultRanker
{
    public const int Threshold = 45;

    // Versions that are rarely what a tracklist means unless the line says so.
    private static readonly (string Word, int Penalty)[] Variants =
    {
        ("live", 15), ("cover", 22), ("karaoke", 30), ("instrumental", 18), ("remix", 12), ("acoustic", 12),
        ("slowed", 25), ("sped up", 25), ("reverb", 18), ("nightcore", 30), ("8d", 25), ("reaction", 30),
        ("tutorial", 30), ("lesson", 25), ("1 hour", 30), ("10 hours", 30), ("loop", 18), ("full album", 30),
        ("bass boosted", 25), ("piano", 10), ("mashup", 12), ("parody", 30), ("shorts", 20), ("teaser", 20), ("trailer", 25)
    };

    // Words that decorate a title without changing which recording it is.
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "official", "video", "audio", "music", "lyrics", "lyric", "visualizer", "visualiser", "hd", "hq", "4k", "mv",
        "clip", "videoclip", "ufficiale", "oficial", "officiel", "offizielles", "feat", "ft", "featuring", "with", "the",
        "remastered", "remaster", "version", "original", "mix", "extended", "radio", "edit", "explicit", "clean", "new"
    };

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NonWord();

    /// <summary>Scores every result and marks the best one when it clears the threshold.</summary>
    public static void Rank(SongItem song, IReadOnlyList<SearchResult> results, double? audioSeconds)
    {
        foreach (var r in results)
        {
            r.IsRecommended = false;
            r.MatchScore = 0;
            r.MatchReasons = string.Empty;
        }
        if (results.Count == 0) return;

        var name = song.SourceAudioPath != null ? Path.GetFileNameWithoutExtension(song.SourceAudioPath) : song.OriginalTitle;
        var queryText = $"{song.Query} {name}";
        var artist = song.Query.Contains(" - ") ? song.Query[..song.Query.IndexOf(" - ", StringComparison.Ordinal)] : string.Empty;

        var scored = results.Select(r => (Result: r, Score: Score(r, name, queryText, artist, audioSeconds, out var reasons), Reasons: reasons))
                            .OrderByDescending(x => x.Score)
                            .ToList();

        foreach (var (result, score, reasons) in scored)
        {
            result.MatchScore = (int)Math.Round(Math.Clamp(score, 0, 100));
            result.MatchReasons = string.Join("\n", reasons);
        }

        var best = scored[0];
        var runnerUp = scored.Count > 1 ? scored[1].Score : double.MinValue;
        // Recommend only a winner: above the threshold and not an exact tie with the next one.
        if (best.Score >= Threshold && best.Score - runnerUp > 0.05) best.Result.IsRecommended = true;
    }

    private static double Score(SearchResult r, string name, string queryText, string artist, double? audioSeconds, out List<string> reasons)
    {
        var L = I18n.Instance;
        reasons = new List<string>();
        double score = 0;

        // 1. Exact name
        var strictName = Strict(name);
        var strictTitle = Strict(r.Title);
        var looseName = Loose(name);
        var looseTitle = Loose(r.Title);
        if (strictName.Length > 0 && (strictName == strictTitle || looseName == looseTitle))
        {
            score += 55;
            reasons.Add(L["PsExactName"]);
        }
        else if (looseName.Length > 3 && looseTitle.Contains(looseName, StringComparison.Ordinal))
        {
            score += 35;
            reasons.Add(L["PsNameInTitle"]);
        }

        // 2. Duration
        if (audioSeconds is double audio && r.DurationSeconds is int video && video > 0)
        {
            var diff = Math.Abs(audio - video);
            if (diff <= 2) { score += 28; reasons.Add(L.Format("PsDurationFormat", Math.Round(diff))); }
            else if (diff <= 5) { score += 20; reasons.Add(L.Format("PsDurationFormat", Math.Round(diff))); }
            else if (diff <= 15) { score += 8; reasons.Add(L.Format("PsDurationFormat", Math.Round(diff))); }
            else if (diff > 60) { score -= 25; reasons.Add(L.Format("PsDurationOffFormat", Math.Round(diff))); }
            else if (diff > 30) { score -= 12; reasons.Add(L.Format("PsDurationOffFormat", Math.Round(diff))); }
        }

        // 3. Words of artist and title
        var wanted = Tokens(queryText).Where(t => !Noise.Contains(t)).ToHashSet();
        var have = Tokens($"{r.Title} {r.ChannelTitle}");
        if (wanted.Count > 0)
        {
            var coverage = wanted.Count(have.Contains) / (double)wanted.Count;
            score += coverage * 25;
            if (coverage >= 0.99) reasons.Add(L["PsTokens"]);
        }

        // 4. Artist's own channel
        var channel = r.ChannelTitle.ToLowerInvariant();
        var artistLoose = Loose(artist);
        if (channel.EndsWith(" - topic", StringComparison.Ordinal) || channel.Contains("vevo", StringComparison.Ordinal) ||
            (artistLoose.Length > 2 && Loose(r.ChannelTitle).Contains(artistLoose, StringComparison.Ordinal)))
        {
            score += 10;
            reasons.Add(L["PsOfficial"]);
        }

        // 5. Versions the list did not ask for
        var titleLower = " " + Spaced(r.Title) + " ";
        var queryLower = " " + Spaced(queryText) + " ";
        foreach (var (word, penalty) in Variants)
        {
            var w = " " + word + " ";
            if (titleLower.Contains(w, StringComparison.Ordinal) && !queryLower.Contains(w, StringComparison.Ordinal))
            {
                score -= penalty;
                reasons.Add(L.Format("PsVariantFormat", word));
            }
        }

        // 6. Phonexis archives videos: a real video beats a static "official audio" upload.
        if (titleLower.Contains(" official video ", StringComparison.Ordinal) || titleLower.Contains(" music video ", StringComparison.Ordinal) ||
            titleLower.Contains(" videoclip ", StringComparison.Ordinal) || titleLower.Contains(" video ufficiale ", StringComparison.Ordinal))
        {
            score += 5;
            reasons.Add(L["PsMusicVideo"]);
        }
        else if (titleLower.Contains(" official audio ", StringComparison.Ordinal) || titleLower.Contains(" audio ", StringComparison.Ordinal))
        {
            score -= 3;
        }

        // 7. Popularity (light, continuous so it can break ties)
        if (r.ViewCount > 0)
        {
            score += Math.Min(12, Math.Log10(r.ViewCount) * 1.2);
            if (r.ViewCount >= 1_000_000) reasons.Add(L["PsPopular"]);
        }

        return score;
    }

    private static string Fold(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>Punctuation and spacing folded: "Bicep - Glue" == "bicep glue".</summary>
    private static string Strict(string text) => string.Join(' ', NonWord().Split(Fold(text)).Where(t => t.Length > 0));

    private static string Spaced(string text) => Strict(text);

    /// <summary>Strict, without decoration words like "official video" or "lyrics".</summary>
    private static string Loose(string text) =>
        string.Join(' ', NonWord().Split(Fold(text)).Where(t => t.Length > 0 && !Noise.Contains(t)));

    private static HashSet<string> Tokens(string text) =>
        NonWord().Split(Fold(text)).Where(t => t.Length > 1).ToHashSet();
}
