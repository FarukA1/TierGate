# TierGate.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/TierGate.AspNetCore)](https://www.nuget.org/packages/TierGate.AspNetCore)

ASP.NET Core middleware for [TierGate.Core](https://www.nuget.org/packages/TierGate.Core) — tiered rate limiting and quota enforcement.

## Installation

```bash
dotnet add package TierGate.AspNetCore
```

## Quick start

```csharp
using TierGate.AspNetCore.RateLimiting;
using TierGate.Core.RateLimiting;

app.UseTierGate(new TierGateOptions<string, MyTier>
{
    Store = app.Services.GetRequiredService<IRateLimitStore>(),

    // How to identify the caller. Return null for "no credentials" -> 401.
    ExtractSubject = ctx => ctx.Request.Headers["X-Api-Key"].FirstOrDefault(),

    // How to resolve a tier for that caller. Return null -> 401. No fallback/demotion
    // policy is baked in here — implement that inside your own resolver if you want it.
    ResolveTierAsync = async (apiKey, ct) => await myTierService.ResolveAsync(apiKey, ct),

    // Which windows/limits apply for a given tier, checked in order.
    GetLimits = tier =>
    [
        new TierLimit(RateLimitWindow.PerMinute, tier.RateLimitPerMinute, RateLimitKind.Throttle),
        new TierLimit(RateLimitWindow.CalendarMonth, tier.MonthlyQuota, RateLimitKind.Quota),
    ],

    GetStoreKey = apiKey => apiKey,
    ExcludedPaths = [new PathString("/health"), new PathString("/swagger")],
});
```

On success, the resolved tier is available downstream via `HttpContext.GetTierGateTier<MyTier>()`. On denial, the middleware writes an `application/problem+json` response — 429 for a `Throttle` limit (with an accurate `Retry-After`), 402 for a `Quota` limit — and sets `X-RateLimit-*`/`X-Quota-*` headers (customizable per `TierLimit`).

For feature/limit gating inside a controller action (separate from the rate-limit middleware), use `TierGate.Core.Gating.TierGate`'s `RequireFeature`/`ValidateMax`/`ValidateMin` helpers together with `GateResultExtensions.ToActionResultOrNull()`:

```csharp
using static TierGate.Core.Gating.TierGate;
using TierGate.AspNetCore.Gating;

if (ValidateMax(policy, p => p.MaxPageSize, requested: pageSize, "page-size").ToActionResultOrNull() is { } result)
    return result;
```

## Contribution

Contributions are welcome. If you find a bug or have an enhancement in mind, feel free to open an issue or submit a pull request.
