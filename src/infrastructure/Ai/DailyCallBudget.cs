using Microsoft.Extensions.Options;
using Uic.Claims.Ai.Core.Ai;

namespace Uic.Claims.Ai.Infrastructure.Ai;

/// <summary>
/// Process-local counter. Good enough for a single instance; if we scale out past two
/// replicas this needs to move to Redis, otherwise the real ceiling becomes N × budget.
/// </summary>
public sealed class DailyCallBudget(IOptions<AiOptions> options, TimeProvider clock) : IAiUsageBudget
{
    private readonly AiOptions _options = options.Value;
    // object, not System.Threading.Lock - the Lock type only exists from .NET 9
    // and this project still multi-targets net8.0.
    private readonly object _gate = new();
    private int _used;
    private DateOnly _windowStart = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public int RemainingToday
    {
        get
        {
            RollWindowIfNeeded();
            lock (_gate) return Math.Max(0, _options.DailyCallBudget - _used);
        }
    }

    public ValueTask<bool> TryConsumeAsync(CancellationToken ct = default)
    {
        RollWindowIfNeeded();
        lock (_gate)
        {
            if (_used >= _options.DailyCallBudget) return ValueTask.FromResult(false);
            _used++;
            return ValueTask.FromResult(true);
        }
    }

    private void RollWindowIfNeeded()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (today == _windowStart) return;

        lock (_gate)
        {
            if (today == _windowStart) return;
            _windowStart = today;
            _used = 0;
        }
    }
}
