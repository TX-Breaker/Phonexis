using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Phonexis.Models;
using Phonexis.Services;

namespace Phonexis.ViewModels;

/// <summary>.phonexis list files: open, save as, and the state snapshot shared with the autosave.</summary>
public partial class MainViewModel
{
    /// <summary>The queue as it stands; used by the autosave and by "Save list".</summary>
    private SessionState BuildState() => new()
    {
        SourceFilePath = _sourceFilePath,
        CurrentIndex = Math.Max(0, CurrentIndex),
        Songs = Songs.Select(s => new SessionSong
        {
            OriginalTitle = s.OriginalTitle,
            Query = s.Query,
            Status = s.Status,
            SelectedUrl = s.SelectedUrl,
            SelectedTitle = s.SelectedTitle,
            SourceAudioPath = s.SourceAudioPath,
            SelectedVideoId = s.SelectedVideoId,
            SelectedDurationSeconds = s.SelectedDurationSeconds,
            DownloadedPath = s.DownloadedPath
        }).ToList()
    };

    private void LoadListFile(string path)
    {
        try
        {
            var state = ListFileService.Load(path);
            ApplyState(state, path);
            MarkDirty();
            SaveSessionInternal(path);
            StatusMessage = L.Format("StatusFileLoadedFormat", SourceFileName);
            _log.Info($"Loaded list {path}: {Songs.Count} songs");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _log.Error($"Failed to load list '{path}'", ex);
            MessageBox.Show(L.Format("FileLoadErrorFormat", ex.Message), L["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanSaveList() => HasSongs;

    [RelayCommand(CanExecute = nameof(CanSaveList))]
    private void SaveListAs()
    {
        var suggested = string.IsNullOrEmpty(SourceFileName) ? "phonexis" : Path.GetFileNameWithoutExtension(SourceFileName);
        var dialog = new SaveFileDialog
        {
            Title = L["SaveList"],
            FileName = suggested + ListFileService.Extension,
            Filter = $"{L["ListFilterPhonexis"]}|*.phonexis",
            InitialDirectory = _sourceFilePath != null && Path.GetDirectoryName(_sourceFilePath) is { } dir && Directory.Exists(dir) ? dir : null
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            ListFileService.Save(dialog.FileName, BuildState());
            _sourceFilePath = dialog.FileName;
            SourceFileName = Path.GetFileName(dialog.FileName);
            SaveSessionInternal(dialog.FileName);
            StatusMessage = L.Format("ListSavedFormat", SourceFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Saving list failed", ex);
            MessageBox.Show(ex.Message, L["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
