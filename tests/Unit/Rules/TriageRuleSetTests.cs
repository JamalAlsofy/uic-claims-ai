using Shouldly;
using Uic.Claims.Ai.Core.Claims;
using Uic.Claims.Ai.Core.Rules;
using Uic.Claims.Ai.UnitTests.Claims;
using Xunit;

namespace Uic.Claims.Ai.UnitTests.Rules;

public class TriageRuleSetTests
{
    private readonly TriageRuleSet _rules = TriageRuleSet.Default;

    [Fact]
    public void Settled_claim_needs_no_review_and_no_model()
    {
        var claim = new ClaimBuilder().WithStatus("PAID").Build();

        var result = _rules.Evaluate(claim);

        result.ShouldNotBeNull();
        result.Category.ShouldBe("CLOSED");
        result.RequiresHumanReview.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Fewer_than_four_prior_claims_do_not_trigger_the_repeat_rule(int history)
    {
        var claim = new ClaimBuilder().WithHistory(history).WithAmount(800_000m).Build();

        _rules.Evaluate(claim)?.Category.ShouldNotBe("REPEAT_CLAIM");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(11)]
    public void Four_or_more_prior_claims_go_to_the_fraud_queue(int history)
    {
        var claim = new ClaimBuilder().WithHistory(history).Build();

        var result = _rules.Evaluate(claim);

        result.ShouldNotBeNull();
        result.Category.ShouldBe("REPEAT_CLAIM");
        result.RequiresHumanReview.ShouldBeTrue();
        result.Flags.ShouldContain(ClaimFlags.RepeatClaimant);
    }

    [Theory]
    [InlineData(49_999, 1, false, true)]   // just inside the ceiling
    [InlineData(50_001, 1, false, false)]  // just outside
    [InlineData(1_000, 8, false, false)]   // too old
    [InlineData(1_000, 1, true, false)]    // over the policy limit
    public void Fast_track_needs_all_three_conditions(
        decimal amount, int ageDays, bool aboveLimit, bool expected)
    {
        var claim = new ClaimBuilder().WithAmount(amount).WithAge(ageDays).Build();
        if (aboveLimit) claim = claim with { AboveLimit = 1 };

        var result = _rules.Evaluate(claim);
        var fastTracked = result?.Category == "FAST_TRACK";

        fastTracked.ShouldBe(expected);
    }

    [Fact]
    public void Ambiguous_claim_is_left_to_the_model()
    {
        var claim = new ClaimBuilder().WithAmount(400_000m).WithAge(20).WithHistory(1).Build();

        _rules.Evaluate(claim).ShouldBeNull();
    }

    [Fact]
    public void Rules_are_ordered_and_names_are_stable()
    {
        _rules.RuleNames.ShouldBe(["already-settled", "repeat-claimant", "fast-track"]);
    }

    /// <summary>
    /// Regression guard. Twice during design the "fast-track" rule was tempted into
    /// saying APPROVED. Nothing in this layer may ever emit a payment decision.
    /// </summary>
    [Fact]
    public void No_rule_ever_emits_a_payment_decision()
    {
        var forbidden = new[] { "APPROVE", "APPROVED", "ACCEPT", "REJECT", "DENY", "DENIED" };
        var samples = new[]
        {
            new ClaimBuilder().WithStatus("PAID").Build(),
            new ClaimBuilder().WithHistory(9).Build(),
            new ClaimBuilder().WithAmount(500m).WithAge(1).Build()
        };

        foreach (var claim in samples)
        {
            var category = _rules.Evaluate(claim)!.Category;
            forbidden.ShouldNotContain(category);
        }
    }
}
