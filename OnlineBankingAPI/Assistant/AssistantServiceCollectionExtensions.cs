using Anthropic;
using OnlineBankingAPI.Assistant.Models;
using OnlineBankingAPI.Assistant.Tools;

namespace OnlineBankingAPI.Assistant;

public static class AssistantServiceCollectionExtensions
{
    /// <summary>
    /// The assistant, its two read-only tools and the model: Claude when an ANTHROPIC_API_KEY is set and
    /// Bank:Assistant:Provider is Claude, otherwise the mock.
    /// </summary>
    public static IServiceCollection AddBankAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AssistantOptions>(configuration.GetSection(AssistantOptions.Section));
        services.AddMemoryCache();
        services.AddSingleton<AssistantConversationStore>();
        services.AddScoped<IAssistantTool, ListAccountsTool>();
        services.AddScoped<IAssistantTool, SearchTransactionsTool>();
        services.AddScoped<AssistantService>();

        var provider = configuration.GetSection(AssistantOptions.Section).Get<AssistantOptions>()?.Provider ?? "Claude";
        var apiKey = configuration[AssistantOptions.ApiKeySetting];
        if (provider.Equals("Claude", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton(new AnthropicClient { ApiKey = apiKey });
            services.AddSingleton<IAssistantModel, ClaudeAssistantModel>();
        }
        else
        {
            services.AddSingleton<IAssistantModel, MockAssistantModel>();
        }
        return services;
    }
}
