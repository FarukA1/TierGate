namespace TierGate.Core.RateLimiting;

public readonly record struct RateLimitResult(
    RateLimitOutcome Outcome,
    int Remaining,
    int Limit,
    DateTimeOffset ResetsAt)
{
    public bool Allowed => Outcome == RateLimitOutcome.Allowed;

    public static RateLimitResult Allow(int remaining, int limit, DateTimeOffset resetsAt) =>
        new(RateLimitOutcome.Allowed, remaining, limit, resetsAt);

    public static RateLimitResult Deny(int limit, DateTimeOffset resetsAt) =>
        new(RateLimitOutcome.Denied, 0, limit, resetsAt);

    // No ResetsAt available — the store couldn't reach its backend to determine one.
    public static RateLimitResult Unavailable(int limit) =>
        new(RateLimitOutcome.StoreUnavailable, 0, limit, DateTimeOffset.MinValue);
}
