namespace OnlineBankingAPI.Assistant;

/// <summary>Settings under Bank:Assistant. The API key is read from ANTHROPIC_API_KEY, never from appsettings.</summary>
public class AssistantOptions
{
    public const string Section = "Bank:Assistant";
    public const string ApiKeySetting = "ANTHROPIC_API_KEY";

    /// <summary>Claude or Mock. Claude falls back to Mock when no API key is set.</summary>
    public string Provider { get; set; } = "Claude";

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>low, medium, high, xhigh or max. Short chat answers do well at low.</summary>
    public string Effort { get; set; } = "low";

    public int MaxTokens { get; set; } = 4096;

    /// <summary>Model calls per question before the turn gives up.</summary>
    public int MaxToolRounds { get; set; } = 6;

    public int ConversationIdleMinutes { get; set; } = 30;
}
