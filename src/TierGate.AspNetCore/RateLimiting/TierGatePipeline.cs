using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TierGate.Core.RateLimiting;

namespace TierGate.AspNetCore.RateLimiting;

internal sealed class TierGatePipeline<TSubject, TTier>(TierGateOptions<TSubject, TTier> options)
{
    public async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (IsExcluded(context.Request.Path))
        {
            await next();
            return;
        }

        var subject = options.ExtractSubject(context);
        if (subject is null)
        {
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Missing or invalid credentials.");
            return;
        }

        var tier = await options.ResolveTierAsync(subject, context.RequestAborted);
        if (tier is null)
        {
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized,
                "Unable to resolve a subscription tier for the supplied credentials.");
            return;
        }

        var storeKey = options.GetStoreKey(subject);

        foreach (var limit in options.GetLimits(tier))
        {
            var result = await options.Store.TryConsumeAsync(
                storeKey, limit.Window, limit.Limit, context.RequestAborted);

            if (result.Outcome == RateLimitOutcome.StoreUnavailable && options.FailOpenOnStoreUnavailable)
                continue;

            WriteLimitHeaders(context, limit, result);

            if (!result.Allowed)
            {
                await WriteProblemAsync(context, StatusCodeFor(limit.Kind), DetailFor(limit));
                return;
            }
        }

        context.Items[TierGateItemKeys.Tier] = tier;
        await next();
    }

    private bool IsExcluded(PathString path) =>
        options.ExcludedPaths.Any(excluded => path.StartsWithSegments(excluded));

    private static string ResolveHeaderPrefix(TierLimit limit) =>
        limit.HeaderPrefix ?? (limit.Kind == RateLimitKind.Throttle ? "X-RateLimit" : "X-Quota");

    private static void WriteLimitHeaders(HttpContext context, TierLimit limit, RateLimitResult result)
    {
        var prefix = ResolveHeaderPrefix(limit);
        context.Response.Headers[$"{prefix}-Limit"] = result.Limit.ToString();
        context.Response.Headers[$"{prefix}-Remaining"] = Math.Max(0, result.Remaining).ToString();

        // ResetsAt is DateTimeOffset.MinValue when the store couldn't determine one
        // (RateLimitResult.Unavailable) — nothing meaningful to report in that case.
        if (result.ResetsAt == DateTimeOffset.MinValue)
            return;

        context.Response.Headers[$"{prefix}-Reset"] = result.ResetsAt.ToUnixTimeSeconds().ToString();

        if (!result.Allowed)
        {
            var retryAfterSeconds = Math.Max(0, (int)Math.Ceiling((result.ResetsAt - DateTimeOffset.UtcNow).TotalSeconds));
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        }
    }

    private static HttpStatusCode StatusCodeFor(RateLimitKind kind) =>
        kind == RateLimitKind.Throttle ? HttpStatusCode.TooManyRequests : HttpStatusCode.PaymentRequired;

    private static string DetailFor(TierLimit limit) =>
        limit.Kind == RateLimitKind.Throttle
            ? "Rate limit exceeded."
            : "Quota exceeded. Upgrade your subscription to continue.";

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode statusCode, string detail)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        var body = JsonSerializer.Serialize(new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title = statusCode.ToString(),
            status = (int)statusCode,
            detail
        });
        await context.Response.WriteAsync(body, context.RequestAborted);
    }
}
