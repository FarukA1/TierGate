using TierGate.Core.RateLimiting;

namespace TierGate.Core.Tests;

public class InMemoryRateLimitStoreTests
{
    [Fact]
    public async Task TryConsumeAsync_AllowsUpToLimit_ThenDenies()
    {
        var store = new InMemoryRateLimitStore();

        var first = await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 2);
        var second = await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 2);
        var third = await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 2);

        Assert.True(first.Allowed);
        Assert.Equal(1, first.Remaining);
        Assert.True(second.Allowed);
        Assert.Equal(0, second.Remaining);
        Assert.False(third.Allowed);
        Assert.Equal(RateLimitOutcome.Denied, third.Outcome);
    }

    [Fact]
    public async Task TryConsumeAsync_DeniedRequests_DoNotOvershootTheCounter()
    {
        var store = new InMemoryRateLimitStore();

        for (var i = 0; i < 10; i++)
            await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 3);

        var usage = await store.GetCurrentUsageAsync("subject-1", RateLimitWindow.PerMinute);
        Assert.Equal(3, usage);
    }

    [Fact]
    public async Task TryConsumeAsync_DifferentSubjects_AreIndependent()
    {
        var store = new InMemoryRateLimitStore();

        await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 1);
        var result = await store.TryConsumeAsync("subject-2", RateLimitWindow.PerMinute, limit: 1);

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task SeedUsageAsync_SetsInitialCount()
    {
        var store = new InMemoryRateLimitStore();

        await store.SeedUsageAsync("subject-1", RateLimitWindow.CalendarMonth, count: 40);
        var usage = await store.GetCurrentUsageAsync("subject-1", RateLimitWindow.CalendarMonth);

        Assert.Equal(40, usage);
    }

    [Fact]
    public async Task SeedUsageAsync_NonPositiveCount_IsNoOp()
    {
        var store = new InMemoryRateLimitStore();

        await store.SeedUsageAsync("subject-1", RateLimitWindow.CalendarMonth, count: 0);
        var usage = await store.GetCurrentUsageAsync("subject-1", RateLimitWindow.CalendarMonth);

        Assert.Equal(0, usage);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_UnknownSubject_ReturnsZero()
    {
        var store = new InMemoryRateLimitStore();

        var usage = await store.GetCurrentUsageAsync("never-seen", RateLimitWindow.PerMinute);

        Assert.Equal(0, usage);
    }

    [Fact]
    public async Task ReconcileUsageAsync_OverwritesAnExistingBucket()
    {
        var store = new InMemoryRateLimitStore();
        await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 100);
        await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 100);

        await store.ReconcileUsageAsync("subject-1", RateLimitWindow.PerMinute, authoritativeCount: 50);
        var usage = await store.GetCurrentUsageAsync("subject-1", RateLimitWindow.PerMinute);

        Assert.Equal(50, usage);
    }

    [Fact]
    public async Task ReconcileUsageAsync_CreatesAMissingBucket()
    {
        var store = new InMemoryRateLimitStore();

        await store.ReconcileUsageAsync("subject-1", RateLimitWindow.PerMinute, authoritativeCount: 30);
        var usage = await store.GetCurrentUsageAsync("subject-1", RateLimitWindow.PerMinute);

        Assert.Equal(30, usage);
    }

    [Fact]
    public async Task ReconcileUsageAsync_AcceptsZeroUnlikeSeedUsageAsync()
    {
        var store = new InMemoryRateLimitStore();
        await store.TryConsumeAsync("subject-1", RateLimitWindow.PerMinute, limit: 100);

        await store.ReconcileUsageAsync("subject-1", RateLimitWindow.PerMinute, authoritativeCount: 0);
        var usage = await store.GetCurrentUsageAsync("subject-1", RateLimitWindow.PerMinute);

        Assert.Equal(0, usage);
    }
}
