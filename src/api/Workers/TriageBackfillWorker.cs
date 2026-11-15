using Dapper;
using Microsoft.Extensions.Options;
using Uic.Claims.Ai.Core.Claims;
using Uic.Claims.Ai.Infrastructure.Oracle;

namespace Uic.Claims.Ai.Api.Workers;

/// <summary>
/// Picks up claims that arrived while nobody was looking and pre-computes a suggestion
/// so the officer finds it ready instead of waiting. Deliberately sequential: the
/// bottleneck here is the provider, and parallel loops just move the queue.
/// </summary>
public sealed class TriageBackfillWorker(
    IServiceScopeFactory scopes,
    IOracleConnectionFactory connections,
    IOptions<OracleOptions> oracleOptions,
    ILogger<TriageBackfillWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting up and let the DB come up behind us.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Backfill pass failed; will retry next tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        const string sql = """
            select claim_id
              from app_ai.claim_context v
             where v.claim_status in ('OPEN', 'UNDER_REVIEW')
               and v.age_days <= 3
               and not exists (select 1
                                 from app_ai.ai_suggestions s
                                where s.claim_id = v.claim_id
                                  and s.created_at > systimestamp - interval '24' hour)
             order by v.claim_date desc
             fetch first 20 rows only
            """;

        List<long> pending;
        await using (var connection = connections.Create())
        {
            var command = new CommandDefinition(sql, commandTimeout: oracleOptions.Value.CommandTimeoutSeconds, cancellationToken: ct);
            pending = (await connection.QueryAsync<long>(command)).AsList();
        }

        if (pending.Count == 0) return;

        logger.LogInformation("Backfill pass picked up {Count} claim(s)", pending.Count);

        using var scope = scopes.CreateScope();
        var triage = scope.ServiceProvider.GetRequiredService<ClaimTriageService>();

        foreach (var claimId in pending.Take(BatchSize))
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await triage.TriageAsync(claimId, ct);
            }
            catch (ClaimNotFoundException ex)
            {
                logger.LogWarning(ex, "Claim {ClaimId} vanished between query and triage", claimId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Backfill triage failed for claim {ClaimId}", claimId);
            }
        }
    }
}
