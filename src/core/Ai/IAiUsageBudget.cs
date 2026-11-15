namespace Uic.Claims.Ai.Core.Ai;

/// <summary>
/// Cheap circuit breaker for cost. Not a billing system - just stops a runaway loop
/// from turning into a five-figure invoice overnight.
/// </summary>
public interface IAiUsageBudget
{
    ValueTask<bool> TryConsumeAsync(CancellationToken ct = default);
    int RemainingToday { get; }
}
