using Microsoft.AspNetCore.Http;
using TierGate.AspNetCore.RateLimiting;
using TierGate.Core.RateLimiting;

namespace TierGate.AspNetCore.Tests;

public class TierGatePipelineTests
{
    private sealed record TestTier(int Limit, RateLimitKind Kind);

    private sealed class AlwaysUnavailableStore : IRateLimitStore
    {
        public Task<RateLimitResult> TryConsumeAsync(
            string subjectKey, RateLimitWindow window, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(RateLimitResult.Unavailable(limit));

        public Task<int> GetCurrentUsageAsync(
            string subjectKey, RateLimitWindow window, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task SeedUsageAsync(
            string subjectKey, RateLimitWindow window, int count, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static TierGateOptions<string, TestTier> BuildOptions(
        IRateLimitStore store,
        string? subject = "subject-1",
        bool failOpen = false,
        IReadOnlyList<PathString>? excludedPaths = null) => new()
    {
        Store = store,
        ExtractSubject = _ => subject,
        ResolveTierAsync = (_, _) => Task.FromResult<TestTier?>(new TestTier(2, RateLimitKind.Throttle)),
        GetLimits = tier => [new TierLimit(RateLimitWindow.PerMinute, tier.Limit, tier.Kind)],
        GetStoreKey = s => s,
        ExcludedPaths = excludedPaths ?? Array.Empty<PathString>(),
        FailOpenOnStoreUnavailable = failOpen,
    };

    private static DefaultHttpContext BuildContext(string path = "/api/resource")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task ExcludedPath_BypassesGatingEntirely()
    {
        var store = new InMemoryRateLimitStore();
        var options = BuildOptions(store, subject: null, excludedPaths: [new PathString("/health")]);
        var pipeline = new TierGatePipeline<string, TestTier>(options);
        var context = BuildContext("/health/live");

        var nextCalled = false;
        await pipeline.InvokeAsync(context, () => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task MissingSubject_Returns401()
    {
        var store = new InMemoryRateLimitStore();
        var options = BuildOptions(store, subject: null);
        var pipeline = new TierGatePipeline<string, TestTier>(options);
        var context = BuildContext();

        await pipeline.InvokeAsync(context, () => Task.CompletedTask);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task WithinLimit_CallsNextAndSetsHeaders()
    {
        var store = new InMemoryRateLimitStore();
        var options = BuildOptions(store);
        var pipeline = new TierGatePipeline<string, TestTier>(options);
        var context = BuildContext();

        var nextCalled = false;
        await pipeline.InvokeAsync(context, () => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.Equal("2", context.Response.Headers["X-RateLimit-Limit"]);
        Assert.Equal("1", context.Response.Headers["X-RateLimit-Remaining"]);
        Assert.Equal(new TestTier(2, RateLimitKind.Throttle), context.GetTierGateTier<TestTier>());
    }

    [Fact]
    public async Task ExceedsThrottleLimit_Returns429WithRetryAfter()
    {
        var store = new InMemoryRateLimitStore();
        var options = BuildOptions(store);
        var pipeline = new TierGatePipeline<string, TestTier>(options);

        await pipeline.InvokeAsync(BuildContext(), () => Task.CompletedTask);
        await pipeline.InvokeAsync(BuildContext(), () => Task.CompletedTask);
        var thirdContext = BuildContext();
        var nextCalled = false;

        await pipeline.InvokeAsync(thirdContext, () => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, thirdContext.Response.StatusCode);
        Assert.True(thirdContext.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task ExceedsQuotaLimit_Returns402()
    {
        var store = new InMemoryRateLimitStore();
        var options = new TierGateOptions<string, TestTier>
        {
            Store = store,
            ExtractSubject = _ => "subject-1",
            ResolveTierAsync = (_, _) => Task.FromResult<TestTier?>(new TestTier(0, RateLimitKind.Quota)),
            GetLimits = tier => [new TierLimit(RateLimitWindow.CalendarMonth, tier.Limit, tier.Kind)],
            GetStoreKey = s => s,
        };
        var pipeline = new TierGatePipeline<string, TestTier>(options);
        var context = BuildContext();

        await pipeline.InvokeAsync(context, () => Task.CompletedTask);

        Assert.Equal(402, context.Response.StatusCode);
    }

    [Fact]
    public async Task StoreUnavailable_FailClosedByDefault_Denies()
    {
        var options = BuildOptions(new AlwaysUnavailableStore());
        var pipeline = new TierGatePipeline<string, TestTier>(options);
        var context = BuildContext();

        var nextCalled = false;
        await pipeline.InvokeAsync(context, () => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, context.Response.StatusCode);
    }

    [Fact]
    public async Task StoreUnavailable_FailOpenConfigured_Allows()
    {
        var options = BuildOptions(new AlwaysUnavailableStore(), failOpen: true);
        var pipeline = new TierGatePipeline<string, TestTier>(options);
        var context = BuildContext();

        var nextCalled = false;
        await pipeline.InvokeAsync(context, () => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
    }
}
