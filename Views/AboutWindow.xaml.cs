using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using Phonexis.Helpers;

namespace Phonexis.Views
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            // Create a programmatic About window since XAML code-behind generation is having issues
            this.Title = "About";
            this.Width = 500;
            this.Height = 400;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ResizeMode = ResizeMode.NoResize;
            this.ShowInTaskbar = false;
            
            // Create main container
            Grid mainGrid = new Grid();
            mainGrid.Margin = new Thickness(25);
            
            // Define rows
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Try to load logo image
            try
            {
                Image logoImage = new Image();
                logoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Images/Logo_YT_Searcher.png"));
                logoImage.Height = 60;
                logoImage.Margin = new Thickness(0, 0, 0, 20);
                Grid.SetRow(logoImage, 0);
                mainGrid.Children.Add(logoImage);
            }
            catch (Exception)
            {
                // If image loading fails, add a placeholder text
                TextBlock logoPlaceholder = new TextBlock();
                logoPlaceholder.Text = "LOGO";
                logoPlaceholder.FontSize = 24;
                logoPlaceholder.HorizontalAlignment = HorizontalAlignment.Center;
                logoPlaceholder.Margin = new Thickness(0, 0, 0, 20);
                Grid.SetRow(logoPlaceholder, 0);
                mainGrid.Children.Add(logoPlaceholder);
            }
            
            // Add title
            TextBlock titleBlock = new TextBlock();
            titleBlock.Text = "Phonexis - TXT a YT Multisearch";
            titleBlock.FontSize = 20;
            titleBlock.FontWeight = FontWeights.Bold;
            titleBlock.HorizontalAlignment = HorizontalAlignment.Center;
            titleBlock.Margin = new Thickness(0, 0, 0, 10);
            Grid.SetRow(titleBlock, 1);
            mainGrid.Children.Add(titleBlock);
            
            // Add content stack panel
            StackPanel contentPanel = new StackPanel();
            contentPanel.HorizontalAlignment = HorizontalAlignment.Center;
            contentPanel.Margin = new Thickness(0, 10, 0, 20);
            Grid.SetRow(contentPanel, 2);
            
            // Version info
            TextBlock versionBlock = new TextBlock();
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                versionBlock.Text = $"Versione: {version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
            }
            catch (Exception)
            {
                versionBlock.Text = "Versione: 1.0.0";
            }
            versionBlock.HorizontalAlignment = HorizontalAlignment.Center;
            versionBlock.Margin = new Thickness(0, 0, 0, 5);
            contentPanel.Children.Add(versionBlock);
            
            // Copyright
            TextBlock copyrightBlock = new TextBlock();
            copyrightBlock.Text = "© 2025 TX-Breaker";
            copyrightBlock.HorizontalAlignment = HorizontalAlignment.Center;
            copyrightBlock.Margin = new Thickness(0, 0, 0, 15);
            contentPanel.Children.Add(copyrightBlock);
            
            // Description
            TextBlock descriptionBlock = new TextBlock();
            descriptionBlock.Text = "Application for searching YouTube videos from a text file containing song titles.";
            descriptionBlock.TextWrapping = TextWrapping.Wrap;
            descriptionBlock.TextAlignment = TextAlignment.Center;
            descriptionBlock.MaxWidth = 400;
            descriptionBlock.Margin = new Thickness(0, 0, 0, 10);
            contentPanel.Children.Add(descriptionBlock);
            
            // Technologies
            TextBlock techBlock = new TextBlock();
            techBlock.Text = "Built with C#, WPF, and YouTube Data API v3.";
            techBlock.TextWrapping = TextWrapping.Wrap;
            techBlock.TextAlignment = TextAlignment.Center;
            techBlock.MaxWidth = 400;
            contentPanel.Children.Add(techBlock);
            
            mainGrid.Children.Add(contentPanel);
            
            // Add OK button
            Button okButton = new Button();
            okButton.Content = "OK";
            okButton.Width = 100;
            okButton.HorizontalAlignment = HorizontalAlignment.Center;
            okButton.Padding = new Thickness(0, 8, 0, 8);
            okButton.Click += OkButton_Click;
            okButton.IsDefault = true;
            Grid.SetRow(okButton, 3);
            mainGrid.Children.Add(okButton);
            
            this.Content = mainGrid;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
