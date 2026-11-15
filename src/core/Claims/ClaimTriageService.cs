using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Uic.Claims.Ai.Core.Ai;
using Uic.Claims.Ai.Core.Rules;

namespace Uic.Claims.Ai.Core.Claims;

public sealed class ClaimTriageService(
    IAiClient ai,
    IClaimRepository repository,
    IAiAuditSink audit,
    TriageRuleSet rules,
    ILogger<ClaimTriageService> logger)
{
    private const string TriagePromptKey = "claim.triage.v1";

    public async Task<TriageSuggestion> TriageAsync(long claimId, CancellationToken ct = default)
    {
        var claim = await repository.FindAsync(claimId, ct) ?? throw new ClaimNotFoundException(claimId);

        var fromRules = rules.Evaluate(claim);
        if (fromRules is not null)
        {
            logger.LogDebug("Triage for {ClaimId} resolved by rules ({Category})", claimId, fromRules.Category);
            await repository.SaveSuggestionAsync(claimId, fromRules, AiUsage.None, ct);
            return fromRules;
        }

        var (suggestion, usage) = await AskModelAsync(claim, ct);

        // Guardrail sits after the model on purpose: a confident answer does not
        // get to override policy limits.
        if (claim.IsAboveLimit)
        {
            suggestion = suggestion.WithFlag(ClaimFlags.AbovePolicyLimit);
        }

        await repository.SaveSuggestionAsync(claimId, suggestion, usage, ct);
        await audit.RecordAsync(new AiAuditEvent(
            "AI_TRIAGE", "CLAIM", claimId, "service:ai",
            usage.Model, TriagePromptKey, usage, claimId.ToString()), ct);

        if (suggestion.RequiresHumanReview)
        {
            await repository.FlagForReviewAsync(claimId, "service:ai", ct);
        }

        return suggestion;
    }

    private async Task<(TriageSuggestion Suggestion, AiUsage Usage)> AskModelAsync(
        ClaimContext claim, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await ai.CompleteAsync(new AiRequest(
                TriagePromptKey,
                TriagePayload.From(claim),
                Temperature: 0.1,
                MaxOutputTokens: 700,
                CorrelationId: claim.ClaimId.ToString()), ct);

            var parsed = TriageOutputParser.TryParse(response.Content, out var suggestion, out var error);
            if (!parsed)
            {
                // Log the reason, not the payload - the payload may contain claim amounts
                // that we do not want duplicated into log indices.
                logger.LogWarning("Unparseable triage output for {ClaimId}: {Reason}", claim.ClaimId, error);
                return (Fallback(ClaimFlags.AiInvalidOutput), response.Usage);
            }

            return (suggestion!, response.Usage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning(ex, "Provider call failed for {ClaimId} after {Elapsed}ms",
                claim.ClaimId, stopwatch.ElapsedMilliseconds);
            return (Fallback(ClaimFlags.AiUnavailable), AiUsage.None);
        }
    }

    private static TriageSuggestion Fallback(string flag) =>
        new("UNCLASSIFIED", 0, 100, [flag],
            "تعذّر إكمال التحليل الآلي، أُحيلت المطالبة إلى المراجعة اليدوية.")
        { RequiresHumanReview = true };
}
