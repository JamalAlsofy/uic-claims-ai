using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Uic.Claims.Ai.Core.Ai;

namespace Uic.Claims.Ai.Infrastructure.Oracle;

/// <summary>
/// Audit writes must never take the request down with them. If Oracle refuses the insert
/// we log loudly and continue - losing one audit row is bad, failing the officer's screen
/// because of it is worse.
/// </summary>
public sealed class OracleAuditSink(
    IOracleConnectionFactory connections,
    IOptions<OracleOptions> options,
    ILogger<OracleAuditSink> logger) : IAiAuditSink
{
    private readonly int _timeout = options.Value.CommandTimeoutSeconds;

    public async Task RecordAsync(AiAuditEvent entry, CancellationToken ct = default)
    {
        const string sql = """
            insert into app_ai.audit_trail
                (action, entity, entity_id, actor, model_name, payload)
            values
                (:action, :entity, :entityId, :actor, :model, :payload)
            """;

        var payload = JsonSerializer.Serialize(new
        {
            entry.PromptKey,
            entry.Usage.InputTokens,
            entry.Usage.OutputTokens,
            entry.Usage.LatencyMs,
            entry.CorrelationId
        });

        try
        {
            await using var connection = connections.Create();
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                action = entry.Action,
                entity = entry.Entity,
                entityId = entry.EntityId,
                actor = entry.Actor,
                model = entry.Model,
                payload
            }, commandTimeout: _timeout, cancellationToken: ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Audit insert failed for {Action}/{Entity}/{EntityId}", entry.Action, entry.Entity, entry.EntityId);
        }
    }
}
