using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TierGate.Core.Gating;

namespace TierGate.AspNetCore.Gating;

public static class GateResultExtensions
{
    /// <summary>
    /// Converts a denied <see cref="GateResult"/> into a 402 Upgrade Required response, or
    /// null when allowed — pairs with the pattern
    /// <c>if (TierGate.ValidateMax(...).ToActionResultOrNull() is { } result) return result;</c>
    /// </summary>
    public static IActionResult? ToActionResultOrNull(this GateResult result)
    {
        if (result.Allowed)
            return null;

        var problem = new ProblemDetails
        {
            Type = "about:upgrade-required",
            Title = "Upgrade Required",
            Status = StatusCodes.Status402PaymentRequired,
            Detail = result.Detail,
        };

        if (result.Feature is not null)
            problem.Extensions["feature"] = result.Feature;

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status402PaymentRequired };
    }
}
