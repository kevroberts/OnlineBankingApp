using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Assistant;
using OnlineBankingAPI.Assistant.Models;
using OnlineBankingAPI.Data;
using OnlineBankingAPI.Infrastructure;
using OnlineBankingAPI.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BankOptions>(builder.Configuration.GetSection(BankOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<BankDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("BankDb"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddBankServices();
builder.Services.AddBankAssistant(builder.Configuration);

builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName, null);
// Everything except POST /auth/login requires a session.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddBankRateLimiting();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
    ApiProblem.Result(context.HttpContext, StatusCodes.Status400BadRequest, ProblemCodes.BadRequest,
        string.Join(" ", context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .Select(kv => $"{kv.Key}: {kv.Value!.Errors[0].ErrorMessage}".Trim()))));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
{
    var status = context.ProblemDetails.Status ?? 500;
    context.ProblemDetails.Extensions.TryAdd("code", ProblemCodes.ForStatus(status));
    context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<ApiProblemExceptionHandler>();

builder.Services.AddOpenApi(o =>
{
    o.AddDocumentTransformer<SessionSecuritySchemeTransformer>();
    o.AddOperationTransformer<IdempotencyKeyOperationTransformer>();
});

var app = builder.Build();

// Development and Demo environments migrate and seed at startup; `dotnet run -- --reseed` starts over.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo")
    || app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>()
        .MigrateAndSeedAsync(reseed: args.Contains("--reseed"), CancellationToken.None);
}

if (app.Services.GetRequiredService<IAssistantModel>().Mode == "mock")
{
    app.Logger.LogWarning("The assistant is using demo answers. Set {Setting} (dotnet user-secrets) to use Claude.",
        AssistantOptions.ApiKeySetting);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapOpenApi().AllowAnonymous();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapControllers();

app.Run();

public partial class Program;
