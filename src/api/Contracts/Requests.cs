namespace Uic.Claims.Ai.Api.Contracts;

/// <summary>
/// What the officer sends back after reading a suggestion. Deliberately tiny: we record
/// agreement or the corrected category, and nothing else. The actual claim decision
/// stays in the core system.
/// </summary>
public sealed record SuggestionFeedback(
    string Outcome,          // "accepted" | "overridden"
    string? FinalCategory,
    string? Comment);
