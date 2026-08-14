# TierGate.Core

Tiered rate-limiting and quota tracking for .NET.

## Overview

`TierGate.Core` provides subscription-tier-aware rate limiting: per-window request limits (per-second, per-minute, per-hour, per-day, or calendar-month quotas), pluggable storage backends, and a generic tier-policy gate for feature/limit checks — all framework-agnostic.

Extracted and generalized from a tiered rate-limiting system built and run in production for a real SaaS API.

## Key features

- Arbitrary rate-limit windows (`PerSecond`, `PerMinute`, `PerHour`, `PerDay`, `CalendarMonth`, or a custom window) via one `IRateLimitStore.TryConsumeAsync` call.
- `TableStorageRateLimitStore` — Azure Table Storage-backed, ETag-safe under concurrency, fails closed by default rather than silently allowing an unmetered request.
- `InMemoryRateLimitStore` — for tests and single-instance apps.
- Generic `TierGate` gate helpers (`RequireFeature`, `ValidateMax`, `ValidateMin`) for tier-based feature/limit checks, parameterized by your own policy type — the library never hardcodes what a "tier" restricts.

## Installation

```bash
dotnet add package TierGate.Core
```

## Quick start

```csharp
using TierGate.Core.RateLimiting;

IRateLimitStore store = new InMemoryRateLimitStore(); // or TableStorageRateLimitStore for production

var result = await store.TryConsumeAsync(subjectKey: apiKeyHash, RateLimitWindow.PerMinute, limit: 60);

if (!result.Allowed)
{
    // result.ResetsAt tells you exactly when the window rolls over
}
```

```csharp
using TierGate.Core.Gating;
using static TierGate.Core.Gating.TierGate;

record MyTierPolicy(int MaxPageSize, bool HasStreamingAccess);

var policy = new MyTierPolicy(MaxPageSize: 50, HasStreamingAccess: false);

var gate = ValidateMax(policy, p => p.MaxPageSize, requested: pageSize, "page-size");
if (!gate.Allowed)
{
    // gate.Feature, gate.Detail
}
```

## Contribution

Contributions are welcome. If you find a bug or have an enhancement in mind, feel free to open an issue or submit a pull request.
