using Microsoft.AspNetCore.Mvc;
using TierGate.AspNetCore.Gating;
using TierGate.AspNetCore.RateLimiting;
using static TierGate.Core.Gating.TierGate;

namespace TierGate.AspNetDemo.Controllers;

[ApiController]
[Route("[controller]")]
public class DemoController : ControllerBase
{
    // Gated entirely by the UseTierGate middleware. Hit this repeatedly with the same
    // X-Api-Key to watch X-RateLimit-* headers count down, then a 429 once exhausted.
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        var tier = HttpContext.GetTierGateTier<DemoTier>();
        return Ok(new { message = "pong", tier = tier?.Name });
    }

    // Demonstrates the generic TierGate.Core gate helpers directly, independent of the
    // rate-limit middleware. Try ?pageSize=50 with free-demo-key vs pro-demo-key.
    [HttpGet("items")]
    public IActionResult GetItems([FromQuery] int pageSize = 10)
    {
        var tier = HttpContext.GetTierGateTier<DemoTier>();
        if (tier is null)
            return Unauthorized();

        if (ValidateMax(tier, t => t.MaxPageSize, pageSize, "page-size").ToActionResultOrNull() is { } result)
            return result;

        return Ok(Enumerable.Range(1, pageSize).Select(i => $"item-{i}"));
    }
}
