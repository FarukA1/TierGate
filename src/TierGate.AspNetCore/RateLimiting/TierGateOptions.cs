using Microsoft.AspNetCore.Http;
using TierGate.Core.RateLimiting;

namespace TierGate.AspNetCore.RateLimiting;

/// <summary>
/// Configuration for <see cref="ApplicationBuilderExtensions.UseTierGate{TSubject,TTier}"/>.
/// <typeparamref name="TSubject"/> and <typeparamref name="TTier"/> are left entirely to the
/// app — a subject might be a raw API key or an already-validated subscription object; a tier
/// might be a string, an enum, or a richer policy type.
/// </summary>
public sealed class TierGateOptions<TSubject, TTier>
{
    public required IRateLimitStore Store { get; init; }

    /// <summary>Extracts the caller's identity from the request. Return null if none is present.</summary>
    public required Func<HttpContext, TSubject?> ExtractSubject { get; init; }

    /// <summary>
    /// Resolves the subject's tier. Return null to reject the request as unauthorized —
    /// the library has no opinion on fallback/demotion policy; implement that here if wanted.
    /// </summary>
    public required Func<TSubject, CancellationToken, Task<TTier?>> ResolveTierAsync { get; init; }

    /// <summary>The windows/limits to enforce for a resolved tier, checked in order.</summary>
    public required Func<TTier, IReadOnlyList<TierLimit>> GetLimits { get; init; }

    /// <summary>The key passed to <see cref="IRateLimitStore"/> to identify this subject's counters.</summary>
    public required Func<TSubject, string> GetStoreKey { get; init; }

    /// <summary>Request paths that bypass tier gating entirely (health checks, auth, billing, etc.).</summary>
    public IReadOnlyList<PathString> ExcludedPaths { get; init; } = Array.Empty<PathString>();

    /// <summary>
    /// When true, a <see cref="RateLimitOutcome.StoreUnavailable"/> result is treated as allowed.
    /// Defaults to false — fail closed rather than let an unmetered request through.
    /// </summary>
    public bool FailOpenOnStoreUnavailable { get; init; }
}
