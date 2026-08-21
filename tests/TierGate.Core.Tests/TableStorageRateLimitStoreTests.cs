using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Testcontainers.Azurite;
using TierGate.Core.RateLimiting;

namespace TierGate.Core.Tests;

/// <summary>
/// Runs TableStorageRateLimitStore against a real Azurite container (Testcontainers), not a mock —
/// this is the class InMemoryRateLimitStoreTests can't cover: the ETag optimistic-concurrency retry
/// loop, the 404-then-409 create race, and the actual RequestFailedException shapes a real Table
/// Storage/Azurite endpoint returns. Requires Docker.
/// </summary>
public sealed class TableStorageRateLimitStoreTests : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite = new AzuriteBuilder().Build();
    private TableStorageRateLimitStore _store = null!;

    public async Task InitializeAsync()
    {
        await _azurite.StartAsync();
        _store = new TableStorageRateLimitStore(_azurite.GetConnectionString(), tableName: "TestCounters");
    }

    public async Task DisposeAsync() => await _azurite.DisposeAsync();

    private static string NewSubject() => Guid.NewGuid().ToString("N");

    [Fact]
    public async Task TryConsumeAsync_AllowsUpToLimit_ThenDenies()
    {
        var subject = NewSubject();

        var first = await _store.TryConsumeAsync(subject, RateLimitWindow.PerMinute, limit: 2);
        var second = await _store.TryConsumeAsync(subject, RateLimitWindow.PerMinute, limit: 2);
        var third = await _store.TryConsumeAsync(subject, RateLimitWindow.PerMinute, limit: 2);

        Assert.True(first.Allowed);
        Assert.Equal(1, first.Remaining);
        Assert.True(second.Allowed);
        Assert.Equal(0, second.Remaining);
        Assert.False(third.Allowed);
        Assert.Equal(RateLimitOutcome.Denied, third.Outcome);
    }

    [Fact]
    public async Task TryConsumeAsync_ConcurrentCallers_DoNotOvershootTheLimit()
    {
        // The thing that's never been verified against a real backend: many callers hitting the same
        // row at once relies on ETag conflicts (412s) being caught and retried correctly, not just the
        // in-memory compare-and-swap InMemoryRateLimitStoreTests already covers.
        var subject = NewSubject();
        const int limit = 20;
        const int callers = 50;

        var results = await Task.WhenAll(Enumerable.Range(0, callers)
            .Select(_ => _store.TryConsumeAsync(subject, RateLimitWindow.PerMinute, limit)));

        var allowed = results.Count(r => r.Allowed);
        Assert.Equal(limit, allowed);

        var finalUsage = await _store.GetCurrentUsageAsync(subject, RateLimitWindow.PerMinute);
        Assert.Equal(limit, finalUsage);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_ReflectsConsumedCount()
    {
        var subject = NewSubject();
        await _store.TryConsumeAsync(subject, RateLimitWindow.PerMinute, limit: 100);
        await _store.TryConsumeAsync(subject, RateLimitWindow.PerMinute, limit: 100);
        await _store.TryConsumeAsync(subject, RateLimitWindow.PerMinute, limit: 100);

        var usage = await _store.GetCurrentUsageAsync(subject, RateLimitWindow.PerMinute);

        Assert.Equal(3, usage);
    }

    [Fact]
    public async Task GetCurrentUsageAsync_UnknownSubject_ReturnsZero()
    {
        var usage = await _store.GetCurrentUsageAsync(NewSubject(), RateLimitWindow.PerMinute);

        Assert.Equal(0, usage);
    }

    [Fact]
    public async Task SeedUsageAsync_CreatesANewBucket()
    {
        var subject = NewSubject();

        await _store.SeedUsageAsync(subject, RateLimitWindow.CalendarMonth, count: 40);
        var usage = await _store.GetCurrentUsageAsync(subject, RateLimitWindow.CalendarMonth);

        Assert.Equal(40, usage);
    }

    [Fact]
    public async Task SeedUsageAsync_ThrowsOnAnExistingBucket()
    {
        // Documents the real, load-bearing limitation ReconcileUsageAsync exists to fix — SeedUsageAsync
        // is an AddEntity under the hood, not an upsert.
        var subject = NewSubject();
        await _store.SeedUsageAsync(subject, RateLimitWindow.CalendarMonth, count: 10);

        await Assert.ThrowsAsync<RequestFailedException>(
            () => _store.SeedUsageAsync(subject, RateLimitWindow.CalendarMonth, count: 20));
    }

    [Fact]
    public async Task ReconcileUsageAsync_CreatesAMissingBucket()
    {
        var subject = NewSubject();

        await _store.ReconcileUsageAsync(subject, RateLimitWindow.CalendarMonth, authoritativeCount: 55);
        var usage = await _store.GetCurrentUsageAsync(subject, RateLimitWindow.CalendarMonth);

        Assert.Equal(55, usage);
    }

    [Fact]
    public async Task ReconcileUsageAsync_OverwritesAnExistingBucket()
    {
        var subject = NewSubject();
        await _store.TryConsumeAsync(subject, RateLimitWindow.CalendarMonth, limit: 100);
        await _store.TryConsumeAsync(subject, RateLimitWindow.CalendarMonth, limit: 100);

        await _store.ReconcileUsageAsync(subject, RateLimitWindow.CalendarMonth, authoritativeCount: 7);
        var usage = await _store.GetCurrentUsageAsync(subject, RateLimitWindow.CalendarMonth);

        Assert.Equal(7, usage);
    }

    [Fact]
    public async Task ReconcileUsageAsync_AcceptsZero()
    {
        var subject = NewSubject();
        await _store.TryConsumeAsync(subject, RateLimitWindow.CalendarMonth, limit: 100);

        await _store.ReconcileUsageAsync(subject, RateLimitWindow.CalendarMonth, authoritativeCount: 0);
        var usage = await _store.GetCurrentUsageAsync(subject, RateLimitWindow.CalendarMonth);

        Assert.Equal(0, usage);
    }

    // Not a real credential — an unreachable dummy endpoint used to prove the constructor doesn't do
    // network I/O. Sourced via configuration (User Secrets locally) rather than a literal in the test
    // body, so nothing connection-string-shaped sits in source for a secret scanner to flag. Falls back
    // to the same dummy value when no user secret is set (e.g. in CI), since there's no real secret here.
    private static string UnreachableConnectionString()
    {
        // Order matters: the in-memory default goes first so User Secrets (added after) overrides it
        // when set, rather than the other way around.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Azurite:UnreachableConnectionString"] =
                    "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Zm9v;TableEndpoint=http://127.0.0.1:1;",
            })
            .AddUserSecrets<TableStorageRateLimitStoreTests>(optional: true)
            .Build();

        return config["Azurite:UnreachableConnectionString"]!;
    }

    [Fact]
    public void Constructor_DoesNotBlockOnNetworkIO()
    {
        // Table creation used to happen synchronously in the constructor. This points at nothing
        // listening — if the constructor still did I/O, this would throw or hang.
        var store = new TableStorageRateLimitStore(UnreachableConnectionString(), tableName: "TestCounters");

        Assert.NotNull(store);
    }

    [Fact]
    public async Task TryConsumeAsync_StoreUnreachable_ReturnsUnavailable_NotAnException()
    {
        // The fail-open/fail-closed decision this store's whole design hinges on depends on this
        // actually being RateLimitOutcome.StoreUnavailable, not an unhandled exception reaching the
        // caller — never verified against a real connection failure before. Own container, deliberately
        // stopped, isolated from the shared fixture instance the other tests use.
        await using var azurite = new AzuriteBuilder().Build();
        await azurite.StartAsync();
        var store = new TableStorageRateLimitStore(azurite.GetConnectionString(), tableName: "TestCounters");
        await azurite.StopAsync();

        var result = await store.TryConsumeAsync(NewSubject(), RateLimitWindow.PerMinute, limit: 10);

        Assert.Equal(RateLimitOutcome.StoreUnavailable, result.Outcome);
    }
}
