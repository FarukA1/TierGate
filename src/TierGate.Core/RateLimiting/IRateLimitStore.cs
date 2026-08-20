namespace TierGate.Core.RateLimiting;

public interface IRateLimitStore
{
    /// <summary>
    /// Attempts to consume one unit against <paramref name="subjectKey"/>'s counter for the
    /// given <paramref name="window"/>. Implementations must not throw for their own transient
    /// failures (e.g. an unreachable backend) — return <see cref="RateLimitOutcome.StoreUnavailable"/>
    /// instead, so callers can make an explicit fail-open/fail-closed decision.
    /// </summary>
    Task<RateLimitResult> TryConsumeAsync(
        string subjectKey,
        RateLimitWindow window,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the current count for the window without consuming a unit.</summary>
    Task<int> GetCurrentUsageAsync(
        string subjectKey,
        RateLimitWindow window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an initial count for a window bucket that does not yet exist — e.g. to carry over
    /// or prorate usage when a subscriber's tier changes mid-cycle. No-ops for counts &lt;= 0.
    /// </summary>
    Task SeedUsageAsync(
        string subjectKey,
        RateLimitWindow window,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites a window bucket's count with an authoritative value from outside the store's own
    /// counting — e.g. a provider's real usage report, used to correct drift. Creates the bucket if
    /// absent, otherwise replaces it. Unlike <see cref="SeedUsageAsync"/>, this also accepts 0 (a
    /// legitimate authoritative reading, not "nothing to seed").
    /// </summary>
    Task ReconcileUsageAsync(
        string subjectKey,
        RateLimitWindow window,
        int authoritativeCount,
        CancellationToken cancellationToken = default);
}
