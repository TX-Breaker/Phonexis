using System.Text.RegularExpressions;

namespace Phonexis.Services;

/// <summary>Video IDs from the YouTube link shapes people paste or Phonexis stores.</summary>
public static partial class YouTubeUrl
{
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdShape();

    /// <summary>watch?v=…, youtu.be/…, /shorts/…, /embed/…, /live/… → the 11-character ID, or null.</summary>
    public static string? TryGetVideoId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        var host = uri.Host.ToLowerInvariant();
        string? id = null;

        if (host.EndsWith("youtu.be", StringComparison.Ordinal))
        {
            id = uri.AbsolutePath.Trim('/');
        }
        else if (host.EndsWith("youtube.com", StringComparison.Ordinal) || host.EndsWith("youtube-nocookie.com", StringComparison.Ordinal))
        {
            id = System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
            if (id == null)
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && segments[0] is "shorts" or "embed" or "live" or "v") id = segments[1];
            }
        }
        return id != null && IdShape().IsMatch(id) ? id : null;
    }
}
