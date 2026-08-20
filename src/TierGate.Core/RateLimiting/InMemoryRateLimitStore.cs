using System.Collections.Concurrent;

namespace TierGate.Core.RateLimiting;

/// <summary>
/// Single-process, in-memory rate limit store. Counters are not shared across processes
/// or preserved across restarts — suitable for tests and single-instance apps, not for
/// multi-instance deployments (use <see cref="TableStorageRateLimitStore"/> for that).
/// </summary>
public sealed class InMemoryRateLimitStore : IRateLimitStore
{
    private readonly ConcurrentDictionary<string, int> _counters = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryRateLimitStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<RateLimitResult> TryConsumeAsync(
        string subjectKey, RateLimitWindow window, int limit, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var key = BuildKey(subjectKey, window, now);
        var resetsAt = window.GetResetsAt(now);

        // Compare-and-swap loop: only increments when still under the limit, so a burst of
        // denied requests doesn't push the counter past `limit`.
        while (true)
        {
            var current = _counters.GetOrAdd(key, 0);
            if (current >= limit)
                return Task.FromResult(RateLimitResult.Deny(limit, resetsAt));

            if (_counters.TryUpdate(key, current + 1, current))
                return Task.FromResult(RateLimitResult.Allow(limit - (current + 1), limit, resetsAt));
        }
    }

    public Task<int> GetCurrentUsageAsync(
        string subjectKey, RateLimitWindow window, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var key = BuildKey(subjectKey, window, now);
        return Task.FromResult(_counters.TryGetValue(key, out var count) ? count : 0);
    }

    public Task SeedUsageAsync(
        string subjectKey, RateLimitWindow window, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return Task.CompletedTask;
        var now = _timeProvider.GetUtcNow();
        var key = BuildKey(subjectKey, window, now);
        _counters[key] = count;
        return Task.CompletedTask;
    }

    public Task ReconcileUsageAsync(
        string subjectKey, RateLimitWindow window, int authoritativeCount, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var key = BuildKey(subjectKey, window, now);
        _counters[key] = authoritativeCount;
        return Task.CompletedTask;
    }

    private static string BuildKey(string subjectKey, RateLimitWindow window, DateTimeOffset now) =>
        $"{subjectKey}:{window.GetBucketKey(now)}";
}
