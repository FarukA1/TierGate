namespace TierGate.AspNetCore.RateLimiting;

/// <summary>
/// Whether a <see cref="TierLimit"/> is a throttle (retry later — 429) or a quota
/// (hard cap for the period — 402, upgrade required).
/// </summary>
public enum RateLimitKind
{
    Throttle,
    Quota
}
