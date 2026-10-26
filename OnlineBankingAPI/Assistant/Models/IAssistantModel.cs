using System.Text.Json;

namespace OnlineBankingAPI.Assistant.Models;

/// <summary>
/// One model call: the conversation so far in, streamed text and the finished reply out.
/// The tool loop lives in AssistantService, so Claude, the mock and the test fake share it.
/// </summary>
public interface IAssistantModel
{
    /// <summary>"claude" or "mock", sent to the browser so it can label demo answers.</summary>
    string Mode { get; }

    IAsyncEnumerable<ModelStreamItem> StreamAsync(ModelRequest request, CancellationToken ct);
}

public record ModelRequest(
    string SystemPrompt,
    string Context,
    IReadOnlyList<ToolDefinition> Tools,
    IReadOnlyList<ChatMessage> History);

public record ToolDefinition(string Name, string Description, JsonElement InputSchema);

public enum ChatRole { User, Assistant }

/// <summary>A model-neutral transcript entry. Claude's own blocks (thinking) ride along as <see cref="OpaquePart"/>.</summary>
public record ChatMessage(ChatRole Role, IReadOnlyList<ChatPart> Parts);

public abstract record ChatPart;

public record TextPart(string Text) : ChatPart;

public record ToolCallPart(string Id, string Name, JsonElement Input) : ChatPart;

public record ToolResultPart(string ToolCallId, string Json, bool IsError) : ChatPart;

/// <summary>A provider-specific block that must be sent back unchanged, such as a signed thinking block.</summary>
public record OpaquePart(object Value) : ChatPart;

public abstract record ModelStreamItem;

public record TextDelta(string Text) : ModelStreamItem;

/// <summary>The whole assistant message, sent last. StopReason is end_turn, tool_use, max_tokens or refusal.</summary>
public record ModelReply(IReadOnlyList<ChatPart> Parts, string StopReason) : ModelStreamItem;
