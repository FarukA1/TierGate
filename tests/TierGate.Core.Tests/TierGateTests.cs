using static TierGate.Core.Gating.TierGate;

namespace TierGate.Core.Tests;

public class TierGateTests
{
    private sealed record TestPolicy(int MaxPageSize, int? MinDataYear, bool HasStreamingAccess);

    [Fact]
    public void ValidateMax_WithinLimit_Allows()
    {
        var policy = new TestPolicy(MaxPageSize: 50, MinDataYear: null, HasStreamingAccess: false);

        var result = ValidateMax(policy, p => p.MaxPageSize, requested: 20, "page-size");

        Assert.True(result.Allowed);
    }

    [Fact]
    public void ValidateMax_ExceedsLimit_Denies()
    {
        var policy = new TestPolicy(MaxPageSize: 50, MinDataYear: null, HasStreamingAccess: false);

        var result = ValidateMax(policy, p => p.MaxPageSize, requested: 100, "page-size");

        Assert.False(result.Allowed);
        Assert.Equal("page-size", result.Feature);
    }

    [Fact]
    public void ValidateMin_NullMinimum_MeansNoRestriction()
    {
        var policy = new TestPolicy(MaxPageSize: 50, MinDataYear: null, HasStreamingAccess: false);

        var result = ValidateMin(policy, p => p.MinDataYear, requested: 2010, "historic-data");

        Assert.True(result.Allowed);
    }

    [Fact]
    public void ValidateMin_BelowMinimum_Denies()
    {
        var policy = new TestPolicy(MaxPageSize: 50, MinDataYear: 2024, HasStreamingAccess: false);

        var result = ValidateMin(policy, p => p.MinDataYear, requested: 2010, "historic-data");

        Assert.False(result.Allowed);
    }

    [Fact]
    public void ValidateMin_AtOrAboveMinimum_Allows()
    {
        var policy = new TestPolicy(MaxPageSize: 50, MinDataYear: 2024, HasStreamingAccess: false);

        var result = ValidateMin(policy, p => p.MinDataYear, requested: 2024, "historic-data");

        Assert.True(result.Allowed);
    }

    [Fact]
    public void RequireFeature_Allowed_Allows()
    {
        var policy = new TestPolicy(MaxPageSize: 50, MinDataYear: null, HasStreamingAccess: true);

        var result = RequireFeature(policy, p => p.HasStreamingAccess, "streaming");

        Assert.True(result.Allowed);
    }

    [Fact]
    public void RequireFeature_NotAllowed_DeniesWithDefaultDetail()
    {
        var policy = new TestPolicy(MaxPageSize: 50, MinDataYear: null, HasStreamingAccess: false);

        var result = RequireFeature(policy, p => p.HasStreamingAccess, "streaming");

        Assert.False(result.Allowed);
        Assert.Equal("streaming", result.Feature);
        Assert.Contains("streaming", result.Detail);
    }
}
