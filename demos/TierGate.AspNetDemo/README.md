# TierGate.AspNetDemo

A minimal ASP.NET Core Web API showing `TierGate.AspNetCore` end to end, using `InMemoryRateLimitStore` (enough for a demo — swap in `TableStorageRateLimitStore` for production, multi-instance deployments).

## Run it

```bash
dotnet run --project demos/TierGate.AspNetDemo
```

Two hardcoded demo API keys, each mapped to a tier with different limits (see `DemoTiers.cs`):

| Key | Tier | Throttle | Monthly quota | Max page size |
|---|---|---|---|---|
| `free-demo-key` | Free | 5/min | 100 | 10 |
| `pro-demo-key` | Pro | 60/min | 10,000 | 100 |

## Try it

```bash
# No key -> 401
curl -i http://localhost:5000/demo/ping

# Within limits -> 200, with X-RateLimit-*/X-Quota-* headers
curl -i -H "X-Api-Key: free-demo-key" http://localhost:5000/demo/ping

# Call it 6 times in a row on the Free key -> 429 with an accurate Retry-After
for i in $(seq 1 6); do curl -s -o /dev/null -w "%{http_code}\n" -H "X-Api-Key: free-demo-key" http://localhost:5000/demo/ping; done

# TierGate.Core's gate helpers, independent of the rate-limit middleware -> 402 once over the tier's page-size limit
curl -i -H "X-Api-Key: pro-demo-key" "http://localhost:5000/demo/items?pageSize=200"
```
