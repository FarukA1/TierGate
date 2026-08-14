using TierGate.AspNetCore.RateLimiting;
using TierGate.Core.RateLimiting;

namespace TierGate.AspNetDemo;

public sealed record DemoTier(string Name, int MaxPageSize, IReadOnlyList<TierLimit> Limits);

public static class DemoTiers
{
    public static readonly DemoTier Free = new(
        Name: "Free",
        MaxPageSize: 10,
        Limits:
        [
            new TierLimit(RateLimitWindow.PerMinute, 5, RateLimitKind.Throttle),
            new TierLimit(RateLimitWindow.CalendarMonth, 100, RateLimitKind.Quota),
        ]);

    public static readonly DemoTier Pro = new(
        Name: "Pro",
        MaxPageSize: 100,
        Limits:
        [
            new TierLimit(RateLimitWindow.PerMinute, 60, RateLimitKind.Throttle),
            new TierLimit(RateLimitWindow.CalendarMonth, 10_000, RateLimitKind.Quota),
        ]);

    // Demo-only hardcoded lookup — a real app resolves this from a database.
    private static readonly Dictionary<string, DemoTier> ApiKeys = new()
    {
        ["free-demo-key"] = Free,
        ["pro-demo-key"] = Pro,
    };

    public static DemoTier? Resolve(string? apiKey) =>
        apiKey is not null && ApiKeys.TryGetValue(apiKey, out var tier) ? tier : null;
}
