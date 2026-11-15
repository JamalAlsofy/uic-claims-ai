using Shouldly;
using Uic.Claims.Ai.Core.Claims;
using Xunit;

namespace Uic.Claims.Ai.UnitTests.Claims;

public class TriageOutputParserTests
{
    private const string Valid = """
        {"category":"MOTOR","confidence":0.88,"riskScore":22,"flags":[],"summaryAr":"حادث سير بسيط.","requiresHumanReview":false}
        """;

    [Fact]
    public void Accepts_a_well_formed_response()
    {
        TriageOutputParser.TryParse(Valid, out var suggestion, out var error).ShouldBeTrue(error);

        suggestion!.Category.ShouldBe("MOTOR");
        suggestion.RequiresHumanReview.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("Sure! Here is the JSON:", "malformed")]
    [InlineData("""{"category":"MEDICAL"}""", "missing")]
    [InlineData("""{"category":"SPACESHIP","confidence":0.9,"summaryAr":"x"}""", "unknown category")]
    [InlineData("""{"category":"MOTOR","confidence":1.7,"summaryAr":"x"}""", "out of range")]
    public void Rejects_anything_that_is_not_exactly_right(string payload, string expectedReasonHint)
    {
        TriageOutputParser.TryParse(payload, out var suggestion, out var error).ShouldBeFalse();

        suggestion.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain(expectedReasonHint, Case.Insensitive);
    }

    [Fact]
    public void Low_confidence_forces_review_even_if_the_model_says_otherwise()
    {
        const string payload = """
            {"category":"CARGO","confidence":0.31,"riskScore":40,"flags":[],"summaryAr":"غير واضح","requiresHumanReview":false}
            """;

        TriageOutputParser.TryParse(payload, out var suggestion, out _).ShouldBeTrue();

        suggestion!.RequiresHumanReview.ShouldBeTrue();
    }

    [Fact]
    public void Missing_requiresHumanReview_flag_defaults_to_true()
    {
        const string payload = """
            {"category":"FIRE","confidence":0.9,"riskScore":30,"summaryAr":"حريق"}
            """;

        TriageOutputParser.TryParse(payload, out var suggestion, out _).ShouldBeTrue();

        suggestion!.RequiresHumanReview.ShouldBeTrue();
    }
}
