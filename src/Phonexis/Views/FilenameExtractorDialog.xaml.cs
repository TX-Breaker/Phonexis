using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Phonexis.Services;

namespace Phonexis.Views;

public partial class FilenameExtractorDialog : Window
{
    private static readonly string[] AudioExtensions =
        { "mp3", "flac", "wav", "m4a", "aac", "ogg", "opus", "wma", "aiff", "alac", "m4r", "pcm" };

    private static readonly string[] DefaultChecked = { "mp3", "flac", "wav", "m4a", "aac", "ogg", "opus", "wma" };

    private readonly List<CheckBox> _checks = new();
    private string? _folder;

    public List<string> ResultTitles { get; } = new();

    /// <summary>Full audio file paths, parallel to <see cref="ResultTitles"/>.</summary>
    public List<string> ResultPaths { get; } = new();
    public string? SavedFilePath { get; private set; }
    public bool UseAsQueue { get; private set; }

    public FilenameExtractorDialog()
    {
        InitializeComponent();
        foreach (var ext in AudioExtensions)
        {
            var cb = new CheckBox
            {
                Content = ext.ToUpperInvariant(),
                IsChecked = DefaultChecked.Contains(ext),
                Tag = ext
            };
            cb.SetResourceReference(StyleProperty, "CheckBox.Chip");
            cb.Checked += Options_Changed;
            cb.Unchecked += Options_Changed;
            _checks.Add(cb);
            ExtensionsPanel.Children.Add(cb);
        }
        FolderText.Text = I18n.Instance["NoFolderSelected"];
        UpdateCount();
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = I18n.Instance["SelectFolder"] };
        if (dialog.ShowDialog() == true)
        {
            _folder = dialog.FolderName;
            FolderText.Text = _folder;
            UpdateCount();
        }
    }

    private void Options_Changed(object sender, RoutedEventArgs e) => UpdateCount();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _checks) cb.IsChecked = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _checks) cb.IsChecked = false;
    }

    private List<string> Scan()
    {
        if (_folder == null || !Directory.Exists(_folder)) return new List<string>();
        // Returns full paths; titles are derived from them in UpdateCount.

        var wanted = _checks.Where(c => c.IsChecked == true)
                            .Select(c => "." + (string)c.Tag)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0) return new List<string>();

        var option = RecursiveCheck.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        try
        {
            return Directory.EnumerateFiles(_folder, "*.*", option)
                .Where(f => wanted.Contains(Path.GetExtension(f)))
                .Where(f => !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(f)))
                .ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return new List<string>();
        }
    }

    private void UpdateCount()
    {
        var found = Scan();
        ResultPaths.Clear();
        ResultPaths.AddRange(found);
        ResultTitles.Clear();
        ResultTitles.AddRange(found.Select(f => Path.GetFileNameWithoutExtension(f)));
        CountText.Text = found.Count == 0 && _folder != null
            ? I18n.Instance["ExtractorEmpty"]
            : I18n.Instance.Format("ExtractorFoundFormat", found.Count);
        SaveTxtButton.IsEnabled = found.Count > 0;
        UseButton.IsEnabled = found.Count > 0;
    }

    private void SaveTxt_Click(object sender, RoutedEventArgs e)
    {
        if (ResultTitles.Count == 0) return;
        var dialog = new SaveFileDialog
        {
            Filter = $"{I18n.Instance["ListFilterPhonexis"]}|*.phonexis|{I18n.Instance["ListFilterTxt"]}|*.txt",
            FileName = (Path.GetFileName(_folder) is { Length: > 0 } name ? name : "songs") + ListFileService.Extension,
            Title = I18n.Instance["SaveAsTxt"]
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            // A .phonexis list keeps each line's audio file, so videos can later be named after it.
            if (ListFileService.IsListFile(dialog.FileName))
                ListFileService.Save(dialog.FileName, ListFileService.FromTitles(ResultTitles, ResultPaths));
            else
                File.WriteAllLines(dialog.FileName, ResultTitles);
            SavedFilePath = dialog.FileName;
            UseAsQueue = true; // saving implies using the result
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, I18n.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        if (ResultTitles.Count == 0) return;
        UseAsQueue = true;
        DialogResult = true;
    }
}
