using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Uic.Claims.Ai.Api.Endpoints;
using Uic.Claims.Ai.Api.Workers;
using Uic.Claims.Ai.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddClaimsAi(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The triage endpoint is cheap for us and expensive for the provider, so the limit is
// per officer rather than global: one officer clicking twice shouldn't starve the queue.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-officer", context =>
    {
        var officer = context.Request.Headers["X-Officer-Id"].FirstOrDefault() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(officer, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

builder.Services.AddHostedService<TriageBackfillWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapClaimTriageEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "0.9.2" })).ExcludeFromDescription();

app.Run();
