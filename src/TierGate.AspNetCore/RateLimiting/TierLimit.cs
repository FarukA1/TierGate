using TierGate.Core.RateLimiting;

namespace TierGate.AspNetCore.RateLimiting;

/// <summary>
/// One window/limit pair to enforce for a tier. A tier can have any number of these —
/// e.g. a per-minute throttle and a calendar-month quota — checked in order, stopping
/// at the first denial.
/// </summary>
/// <param name="Window">The counting window, e.g. <see cref="RateLimitWindow.PerMinute"/>.</param>
/// <param name="Limit">The maximum allowed within the window.</param>
/// <param name="Kind">Whether this is a throttle (429) or a quota (402).</param>
/// <param name="HeaderPrefix">
/// Response header prefix, e.g. "X-RateLimit" produces X-RateLimit-Limit/Remaining/Reset.
/// Defaults to "X-RateLimit" for <see cref="RateLimitKind.Throttle"/> and "X-Quota" for
/// <see cref="RateLimitKind.Quota"/>. Set explicitly if a tier has more than one limit
/// of the same kind, to avoid header collisions.
/// </param>
public sealed record TierLimit(
    RateLimitWindow Window,
    int Limit,
    RateLimitKind Kind,
    string? HeaderPrefix = null);
