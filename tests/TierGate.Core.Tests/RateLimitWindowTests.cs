using TierGate.Core.RateLimiting;

namespace TierGate.Core.Tests;

public class RateLimitWindowTests
{
    [Fact]
    public void PerMinute_SameMinute_ProducesSameBucketKey()
    {
        var window = RateLimitWindow.PerMinute;
        var t1 = new DateTimeOffset(2026, 1, 1, 10, 30, 5, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 1, 1, 10, 30, 55, TimeSpan.Zero);

        Assert.Equal(window.GetBucketKey(t1), window.GetBucketKey(t2));
    }

    [Fact]
    public void PerMinute_DifferentMinute_ProducesDifferentBucketKey()
    {
        var window = RateLimitWindow.PerMinute;
        var t1 = new DateTimeOffset(2026, 1, 1, 10, 30, 59, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 1, 1, 10, 31, 0, TimeSpan.Zero);

        Assert.NotEqual(window.GetBucketKey(t1), window.GetBucketKey(t2));
    }

    [Fact]
    public void PerMinute_ResetsAt_IsStartOfNextMinute()
    {
        var window = RateLimitWindow.PerMinute;
        var now = new DateTimeOffset(2026, 1, 1, 10, 30, 15, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 31, 0, TimeSpan.Zero), window.GetResetsAt(now));
    }

    [Fact]
    public void CalendarMonth_SameMonth_ProducesSameBucketKey()
    {
        var window = RateLimitWindow.CalendarMonth;
        var t1 = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

        Assert.Equal(window.GetBucketKey(t1), window.GetBucketKey(t2));
    }

    [Fact]
    public void CalendarMonth_ResetsAt_IsFirstOfNextMonth()
    {
        var window = RateLimitWindow.CalendarMonth;
        var now = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), window.GetResetsAt(now));
    }

    [Fact]
    public void CalendarMonth_December_RollsOverToNextYear()
    {
        var window = RateLimitWindow.CalendarMonth;
        var now = new DateTimeOffset(2026, 12, 20, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), window.GetResetsAt(now));
    }

    [Fact]
    public void FixedInterval_RejectsNonPositiveInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RateLimitWindow.FixedInterval(TimeSpan.Zero));
    }

    [Fact]
    public void Custom_UsesProvidedSelectors()
    {
        var window = RateLimitWindow.Custom(
            bucketKeySelector: t => "fixed-bucket",
            resetsAtSelector: t => t.AddDays(1));
        var now = DateTimeOffset.UtcNow;

        Assert.Equal("fixed-bucket", window.GetBucketKey(now));
        Assert.Equal(now.AddDays(1), window.GetResetsAt(now));
    }
}
