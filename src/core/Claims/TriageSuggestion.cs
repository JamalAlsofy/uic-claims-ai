namespace Uic.Claims.Ai.Core.Claims;

/// <summary>A proposal only. Nothing here mutates claim state.</summary>
public sealed record TriageSuggestion(
    string Category,
    double Confidence,
    double RiskScore,
    IReadOnlyList<string> Flags,
    string SummaryAr)
{
    /// <summary>
    /// Set by rules, never by the model alone. Financial exposure always ends up
    /// in a human queue regardless of how confident the model claims to be.
    /// </summary>
    public bool RequiresHumanReview { get; init; } = true;

    public TriageSuggestion WithFlag(string flag) =>
        this with { Flags = [.. Flags, flag], RequiresHumanReview = true };
}

public static class ClaimFlags
{
    public const string AbovePolicyLimit = "ABOVE_POLICY_LIMIT";
    public const string RepeatClaimant = "REPEAT_CLAIMANT";
    public const string StaleClaim = "STALE_CLAIM";
    public const string AiUnavailable = "AI_UNAVAILABLE";
    public const string AiInvalidOutput = "AI_INVALID_OUTPUT";
}
