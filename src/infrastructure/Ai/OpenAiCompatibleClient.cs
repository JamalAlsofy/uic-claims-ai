using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Uic.Claims.Ai.Core.Ai;

namespace Uic.Claims.Ai.Infrastructure.Ai;

/// <summary>
/// Speaks the OpenAI chat-completions dialect, which is also what Azure OpenAI (compat mode),
/// Ollama /v1 and vLLM expose. Swapping provider is a BaseUrl + Model change in config.
/// </summary>
public sealed class OpenAiCompatibleClient(
    HttpClient http,
    IOptions<AiOptions> options,
    IPromptCatalog prompts,
    IAiUsageBudget budget,
    ILogger<OpenAiCompatibleClient> logger) : IAiClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiOptions _options = options.Value;

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        EnsureConfigured();

        if (!await budget.TryConsumeAsync(ct))
        {
            throw new AiBudgetExceededException(
                $"Daily AI call budget of {_options.DailyCallBudget} is exhausted.");
        }

        var started = Stopwatch.GetTimestamp();

        var payload = new
        {
            model = _options.Model,
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = prompts.Get(request.PromptKey) },
                new { role = "user", content = request.UserInput }
            }
        };

        using var response = await http.PostAsJsonAsync("chat/completions", payload, Json, ct);

        if (!response.IsSuccessStatusCode)
        {
            // Body is logged at debug in case a provider echoes the API key back in errors.
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Provider returned {StatusCode} for {PromptKey}", (int)response.StatusCode, request.PromptKey);
            logger.LogDebug("Provider error body: {Body}", body.Length > 500 ? body[..500] : body);
            throw new AiUnavailableException($"Provider responded {(int)response.StatusCode}.");
        }

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletion>(Json, ct)
                         ?? throw new AiUnavailableException("Provider returned an empty body.");

        var content = completion.Choices.FirstOrDefault()?.Message.Content ?? "";
        var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var usage = new AiUsage(
            completion.Model ?? _options.Model,
            completion.Usage?.PromptTokens ?? 0,
            completion.Usage?.CompletionTokens ?? 0,
            elapsed);

        logger.LogInformation(
            "AI call {PromptKey} model={Model} tokens={In}/{Out} in {Elapsed}ms cost≈${Cost:F4}",
            request.PromptKey, usage.Model, usage.InputTokens, usage.OutputTokens,
            elapsed, usage.CostUsd(_options.InputPricePer1M, _options.OutputPricePer1M));

        return new AiResponse(content, usage);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        EnsureConfigured();

        var payload = new
        {
            model = _options.Model,
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            stream = true,
            messages = new object[]
            {
                new { role = "system", content = prompts.Get(request.PromptKey) },
                new { role = "user", content = request.UserInput }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload, options: Json)
        };

        using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var data = line[5..].Trim();
            if (data == "[DONE]") yield break;

            string? delta = null;
            try
            {
                using var chunk = JsonDocument.Parse(data);
                var choice = chunk.RootElement.GetProperty("choices")[0];
                if (choice.TryGetProperty("delta", out var d) && d.TryGetProperty("content", out var c))
                {
                    delta = c.GetString();
                }
            }
            catch (JsonException)
            {
                // Providers occasionally send keep-alive fragments mid-stream. Ignore them.
            }

            if (!string.IsNullOrEmpty(delta)) yield return delta;
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled) throw new AiUnavailableException("AI features are disabled by configuration.");
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) throw new AiUnavailableException("No provider API key is configured.");
    }

    private sealed record ChatCompletion(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] ChatChoice[] Choices,
        [property: JsonPropertyName("usage")] ChatUsage? Usage);

    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage Message);

    private sealed record ChatMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record ChatUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
