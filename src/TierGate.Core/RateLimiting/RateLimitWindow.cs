namespace TierGate.Core.RateLimiting;

/// <summary>
/// Describes how a rate limit's counting window is bucketed and when a bucket resets.
/// Stores call <see cref="GetBucketKey"/> to partition counters and never need to know
/// whether the window is a fixed interval, a calendar month, or a custom rule.
/// </summary>
public readonly struct RateLimitWindow
{
    private enum Kind { FixedInterval, CalendarMonth, Custom }

    private readonly Kind _kind;
    private readonly TimeSpan _interval;
    private readonly Func<DateTimeOffset, string>? _customBucketKeySelector;
    private readonly Func<DateTimeOffset, DateTimeOffset>? _customResetsAtSelector;

    private RateLimitWindow(
        Kind kind,
        TimeSpan interval,
        Func<DateTimeOffset, string>? customBucketKeySelector = null,
        Func<DateTimeOffset, DateTimeOffset>? customResetsAtSelector = null)
    {
        _kind = kind;
        _interval = interval;
        _customBucketKeySelector = customBucketKeySelector;
        _customResetsAtSelector = customResetsAtSelector;
    }

    public static readonly RateLimitWindow PerSecond = FixedInterval(TimeSpan.FromSeconds(1));
    public static readonly RateLimitWindow PerMinute = FixedInterval(TimeSpan.FromMinutes(1));
    public static readonly RateLimitWindow PerHour = FixedInterval(TimeSpan.FromHours(1));
    public static readonly RateLimitWindow PerDay = FixedInterval(TimeSpan.FromDays(1));

    // Not a fixed TimeSpan: calendar months vary in length, and subscription quotas
    // reset on the calendar month boundary to match billing cycles, not a rolling 30 days.
    public static readonly RateLimitWindow CalendarMonth = new(Kind.CalendarMonth, TimeSpan.Zero);

    public static RateLimitWindow FixedInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        return new RateLimitWindow(Kind.FixedInterval, interval);
    }

    /// <summary>
    /// Escape hatch for windows a fixed interval or calendar month can't express,
    /// e.g. a billing-anniversary-aligned window.
    /// </summary>
    public static RateLimitWindow Custom(
        Func<DateTimeOffset, string> bucketKeySelector,
        Func<DateTimeOffset, DateTimeOffset> resetsAtSelector)
    {
        ArgumentNullException.ThrowIfNull(bucketKeySelector);
        ArgumentNullException.ThrowIfNull(resetsAtSelector);
        return new RateLimitWindow(Kind.Custom, TimeSpan.Zero, bucketKeySelector, resetsAtSelector);
    }

    public string GetBucketKey(DateTimeOffset now) => _kind switch
    {
        Kind.FixedInterval => FloorToInterval(now, _interval).UtcTicks.ToString(),
        Kind.CalendarMonth => now.ToString("yyyyMM"),
        Kind.Custom => _customBucketKeySelector!(now),
        _ => throw new InvalidOperationException("Unrecognized window kind.")
    };

    public DateTimeOffset GetResetsAt(DateTimeOffset now) => _kind switch
    {
        Kind.FixedInterval => FloorToInterval(now, _interval) + _interval,
        Kind.CalendarMonth => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1),
        Kind.Custom => _customResetsAtSelector!(now),
        _ => throw new InvalidOperationException("Unrecognized window kind.")
    };

    private static DateTimeOffset FloorToInterval(DateTimeOffset now, TimeSpan interval)
    {
        var utc = now.ToUniversalTime();
        var flooredTicks = utc.UtcTicks - (utc.UtcTicks % interval.Ticks);
        return new DateTimeOffset(flooredTicks, TimeSpan.Zero);
    }
}
