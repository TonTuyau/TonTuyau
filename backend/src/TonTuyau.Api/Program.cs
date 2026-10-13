using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using TonTuyau.Api.Cli;
using TonTuyau.Api.Errors;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure;

// `tontuyau sync …` / `tontuyau index …`: one command, then exit (see CliRunner for the list).
if (CliRunner.IsCommand(args))
{
    return await CliHost.RunAsync(args);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddTonTuyauConfiguration();
// EF Core logs every SQL command it runs, and as an error the query it expects to fail at the very first start (no
// migration history yet). A real database error reaches the error middleware's log anyway.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
builder.Services.AddTonTuyau();
builder.Services.AddSingleton<SyncRunner>();
builder.Services.AddSingleton<Func<SyncMode, CancellationToken, Task<SyncRun>>>(sp =>
    (mode, ct) => sp.GetRequiredService<SyncRunner>().RunAsync(mode, SyncTrigger.Scheduler, ct));
builder.Services.AddHostedService<SyncScheduler>();
builder.Services.AddHostedService<IndexAutoRebuild>();
builder.Services.AddTonTuyauApi();
builder.Services.AddTonTuyauAuth();
builder.Services.AddRunLogs();
// After the runs' log writer: hosted services stop in reverse order, so the run going on ends before its log does.
builder.Services.AddHostedService<SyncRunnerLifetime>();
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

// Broken configuration stops the process here, before anything listens.
app.Services.ValidateTonTuyauOptions();

// PostgreSQL's schema up to date, and the admin account at the first start: before anything listens too.
await app.Services.InitializeAccountsAsync();

// Every answer says which version served it (handy after an upgrade; the same as `tontuyau --version`).
app.Use(AppVersion.AddHeader);

// Exceptions → ProblemDetails, and bodiless errors (unknown route, wrong method) too.
app.UseMiddleware<ErrorMiddleware>();
app.UseStatusCodePages(context => Problems.WriteAsync(
    context.HttpContext,
    Problems.ForStatus(context.HttpContext.Response.StatusCode)));

// Who is logged in (the session cookie), whether that's enough for the admin endpoints, then the login's rate limit.
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// The OpenAPI document (/openapi/v1.json) is what the front generates its types from; the Scalar UI to try the
// API (/scalar) is for development only (R2-25).
app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapControllers();

// Liveness: the process answers. Readiness: Elasticsearch is usable and TonTuyau's indices exist.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions { Predicate = c => c.Tags.Contains(SearchStoreServiceCollectionExtensions.ReadyTag) });

await app.RunAsync();
return 0;

/// <summary>Entry point, visible to <c>WebApplicationFactory</c> in tests.</summary>
public partial class Program;
