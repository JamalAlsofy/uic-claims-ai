namespace Uic.Claims.Ai.Core.Claims;

/// <summary>
/// Everything the triage logic is allowed to know about a claim.
/// Reads from app_ai.claim_context; intentionally carries no insured name,
/// national id or contact details so nothing identifying can reach a model provider.
/// </summary>
public sealed record ClaimContext(
    long ClaimId,
    string ClaimNo,
    string BranchId,
    string ClaimType,
    string Status,
    decimal ClaimedAmount,
    decimal SumInsured,
    int AboveLimit,
    int AgeDays,
    int ClaimsLast12Months)
{
    public bool IsAboveLimit => AboveLimit == 1;

    public bool IsClosed => Status is "PAID" or "CLOSED";
}
