using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Assistant.Models;

namespace OnlineBankingAPI.Assistant;

/// <summary>A chat in progress. The history stays on the server, so the browser can't alter tool results.</summary>
public class AssistantConversation(Guid id, Guid customerId)
{
    public Guid Id { get; } = id;
    public Guid CustomerId { get; } = customerId;
    public List<ChatMessage> History { get; } = [];

    /// <summary>One question at a time per conversation.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

/// <summary>
/// Conversations in memory, grouped by session. A session's chats expire after ConversationIdleMinutes without use
/// and are dropped at sign-out. They don't survive an API restart.
/// </summary>
public class AssistantConversationStore(IMemoryCache cache, IOptions<AssistantOptions> options)
{
    public AssistantConversation Start(Guid sessionId, Guid customerId)
    {
        var conversation = new AssistantConversation(Guid.NewGuid(), customerId);
        ForSession(sessionId)[conversation.Id] = conversation;
        return conversation;
    }

    public AssistantConversation? Find(Guid sessionId, Guid customerId, Guid conversationId) =>
        cache.TryGetValue(Key(sessionId), out ConcurrentDictionary<Guid, AssistantConversation>? chats)
        && chats!.TryGetValue(conversationId, out var conversation) && conversation.CustomerId == customerId
            ? conversation
            : null;

    public bool Remove(Guid sessionId, Guid customerId, Guid conversationId) =>
        Find(sessionId, customerId, conversationId) is not null
        && cache.Get<ConcurrentDictionary<Guid, AssistantConversation>>(Key(sessionId))!.TryRemove(conversationId, out _);

    public void ForgetSession(Guid sessionId) => cache.Remove(Key(sessionId));

    private ConcurrentDictionary<Guid, AssistantConversation> ForSession(Guid sessionId) =>
        cache.GetOrCreate(Key(sessionId), entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(options.Value.ConversationIdleMinutes);
            return new ConcurrentDictionary<Guid, AssistantConversation>();
        })!;

    private static string Key(Guid sessionId) => $"assistant:{sessionId}";
}
