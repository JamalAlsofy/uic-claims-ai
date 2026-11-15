using Uic.Claims.Ai.Core.Claims;

namespace Uic.Claims.Ai.UnitTests.Claims;

/// <summary>Test data builder. Defaults describe an ordinary, unremarkable claim.</summary>
internal sealed class ClaimBuilder
{
    private long _id = 1001;
    private decimal _amount = 420_000m;
    private decimal _sumInsured = 1_000_000m;
    private int _ageDays = 18;
    private int _claims12m = 1;
    private string _status = "OPEN";

    public ClaimBuilder WithId(long id) { _id = id; return this; }
    public ClaimBuilder WithAmount(decimal amount) { _amount = amount; return this; }
    public ClaimBuilder WithSumInsured(decimal sumInsured) { _sumInsured = sumInsured; return this; }
    public ClaimBuilder WithAge(int days) { _ageDays = days; return this; }
    public ClaimBuilder WithHistory(int claims) { _claims12m = claims; return this; }
    public ClaimBuilder WithStatus(string status) { _status = status; return this; }

    public ClaimContext Build() => new(
        _id, $"CLM-{_id}", "BR01", "MEDICAL", _status,
        _amount, _sumInsured,
        _amount > _sumInsured ? 1 : 0,
        _ageDays, _claims12m);
}
