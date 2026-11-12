namespace Phonexis.Models;

public enum DurationFilter { Any, Short, Medium, Long }

public enum DateFilter { Any, Today, ThisWeek, ThisMonth, ThisYear, Older }

public enum ViewsFilter { Any, High, Medium, Low }

/// <summary>Client-side filters applied to loaded results.</summary>
public class SearchFilters
{
    public DurationFilter Duration { get; set; } = DurationFilter.Any;
    public DateFilter Date { get; set; } = DateFilter.Any;
    public ViewsFilter Views { get; set; } = ViewsFilter.Any;

    public bool IsActive => Duration != DurationFilter.Any || Date != DateFilter.Any || Views != ViewsFilter.Any;

    public bool Passes(SearchResult r) => PassesDuration(r) && PassesDate(r) && PassesViews(r);

    private bool PassesDuration(SearchResult r)
    {
        if (Duration == DurationFilter.Any) return true;
        if (r.DurationSeconds is not > 0) return true; // unknown duration: never hide
        var minutes = r.DurationSeconds.Value / 60.0;
        return Duration switch
        {
            DurationFilter.Short => minutes < 4,
            DurationFilter.Medium => minutes is >= 4 and <= 20,
            DurationFilter.Long => minutes > 20,
            _ => true
        };
    }

    private bool PassesDate(SearchResult r)
    {
        if (Date == DateFilter.Any || r.PublishedAt == default) return true;
        var now = DateTimeOffset.Now;
        var age = now - r.PublishedAt;
        return Date switch
        {
            DateFilter.Today => age.TotalDays <= 1,
            DateFilter.ThisWeek => age.TotalDays <= 7,
            DateFilter.ThisMonth => age.TotalDays <= 31,
            DateFilter.ThisYear => age.TotalDays <= 366,
            DateFilter.Older => age.TotalDays > 366,
            _ => true
        };
    }

    private bool PassesViews(SearchResult r)
    {
        if (Views == ViewsFilter.Any) return true;
        if (r.ViewCount <= 0) return true; // unknown views: never hide
        return Views switch
        {
            ViewsFilter.High => r.ViewCount > 100_000,
            ViewsFilter.Medium => r.ViewCount is >= 10_000 and <= 100_000,
            ViewsFilter.Low => r.ViewCount < 10_000,
            _ => true
        };
    }
}
