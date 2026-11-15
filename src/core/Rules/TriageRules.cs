using Uic.Claims.Ai.Core.Claims;

namespace Uic.Claims.Ai.Core.Rules;

// Thresholds below came from the claims team's own escalation sheet (v3, Oct 2026).
// If those numbers change, they change here and nowhere else.

internal sealed class AlreadySettledRule : ITriageRule
{
    public string Name => "already-settled";
    public int Order => 10;
    public bool Matches(ClaimContext c) => c.IsClosed;

    public TriageSuggestion Apply(ClaimContext c) =>
        new("CLOSED", 1.0, 0, [], "المطالبة مسوّاة أو مغلقة، لا حاجة إلى فرز.") { RequiresHumanReview = false };
}

internal sealed class RepeatClaimantRule : ITriageRule
{
    // 4+ claims in 12 months is the point where the fraud desk wants to see it first.
    private const int Threshold = 4;

    public string Name => "repeat-claimant";
    public int Order => 20;
    public bool Matches(ClaimContext c) => c.ClaimsLast12Months >= Threshold;

    public TriageSuggestion Apply(ClaimContext c) =>
        new("REPEAT_CLAIM", 0.95, 90, [ClaimFlags.RepeatClaimant],
            $"المؤمَّن قدّم {c.ClaimsLast12Months} مطالبات خلال 12 شهراً.")
        { RequiresHumanReview = true };
}

internal sealed class FastTrackRule : ITriageRule
{
    private const decimal AmountCeiling = 50_000m;
    private const int AgeCeilingDays = 7;

    public string Name => "fast-track";
    public int Order => 30;

    public bool Matches(ClaimContext c) =>
        c.ClaimedAmount <= AmountCeiling && c.AgeDays <= AgeCeilingDays && !c.IsAboveLimit;

    public TriageSuggestion Apply(ClaimContext c) =>
        new("FAST_TRACK", 0.9, 10, [], "مبلغ صغير ضمن السقف ومكتمل خلال أسبوع.")
        { RequiresHumanReview = false };
}

public sealed class TriageRuleSet(IEnumerable<ITriageRule> rules)
{
    private readonly ITriageRule[] _ordered = rules.OrderBy(r => r.Order).ToArray();

    public static TriageRuleSet Default { get; } = new(
    [
        new AlreadySettledRule(),
        new RepeatClaimantRule(),
        new FastTrackRule()
    ]);

    /// <summary>Returns null when the claim is genuinely ambiguous and needs the model.</summary>
    public TriageSuggestion? Evaluate(ClaimContext claim)
    {
        foreach (var rule in _ordered)
        {
            if (rule.Matches(claim))
            {
                return rule.Apply(claim);
            }
        }

        return null;
    }

    public IReadOnlyList<string> RuleNames => [.. _ordered.Select(r => r.Name)];
}
