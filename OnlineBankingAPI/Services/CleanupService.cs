using OnlineBankingAPI.Repositories;

namespace OnlineBankingAPI.Services;

/// <summary>Hourly: deletes expired idempotency records, expired quotes and ended sessions.</summary>
public class CleanupService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<CleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Cleanup failed; will retry next hour");
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var transfers = scope.ServiceProvider.GetRequiredService<ITransferRepository>();
        var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var now = clock.GetUtcNow();

        var records = await transfers.DeleteExpiredIdempotencyRecordsAsync(now, ct);
        var quotes = await transfers.DeleteExpiredQuotesAsync(now, ct);
        var sessions = await sessionStore.DeleteEndedAsync(now, ct);
        logger.LogInformation("Cleanup removed {Records} idempotency records, {Quotes} quotes, {Sessions} sessions",
            records, quotes, sessions);
    }
}
