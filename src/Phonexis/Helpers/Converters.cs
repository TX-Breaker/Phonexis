using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Phonexis.Models;

namespace Phonexis.Helpers;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is true;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value switch
        {
            null => false,
            string s => s.Length > 0,
            _ => true
        };
        if (Invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Loads thumbnails asynchronously without blocking the UI thread.</summary>
public class UrlToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string url || url.Length == 0) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(url, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class SongStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            SongStatus.Done => "Brush.Brand",
            SongStatus.Skipped => "Brush.Warning",
            SongStatus.NoResults => "Brush.Danger",
            _ => "Brush.BorderStrong"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class SourceToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ResultSource.Cache => "Brush.Success",
            ResultSource.Dummy => "Brush.Warning",
            ResultSource.Free => "Brush.TextSecondary", // neutral: purple belongs to the obi and its marks
            _ => "Brush.Info"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class SourceToSoftBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ResultSource.Cache => "Brush.SuccessSoft",
            ResultSource.Dummy => "Brush.WarningSoft",
            ResultSource.Free => "Brush.SurfaceAlt",
            _ => "Brush.InfoSoft"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible only when the two bound strings differ (e.g. an edited query vs. the original line).</summary>
public class DifferentToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var a = (values.Length > 0 ? values[0] as string : null)?.Trim() ?? string.Empty;
        var b = (values.Length > 1 ? values[1] as string : null)?.Trim() ?? string.Empty;
        return a.Length > 0 && !string.Equals(a, b, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Item index → "1".."9" for the results a number key can pick; empty beyond nine.</summary>
public class ShortcutNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int index && index is >= 0 and < 9 ? (index + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when an int count is above zero.</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>"Retry failed (3)" in the current language.</summary>
public class RetryLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Services.I18n.Instance.Format("DlRetryFailedFormat", value is int n ? n : 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
