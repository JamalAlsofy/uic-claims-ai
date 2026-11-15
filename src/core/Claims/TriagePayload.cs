using System.Text.Json;

namespace Uic.Claims.Ai.Core.Claims;

/// <summary>
/// The exact shape sent to the provider. Written explicitly rather than serialising the
/// record so that adding a field to ClaimContext can never silently start leaking it.
/// </summary>
internal static class TriagePayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string From(ClaimContext claim) => JsonSerializer.Serialize(new
    {
        claimNo = claim.ClaimNo,
        branch = claim.BranchId,
        type = claim.ClaimType,
        amount = claim.ClaimedAmount,
        policyLimit = claim.SumInsured,
        aboveLimit = claim.IsAboveLimit,
        ageDays = claim.AgeDays,
        previousClaims12m = claim.ClaimsLast12Months
    }, Options);
}
