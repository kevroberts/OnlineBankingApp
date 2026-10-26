namespace OnlineBankingAPI.Infrastructure;

public class BankOptions
{
    public const string Section = "Bank";

    /// <summary>Time zone that date filters are interpreted in. Open question: assumed US Eastern.</summary>
    public string TimeZone { get; set; } = "America/New_York";

    public TimeSpan QuoteLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan SessionAbsoluteLifetime { get; set; } = TimeSpan.FromHours(8);
    public int MaxFailedLogins { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    public int LoginsPerMinutePerIp { get; set; } = 10;
    public int RequestsPerMinutePerSession { get; set; } = 60;
    public TimeSpan IdempotencyRetention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How long a duplicate POST /transfers waits for the first one before 409 request_in_progress.</summary>
    public TimeSpan IdempotencyWait { get; set; } = TimeSpan.FromSeconds(5);

    public TimeZoneInfo GetTimeZone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var tz) ? tz : TimeZoneInfo.Utc;
}
