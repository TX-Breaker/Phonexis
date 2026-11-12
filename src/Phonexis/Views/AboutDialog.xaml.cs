using System.Diagnostics;
using System.Windows;
using Phonexis.Services;

namespace Phonexis.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        VersionText.Text = I18n.Instance.Format("AboutVersionFormat", AppInfo.Version);
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = AppInfo.GitHubUrl, UseShellExecute = true });
        }
        catch
        {
            // Browser launch failure is non-fatal.
        }
    }
}
