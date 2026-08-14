using Microsoft.AspNetCore.Http;

namespace TierGate.AspNetCore.RateLimiting;

public static class HttpContextExtensions
{
    /// <summary>The tier resolved by <see cref="ApplicationBuilderExtensions.UseTierGate{TSubject,TTier}"/>, if any.</summary>
    public static TTier? GetTierGateTier<TTier>(this HttpContext context) =>
        context.Items.TryGetValue(TierGateItemKeys.Tier, out var value) && value is TTier typed
            ? typed
            : default;
}
