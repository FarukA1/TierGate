using Microsoft.AspNetCore.Builder;

namespace TierGate.AspNetCore.RateLimiting;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds tier-based rate limiting and quota enforcement to the pipeline. See
    /// <see cref="TierGateOptions{TSubject,TTier}"/> for what the app needs to supply.
    /// </summary>
    public static IApplicationBuilder UseTierGate<TSubject, TTier>(
        this IApplicationBuilder app, TierGateOptions<TSubject, TTier> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pipeline = new TierGatePipeline<TSubject, TTier>(options);
        return app.Use(pipeline.InvokeAsync);
    }
}
