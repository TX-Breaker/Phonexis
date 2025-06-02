# Phonexis - TXT to YT Multisearch

This is the Phonexis application, which allows you to search for YouTube videos based on song titles from a text file.

## Features

- **Youtube**: Search for videos on YouTube using the YouTube Data v3 API
- **Caching**: Caches search results and thumbnails to reduce API usage
- **State Management**: Saves and restores application state
- **Internationalization**: Support for multiple languages
- **Test Mode**: Test the application without making real API calls
- **Cache Only Mode**: Uses only results already present in the local cache

## Requirements

- Windows 10/11
- .NET 6.0 Runtime or higher
- Internet connection (for Youtubees)
- YouTube Data v3 API Key

## Installation

1. Download the latest version from the repository
2. Extract the ZIP file to a directory of your choice
3. Run `Phonexis - TXT to YT Multisearch.exe`

## Usage

1. Start the application
2. Click "Browse .txt File" to select a text file containing song titles (one per line)
3. Set your YouTube API key using the "Set API Key" button
4. Click "Start Youtube" to begin the search
5. For each song, select a video from the results or click "Skip" to skip it
6. Upon completion, the selected links will be saved to a file

## Development

### Prerequisites

- Visual Studio 2022 (Community Edition is sufficient)
- .NET 6.0 SDK
- Knowledge of C# and WPF

### Compiling

1. Open the solution in Visual Studio
2. Restore NuGet packages
3. Compile the solution

### Project Structure

- **Models**: Contains data models
- **ViewModels**: Contains ViewModels for the MVVM pattern
- **Views**: Contains WPF views
- **Services**: Contains services for data access and APIs
- **Helpers**: Contains utility classes
- **Resources**: Contains resources such as images and localization files

## License

This project is released under the MIT License.
