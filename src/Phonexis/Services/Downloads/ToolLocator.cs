using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Microsoft.Win32;

namespace Phonexis.Services.Downloads;

/// <summary>Finds external tools: explicit path, Phonexis tools folder, PATH, then installed programs.</summary>
public static class ToolLocator
{
    public static string ToolsFolder => Path.Combine(AppPaths.Root, "tools");

    private const string FfmpegZipUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    public static string? FindFfmpeg(string? explicitPath) => FindExecutable("ffmpeg.exe", explicitPath);

    public static string? FindFfprobe(string? ffmpegPath)
    {
        if (ffmpegPath != null)
        {
            var sibling = Path.Combine(Path.GetDirectoryName(ffmpegPath) ?? "", "ffprobe.exe");
            if (File.Exists(sibling)) return sibling;
        }
        return FindExecutable("ffprobe.exe", null);
    }

    public static string? FindExecutable(string exeName, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;

        var local = Path.Combine(ToolsFolder, exeName);
        if (File.Exists(local)) return local;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), exeName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry.
            }
        }

        // WinGet keeps portable packages here and only links them on PATH for new shells.
        var winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(winget))
        {
            try
            {
                var hit = Directory.EnumerateFiles(winget, exeName, SearchOption.AllDirectories).FirstOrDefault();
                if (hit != null) return hit;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Unreadable package folder.
            }
        }
        return null;
    }

    /// <summary>
    /// Looks up an installed program in the Windows uninstall registry by display-name fragment
    /// and returns its main executable when one can be derived.
    /// </summary>
    public static string? FindInstalledProgram(string displayNameFragment, string? exeName = null)
    {
        var roots = new[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (hive, path) in roots)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var app = key.OpenSubKey(name);
                    var display = app?.GetValue("DisplayName") as string;
                    if (display == null || !display.Contains(displayNameFragment, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var location = app!.GetValue("InstallLocation") as string;
                    if (exeName != null && !string.IsNullOrWhiteSpace(location))
                    {
                        var exe = Path.Combine(location.Trim('"'), exeName);
                        if (File.Exists(exe)) return exe;
                    }

                    var icon = (app.GetValue("DisplayIcon") as string)?.Split(',')[0].Trim('"');
                    if (!string.IsNullOrWhiteSpace(icon) && icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(icon))
                        return icon;

                    if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location.Trim('"')))
                        return location.Trim('"');
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Registry branch not readable.
            }
        }
        return null;
    }

    /// <summary>Downloads the FFmpeg "essentials" build into the Phonexis tools folder. Asks nothing: callers confirm first.</summary>
    public static async Task<string> DownloadFfmpegAsync(IProgress<double>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(ToolsFolder);
        var zipPath = Path.Combine(ToolsFolder, "ffmpeg.zip");

        using (var http = new HttpClient())
        using (var response = await http.GetAsync(FfmpegZipUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(zipPath);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0) progress?.Report((double)read / total);
            }
        }

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries.Where(e =>
                         e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
                         e.Name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)))
            {
                entry.ExtractToFile(Path.Combine(ToolsFolder, entry.Name), overwrite: true);
            }
        }
        File.Delete(zipPath);

        var ffmpeg = Path.Combine(ToolsFolder, "ffmpeg.exe");
        if (!File.Exists(ffmpeg)) throw new FileNotFoundException("ffmpeg.exe not found in the downloaded archive.");
        return ffmpeg;
    }

    /// <summary>Media duration in seconds via ffprobe, or null.</summary>
    public static double? ProbeDurationSeconds(string ffprobe, string file)
    {
        try
        {
            var psi = new ProcessStartInfo(ffprobe)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            foreach (var a in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", file })
                psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
        catch
        {
            return null;
        }
    }
}
