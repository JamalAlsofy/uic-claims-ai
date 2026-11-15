using System.Text.Json;

namespace Uic.Claims.Ai.Core.Claims;

/// <summary>
/// Strict reader for the model's JSON. The schema check in the prompt is a request,
/// not a guarantee, so we validate again here and reject anything that merely looks close.
/// </summary>
public static class TriageOutputParser
{
    private static readonly string[] KnownCategories =
    [
        "MEDICAL", "MOTOR", "CARGO", "FIRE", "ENGINEERING",
        "WORK_INJURY", "TRAVEL", "MONEY", "FIDELITY", "OTHER"
    ];

    public static bool TryParse(string json, out TriageSuggestion? suggestion, out string? error)
    {
        suggestion = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "empty response";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("category", out var categoryElement) ||
                !root.TryGetProperty("confidence", out var confidenceElement) ||
                !root.TryGetProperty("summaryAr", out var summaryElement))
            {
                error = "missing required property";
                return false;
            }

            var category = categoryElement.GetString()?.Trim().ToUpperInvariant() ?? "";
            if (!KnownCategories.Contains(category))
            {
                error = $"unknown category '{category}'";
                return false;
            }

            var confidence = confidenceElement.GetDouble();
            if (confidence is < 0 or > 1)
            {
                error = "confidence out of range";
                return false;
            }

            var riskScore = root.TryGetProperty("riskScore", out var riskElement) ? riskElement.GetDouble() : 50;
            riskScore = Math.Clamp(riskScore, 0, 100);

            var flags = root.TryGetProperty("flags", out var flagsElement) && flagsElement.ValueKind == JsonValueKind.Array
                ? flagsElement.EnumerateArray().Select(f => f.GetString() ?? "").Where(f => f.Length > 0).ToArray()
                : [];

            var wantsReview = !root.TryGetProperty("requiresHumanReview", out var reviewElement) ||
                              reviewElement.ValueKind != JsonValueKind.False;

            suggestion = new TriageSuggestion(
                category,
                confidence,
                riskScore,
                flags,
                summaryElement.GetString() ?? "")
            {
                // Below 0.6 the model is guessing more than it knows.
                RequiresHumanReview = wantsReview || confidence < 0.6
            };

            return true;
        }
        catch (JsonException ex)
        {
            error = $"malformed json: {ex.Message}";
            return false;
        }
    }
}
