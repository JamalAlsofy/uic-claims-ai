using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Uic.Claims.Ai.Core.Ai;
using Uic.Claims.Ai.Core.Claims;
using Uic.Claims.Ai.Core.Rules;
using Uic.Claims.Ai.Infrastructure.Ai;
using Uic.Claims.Ai.Infrastructure.Oracle;

namespace Uic.Claims.Ai.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClaimsAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<OracleOptions>()
            .Bind(configuration.GetSection(OracleOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IPromptCatalog, FilePromptCatalog>();
        services.AddSingleton<IAiUsageBudget, DailyCallBudget>();
        services.AddSingleton<IOracleConnectionFactory, OracleConnectionFactory>();
        services.AddSingleton(TriageRuleSet.Default);

        services.AddScoped<IClaimRepository, OracleClaimRepository>();
        services.AddScoped<IAiAuditSink, OracleAuditSink>();
        services.AddScoped<ClaimTriageService>();

        services.AddHttpClient<IAiClient, OpenAiCompatibleClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            http.DefaultRequestHeaders.Add("User-Agent", "uic-claims-ai/0.9");
        })
        .AddStandardResilienceHandler(builder =>
        {
            // Two retries is enough for a 429; more just deepens the queue.
            builder.Retry.MaxRetryAttempts = 2;
            builder.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
            builder.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        });

        return services;
    }
}
