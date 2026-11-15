using Uic.Claims.Ai.Core.Ai;

namespace Uic.Claims.Ai.Core.Claims;

public interface IClaimRepository
{
    Task<ClaimContext?> FindAsync(long claimId, CancellationToken ct = default);

    Task SaveSuggestionAsync(long claimId, TriageSuggestion suggestion, AiUsage usage, CancellationToken ct = default);

    /// <summary>Goes through the INS team's PL/SQL procedure, not a direct UPDATE.</summary>
    Task FlagForReviewAsync(long claimId, string actor, CancellationToken ct = default);
}

public sealed class ClaimNotFoundException(long claimId)
    : Exception($"Claim {claimId} was not found in app_ai.claim_context.");
