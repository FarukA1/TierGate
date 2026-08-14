# TierGate.AspNetCore

ASP.NET Core middleware for [TierGate.Core](https://www.nuget.org/packages/TierGate.Core) — tiered rate limiting and quota enforcement.

**Status: not yet implemented.** This package is scaffolded but the middleware itself — tier resolution, `IRateLimitStore` wiring, response headers, and `GateResult` → `IActionResult`/`IResult` conversion — hasn't been built yet.

## Installation

```bash
dotnet add package TierGate.AspNetCore
```

## Contribution

Contributions are welcome. If you find a bug or have an enhancement in mind, feel free to open an issue or submit a pull request.
