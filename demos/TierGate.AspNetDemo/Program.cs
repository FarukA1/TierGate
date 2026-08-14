using Microsoft.AspNetCore.Http;
using TierGate.AspNetCore.RateLimiting;
using TierGate.AspNetDemo;
using TierGate.Core.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory is enough for this demo — see TierGate.Core's TableStorageRateLimitStore
// for a production, multi-instance-safe backend.
builder.Services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseTierGate(new TierGateOptions<string, DemoTier>
{
    Store = app.Services.GetRequiredService<IRateLimitStore>(),
    ExtractSubject = ctx => ctx.Request.Headers["X-Api-Key"].FirstOrDefault(),
    ResolveTierAsync = (apiKey, _) => Task.FromResult(DemoTiers.Resolve(apiKey)),
    GetLimits = tier => tier.Limits,
    GetStoreKey = apiKey => apiKey,
    ExcludedPaths = [new PathString("/swagger")],
});

app.MapControllers();

app.Run();
