using Microsoft.AspNetCore.Mvc;
using Dapper;
using Uic.Claims.Ai.Api.Contracts;
using Uic.Claims.Ai.Core.Ai;
using Uic.Claims.Ai.Core.Claims;
using Uic.Claims.Ai.Infrastructure.Oracle;

namespace Uic.Claims.Ai.Api.Endpoints;

public static class ClaimTriageEndpoints
{
    public static IEndpointRouteBuilder MapClaimTriageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai/claims").WithTags("Claims AI").RequireRateLimiting("per-officer");

        group.MapPost("/{claimId:long}/triage", async (
            long claimId,
            ClaimTriageService service,
            CancellationToken ct) =>
        {
            var suggestion = await service.TriageAsync(claimId, ct);
            return TypedResults.Ok(suggestion);
        })
        .WithName("TriageClaim")
        .Produces<TriageSuggestion>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{claimId:long}/feedback", async Task<IResult> (
            long claimId,
            [FromBody] SuggestionFeedback feedback,
            IOracleConnectionFactory connections,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (feedback.Outcome is not ("accepted" or "overridden"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["outcome"] = ["must be 'accepted' or 'overridden'"]
                });
            }

            // Identity comes from the gateway header, not the request body. The body is
            // client-controlled and would let an officer attribute a decision to someone else.
            var officer = http.Request.Headers["X-Officer-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(officer))
            {
                return Results.Unauthorized();
            }

            const string sql = """
                insert into app_ai.claim_decisions
                    (claim_id, decision, final_category, comment_ar, decided_by)
                values (:claimId, :decision, :category, :comment, :officer)
                """;

            await using var connection = connections.Create();
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                claimId,
                decision = feedback.Outcome == "accepted" ? "ACCEPT" : "OVERRIDE",
                category = feedback.FinalCategory,
                comment = feedback.Comment,
                officer
            }, cancellationToken: ct));

            return Results.NoContent();
        })
        .WithName("RecordSuggestionFeedback")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        group.MapGet("/{claimId:long}/summary/stream", (
            long claimId,
            [FromServices] IAiClient ai,
            HttpContext http,
            CancellationToken ct) =>
        {
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-store";

            return Task.Run(async () =>
            {
                await foreach (var chunk in ai.StreamAsync(
                                   new AiRequest("claim.summary.v1", $"claimId={claimId}"), ct)
                               .WithCancellation(ct))
                {
                    await http.Response.WriteAsync($"data: {chunk}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }

                await http.Response.WriteAsync("data: [DONE]\n\n", ct);
            }, ct);
        })
        .WithName("StreamClaimSummary");

        return app;
    }
}
