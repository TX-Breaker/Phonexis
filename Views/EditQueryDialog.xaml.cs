using System.Windows;
using Phonexis.Helpers; // Added for LocalizationHelper

namespace Phonexis.Views
{
    public partial class EditQueryDialog : Window
    {
        public string EditedQuery { get; set; }

        public EditQueryDialog(string currentQuery)
        {
            InitializeComponent();
            EditedQuery = currentQuery;
            DataContext = this;

            // Set localized strings
            SetLocalizedStrings();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SetLocalizedStrings()
        {
            this.Title = LocalizationHelper.GetString("EditQueryDialogTitle");
            DialogLabel.Content = LocalizationHelper.GetString("EditQueryDialogLabel");
            OkButton.Content = LocalizationHelper.GetString("OkButton");
            CancelButton.Content = LocalizationHelper.GetString("CancelButton");
        }
    }
}
