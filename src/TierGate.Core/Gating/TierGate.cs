namespace TierGate.Core.Gating;

/// <summary>
/// Generic tier-policy gate helpers. These never know what a policy's fields mean — the
/// consuming app defines its own policy type and points a selector at whichever field
/// it wants checked.
/// </summary>
public static class TierGate
{
    public static GateResult RequireFeature<T>(
        T policy, Func<T, bool> isAllowed, string feature, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(isAllowed);
        return isAllowed(policy)
            ? GateResult.Allow
            : GateResult.Deny(feature, detail ?? $"The '{feature}' feature requires a higher tier.");
    }

    public static GateResult ValidateMax<T>(
        T policy, Func<T, int> limitSelector, int requested, string paramName)
    {
        ArgumentNullException.ThrowIfNull(limitSelector);
        var limit = limitSelector(policy);
        return requested <= limit
            ? GateResult.Allow
            : GateResult.Deny(paramName, $"{paramName} of {requested} exceeds the tier limit of {limit}.");
    }

    /// <summary>A null <paramref name="minSelector"/> result means no restriction.</summary>
    public static GateResult ValidateMin<T>(
        T policy, Func<T, int?> minSelector, int requested, string paramName)
    {
        ArgumentNullException.ThrowIfNull(minSelector);
        var min = minSelector(policy);
        return min is null || requested >= min
            ? GateResult.Allow
            : GateResult.Deny(paramName, $"{paramName} of {requested} is below the tier minimum of {min}.");
    }
}
