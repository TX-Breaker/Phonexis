using System.Windows.Input;

namespace Phonexis.ViewModels;

/// <summary>Keyboard shortcuts: the window maps keys to these actions, the view model decides what is allowed.</summary>
public partial class MainViewModel
{
    public enum ShortcutAction
    {
        Pick,
        Skip,
        Back,
        EditQuery,
        SearchAgain,
        OpenFirst,
        Preview,
        LoadFile,
        SaveSession,
        SaveList,
        Export,
        DownloadVideos,
        Guide
    }

    /// <summary>Runs a shortcut; returns false when it does not apply right now (the key then keeps its default behaviour).</summary>
    public bool RunShortcut(ShortcutAction action, int number = 0)
    {
        return action switch
        {
            ShortcutAction.Pick => IsResultsVisible && !IsSearching && number >= 1 && number <= Results.Count
                                   && Execute(SelectVideoCommand, Results[number - 1]),
            ShortcutAction.Skip => IsResultsVisible && Execute(SkipCommand),
            ShortcutAction.Back => IsResultsVisible && Execute(GoBackCommand),
            ShortcutAction.EditQuery => IsResultsVisible && Execute(EditQueryCommand),
            ShortcutAction.SearchAgain => IsResultsVisible && Execute(RefreshSearchCommand),
            ShortcutAction.OpenFirst => IsResultsVisible && Results.Count > 0 && Execute(OpenVideoCommand, Results[0]),
            ShortcutAction.Preview => IsResultsVisible && !IsSearching && number >= 1 && number <= Results.Count
                                      && Execute(PreviewVideoCommand, Results[number - 1]),
            ShortcutAction.LoadFile => Execute(LoadFileCommand),
            ShortcutAction.SaveSession => HasSongs && Execute(SaveSessionNowCommand),
            ShortcutAction.SaveList => Execute(SaveListAsCommand),
            ShortcutAction.Export => HasSongs && Execute(ExportCommand),
            ShortcutAction.DownloadVideos => HasSongs && Execute(DownloadVideosCommand),
            ShortcutAction.Guide => Execute(OpenOnboardingCommand),
            _ => false
        };
    }

    /// <summary>Plays a result inside Phonexis; "Select this video" in the preview picks it.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void PreviewVideo(Models.SearchResult? result)
    {
        if (result == null) return;
        var dialog = new Views.PreviewDialog(result) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() == true) SelectVideoCommand.Execute(result);
    }

    private static bool Execute(ICommand command, object? parameter = null)
    {
        if (!command.CanExecute(parameter)) return false;
        command.Execute(parameter);
        return true;
    }
}
