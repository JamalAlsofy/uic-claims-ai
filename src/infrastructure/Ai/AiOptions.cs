using System.ComponentModel.DataAnnotations;

namespace Uic.Claims.Ai.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; init; } = true;

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";

    public string ApiKey { get; init; } = "";

    [Required]
    public string Model { get; init; } = "gpt-4.1-mini";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 20;

    [Range(0, 1)]
    public double Temperature { get; init; } = 0.1;

    /// <summary>Hard stop for runaway loops. One provider call per claim, so this is generous.</summary>
    [Range(1, 100_000)]
    public int DailyCallBudget { get; init; } = 500;

    public decimal InputPricePer1M { get; init; } = 0.40m;
    public decimal OutputPricePer1M { get; init; } = 1.60m;
}
