namespace TierGate.Core.Gating;

public sealed record GateResult(bool Allowed, string? Feature = null, string? Detail = null)
{
    public static readonly GateResult Allow = new(true);

    public static GateResult Deny(string feature, string detail) => new(false, feature, detail);
}
