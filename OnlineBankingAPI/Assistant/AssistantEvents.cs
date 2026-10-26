using System.Text.Json.Serialization;

namespace OnlineBankingAPI.Assistant;

/// <summary>One server-sent event of a reply: event name plus a JSON-serializable payload.</summary>
public abstract record AssistantEvent([property: JsonIgnore] string Name);

public record ConversationEvent(Guid ConversationId, string Mode) : AssistantEvent("conversation");

/// <summary>A tool is running, so the UI can say what it is looking up.</summary>
public record StatusEvent(string Tool) : AssistantEvent("status");

public record TextEvent(string Delta) : AssistantEvent("text");

public record DoneEvent(string StopReason) : AssistantEvent("done");

public record ErrorEvent(string Code, string Detail) : AssistantEvent("error");
