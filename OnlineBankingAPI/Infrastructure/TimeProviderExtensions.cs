namespace OnlineBankingAPI.Infrastructure;

public static class TimeProviderExtensions
{
    /// <summary>UTC now truncated to microseconds, the precision PostgreSQL stores, so responses match later reads.</summary>
    public static DateTimeOffset UtcNow(this TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.UtcTicks % 10));
    }
}
