using Uic.Claims.Ai.Core.Claims;

namespace Uic.Claims.Ai.Core.Rules;

/// <summary>
/// A deterministic rule. Rules run before any model call, in ascending <see cref="Order"/>,
/// first match wins. Keeping them as objects (rather than one if/else chain) means each
/// one gets its own test and its own mutation coverage.
/// </summary>
public interface ITriageRule
{
    string Name { get; }
    int Order { get; }
    bool Matches(ClaimContext claim);
    TriageSuggestion Apply(ClaimContext claim);
}
