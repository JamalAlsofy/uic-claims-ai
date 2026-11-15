using System.ComponentModel.DataAnnotations;

namespace Uic.Claims.Ai.Infrastructure.Oracle;

public sealed class OracleOptions
{
    public const string SectionName = "Oracle";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = "";

    /// <summary>Schema that owns the read view and the AI tables.</summary>
    public string AiSchema { get; init; } = "APP_AI";

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}
