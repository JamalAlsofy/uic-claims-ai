using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Uic.Claims.Ai.Core.Ai;
using Uic.Claims.Ai.Core.Claims;

namespace Uic.Claims.Ai.Infrastructure.Oracle;

/// <summary>
/// Read side is the app_ai.claim_context view. Write side is the app_ai schema only.
/// Claim state changes go through pr_flag_for_review, which the core team owns.
/// </summary>
public sealed class OracleClaimRepository(
    IOracleConnectionFactory connections,
    IOptions<OracleOptions> options,
    ILogger<OracleClaimRepository> logger) : IClaimRepository
{
    private readonly int _timeout = options.Value.CommandTimeoutSeconds;

    public async Task<ClaimContext?> FindAsync(long claimId, CancellationToken ct = default)
    {
        // Quoted aliases keep Oracle from upper-casing them, so Dapper's default
        // name matching (no underscore sniffing) works without a custom type map.
        const string sql = """
            select claim_id         as "ClaimId",
                   claim_no         as "ClaimNo",
                   branch_id        as "BranchId",
                   claim_type       as "ClaimType",
                   claim_status     as "Status",
                   claimed_amount   as "ClaimedAmount",
                   sum_insured      as "SumInsured",
                   above_limit_flag as "AboveLimit",
                   age_days         as "AgeDays",
                   claims_12m       as "ClaimsLast12Months"
              from app_ai.claim_context
             where claim_id = :claimId
            """;

        await using var connection = connections.Create();
        return await connection.QuerySingleOrDefaultAsync<ClaimContext>(
            new CommandDefinition(sql, new { claimId }, commandTimeout: _timeout, cancellationToken: ct));
    }

    public async Task SaveSuggestionAsync(
        long claimId,
        TriageSuggestion suggestion,
        AiUsage usage,
        CancellationToken ct = default)
    {
        // Bind variables throughout - Oracle would otherwise hard-parse every call and
        // fill the shared pool with single-use cursors.
        const string sql = """
            insert into app_ai.ai_suggestions
                (claim_id, category, confidence, risk_score, flags_json, summary_ar,
                 requires_review, model_name, input_tokens, output_tokens, latency_ms)
            values
                (:claimId, :category, :confidence, :riskScore, :flags, :summary,
                 :requiresReview, :model, :tokensIn, :tokensOut, :latencyMs)
            """;

        var parameters = new
        {
            claimId,
            category = suggestion.Category,
            confidence = suggestion.Confidence,
            riskScore = suggestion.RiskScore,
            flags = System.Text.Json.JsonSerializer.Serialize(suggestion.Flags),
            summary = Truncate(suggestion.SummaryAr, 2000),
            requiresReview = suggestion.RequiresHumanReview ? "Y" : "N",
            model = usage.Model,
            tokensIn = usage.InputTokens,
            tokensOut = usage.OutputTokens,
            latencyMs = usage.LatencyMs
        };

        await using var connection = connections.Create();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, commandTimeout: _timeout, cancellationToken: ct));
    }

    public async Task FlagForReviewAsync(long claimId, string actor, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_claim_id", claimId, DbType.Int64);
        parameters.Add("p_actor", actor, DbType.String);

        await using var connection = connections.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "app_ai.pr_flag_for_review",
            parameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: _timeout,
            cancellationToken: ct));

        logger.LogDebug("Flagged claim {ClaimId} for review by {Actor}", claimId, actor);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
