using System.Windows;

namespace Phonexis.Views;

public partial class EditQueryDialog : Window
{
    public string EditedQuery => QueryBox.Text;

    public EditQueryDialog(string currentQuery)
    {
        InitializeComponent();
        QueryBox.Text = currentQuery;
        QueryBox.SelectAll();
        QueryBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
