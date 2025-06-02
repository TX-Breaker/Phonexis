using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Phonexis.Helpers;
using Phonexis.ViewModels;

namespace Phonexis.Views
{
    /// <summary>
    /// Interaction logic for AudioFilenameRetrieverView.xaml
    /// </summary>
    public partial class AudioFilenameRetrieverView : UserControl
    {
        private string _folderPath = string.Empty;
        private string _outputFilePath = string.Empty;
        private readonly Dictionary<string, CheckBox> _fileTypeCheckboxes = new Dictionary<string, CheckBox>();
        private MainViewModel _mainViewModel;

        public AudioFilenameRetrieverView(MainViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            InitializeFileTypeCheckboxes();
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Return to the main view
            _mainViewModel.CurrentView = null;
        }

        private void InitializeFileTypeCheckboxes()
        {
            // Store references to all checkboxes for easier access
            _fileTypeCheckboxes.Add("mp3", MP3CheckBox);
            _fileTypeCheckboxes.Add("aac", AACCheckBox);
            _fileTypeCheckboxes.Add("pcm", PCMCheckBox);
            _fileTypeCheckboxes.Add("wma", WMACheckBox);
            _fileTypeCheckboxes.Add("flac", FLACCheckBox);
            _fileTypeCheckboxes.Add("wav", WAVCheckBox);
            _fileTypeCheckboxes.Add("m4a", M4ACheckBox);
            _fileTypeCheckboxes.Add("opus", OPUSCheckBox);
            _fileTypeCheckboxes.Add("aiff", AIFFCheckBox);
            _fileTypeCheckboxes.Add("m4r", M4RCheckBox);
            _fileTypeCheckboxes.Add("alac", ALACCheckBox);
            _fileTypeCheckboxes.Add("ogg", OGGCheckBox);
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Use OpenFileDialog as a workaround for folder selection
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Any File in the Audio Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select this folder"
            };

            if (dialog.ShowDialog() == true)
            {
                // Get the folder path from the selected file path
                _folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                FolderPathTextBlock.Text = $"Folder Selected: {_folderPath}";
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Select Output Text File",
                DefaultExt = ".txt",
                Filter = "Text Files (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                _outputFilePath = dialog.FileName;
                OutputFileTextBlock.Text = $"Output File: {_outputFilePath}";
            }
        }

        private void RetrieveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_folderPath))
            {
                MessageBox.Show("Please select a folder containing audio files.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_outputFilePath))
            {
                MessageBox.Show("Please select an output file location.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Get selected file extensions
                var selectedExtensions = _fileTypeCheckboxes
                    .Where(kvp => kvp.Value.IsChecked == true)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (selectedExtensions.Count == 0)
                {
                    MessageBox.Show("Please select at least one file type.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Determine scan mode
                bool scanSubfolders = ScanModeComboBox.SelectedIndex == 1; // Index 1 is "Master + Subfolders"

                // Retrieve audio filenames
                List<string> audioFilenames = new List<string>();
                
                if (scanSubfolders)
                {
                    // Scan recursively
                    foreach (var file in Directory.EnumerateFiles(_folderPath, "*.*", SearchOption.AllDirectories))
                    {
                        string extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                        if (selectedExtensions.Contains(extension))
                        {
                            audioFilenames.Add(Path.GetFileNameWithoutExtension(file));
                        }
                    }
                }
                else
                {
                    // Scan only main folder
                    foreach (var file in Directory.EnumerateFiles(_folderPath))
                    {
                        string extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                        if (selectedExtensions.Contains(extension))
                        {
                            audioFilenames.Add(Path.GetFileNameWithoutExtension(file));
                        }
                    }
                }

                // Write to output file
                File.WriteAllLines(_outputFilePath, audioFilenames);

                // Update status
                StatusTextBlock.Text = $"Success! {audioFilenames.Count} audio filenames saved to {_outputFilePath}";
                
                // Show success message
                MessageBox.Show($"Audio filenames saved to {_outputFilePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
