namespace Uic.Claims.Ai.Core.Ai;

public sealed record AiRequest(
    string PromptKey,
    string UserInput,
    double Temperature = 0.1,
    int MaxOutputTokens = 700,
    string? CorrelationId = null);

public sealed record AiResponse(string Content, AiUsage Usage);

public sealed record AiUsage(string Model, int InputTokens, int OutputTokens, long LatencyMs)
{
    public static readonly AiUsage None = new("rules-engine", 0, 0, 0);

    public decimal CostUsd(decimal inputPer1M, decimal outputPer1M) =>
        InputTokens / 1_000_000m * inputPer1M + OutputTokens / 1_000_000m * outputPer1M;
}

public interface IAiClient
{
    Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(AiRequest request, CancellationToken ct = default);
}

public interface IPromptCatalog
{
    string Get(string key);
}

public interface IAiAuditSink
{
    Task RecordAsync(AiAuditEvent entry, CancellationToken ct = default);
}

public sealed record AiAuditEvent(
    string Action,
    string Entity,
    long EntityId,
    string Actor,
    string Model,
    string PromptKey,
    AiUsage Usage,
    string? CorrelationId);

/// <summary>
/// Thrown when the feature flag is off or config is missing. Callers treat this the
/// same as any other provider failure: fall back to the manual path.
/// </summary>
public sealed class AiUnavailableException(string message) : Exception(message);

public sealed class AiBudgetExceededException(string message) : Exception(message);
