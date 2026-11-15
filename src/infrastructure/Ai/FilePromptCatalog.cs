using System.Collections.Concurrent;
using System.Reflection;
using Uic.Claims.Ai.Core.Ai;

namespace Uic.Claims.Ai.Infrastructure.Ai;

/// <summary>
/// Prompts live as embedded .md files so they can be diffed and reviewed like code.
/// Missing keys fail fast at startup rather than mid-request.
/// </summary>
public sealed class FilePromptCatalog : IPromptCatalog
{
    private const string Suffix = ".md";
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Assembly _assembly = typeof(FilePromptCatalog).Assembly;

    public string Get(string key)
    {
        var prompt = _cache.GetOrAdd(key, Load);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException($"Prompt '{key}' is not embedded in the assembly.");
        }

        return prompt;
    }

    private string Load(string key)
    {
        var resourceName = _assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"{key}{Suffix}", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null) return "";

        using var stream = _assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
