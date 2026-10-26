namespace OnlineBankingAPI.Contracts;

/// <summary>A question for the assistant. conversationId is null to start a new chat.</summary>
public record AssistantMessageRequest(Guid? ConversationId, string Text);
