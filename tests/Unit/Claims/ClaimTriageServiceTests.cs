using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Uic.Claims.Ai.Core.Ai;
using Uic.Claims.Ai.Core.Claims;
using Uic.Claims.Ai.Core.Rules;
using Uic.Claims.Ai.UnitTests.Claims;
using Xunit;

namespace Uic.Claims.Ai.UnitTests.Claims;

public class ClaimTriageServiceTests
{
    private readonly IAiClient _ai = Substitute.For<IAiClient>();
    private readonly IClaimRepository _repository = Substitute.For<IClaimRepository>();
    private readonly IAiAuditSink _audit = Substitute.For<IAiAuditSink>();

    private ClaimTriageService CreateService() => new(
        _ai, _repository, _audit, TriageRuleSet.Default,
        NullLogger<ClaimTriageService>.Instance);

    private void GivenClaim(ClaimContext claim) =>
        _repository.FindAsync(claim.ClaimId, Arg.Any<CancellationToken>()).Returns(claim);

    private void GivenModelReturns(string json) =>
        _ai.CompleteAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
           .Returns(new AiResponse(json, new AiUsage("test-model", 120, 60, 900)));

    [Fact]
    public async Task Throws_for_a_claim_that_does_not_exist()
    {
        _repository.FindAsync(9999, Arg.Any<CancellationToken>()).Returns((ClaimContext?)null);

        await Should.ThrowAsync<ClaimNotFoundException>(
            () => CreateService().TriageAsync(9999));
    }

    [Fact]
    public async Task Deterministic_claims_never_reach_the_provider()
    {
        var claim = new ClaimBuilder().WithAmount(20_000m).WithAge(2).Build();
        GivenClaim(claim);

        var result = await CreateService().TriageAsync(claim.ClaimId);

        result.Category.ShouldBe("FAST_TRACK");
        await _ai.DidNotReceive().CompleteAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveSuggestionAsync(
            claim.ClaimId, Arg.Any<TriageSuggestion>(), AiUsage.None, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provider_outage_falls_back_to_manual_review_instead_of_throwing()
    {
        var claim = new ClaimBuilder().Build();
        GivenClaim(claim);
        _ai.CompleteAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
           .ThrowsAsync(new AiUnavailableException("provider down"));

        var result = await CreateService().TriageAsync(claim.ClaimId);

        result.Category.ShouldBe("UNCLASSIFIED");
        result.RequiresHumanReview.ShouldBeTrue();
        result.Flags.ShouldContain(ClaimFlags.AiUnavailable);
        await _repository.Received(1).FlagForReviewAsync(
            claim.ClaimId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Garbage_output_is_stored_as_unclassified_not_as_a_guess()
    {
        var claim = new ClaimBuilder().Build();
        GivenClaim(claim);
        GivenModelReturns("I think this is probably a medical claim.");

        var result = await CreateService().TriageAsync(claim.ClaimId);

        result.Category.ShouldBe("UNCLASSIFIED");
        result.Flags.ShouldContain(ClaimFlags.AiInvalidOutput);
    }

    [Fact]
    public async Task Policy_limit_breach_overrides_a_confident_model_answer()
    {
        var claim = new ClaimBuilder().WithAmount(1_400_000m).WithSumInsured(1_000_000m).Build();
        GivenClaim(claim);
        GivenModelReturns("""
            {"category":"MEDICAL","confidence":0.97,"riskScore":5,"flags":[],"summaryAr":"واضحة","requiresHumanReview":false}
            """);

        var result = await CreateService().TriageAsync(claim.ClaimId);

        result.RequiresHumanReview.ShouldBeTrue("a limit breach always goes to a human");
        result.Flags.ShouldContain(ClaimFlags.AbovePolicyLimit);
    }

    [Fact]
    public async Task Audit_row_is_written_for_every_model_call()
    {
        var claim = new ClaimBuilder().Build();
        GivenClaim(claim);
        GivenModelReturns("""
            {"category":"TRAVEL","confidence":0.8,"riskScore":20,"flags":[],"summaryAr":"سفر","requiresHumanReview":true}
            """);

        await CreateService().TriageAsync(claim.ClaimId);

        await _audit.Received(1).RecordAsync(
            Arg.Is<AiAuditEvent>(e => e.EntityId == claim.ClaimId && e.Model == "test-model"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The payload is built field by field rather than serialising ClaimContext, so that
    /// adding a column to the view can never start shipping personal data to a provider.
    /// </summary>
    [Fact]
    public async Task Payload_contains_no_personal_identifiers()
    {
        var claim = new ClaimBuilder().Build();
        GivenClaim(claim);
        GivenModelReturns("""
            {"category":"MOTOR","confidence":0.8,"riskScore":20,"flags":[],"summaryAr":"س","requiresHumanReview":false}
            """);

        await CreateService().TriageAsync(claim.ClaimId);

        var payload = _ai.ReceivedCalls()
            .Select(c => c.GetArguments()[0] as AiRequest)
            .Where(r => r is not null)
            .Select(r => r!.UserInput)
            .ShouldHaveSingleItem();

        payload.ShouldNotContain("insured", Case.Insensitive);
        payload.ShouldNotContain("national", Case.Insensitive);
        payload.ShouldNotContain("phone", Case.Insensitive);
        payload.ShouldNotContain("name", Case.Insensitive);
    }
}
