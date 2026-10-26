using System.Runtime.CompilerServices;
using System.Text.Json;
using Anthropic;
using Anthropic.Helpers;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace OnlineBankingAPI.Assistant.Models;

/// <summary>Claude through the official Anthropic C# SDK, streamed so text reaches the browser as it is written.</summary>
public class ClaudeAssistantModel(
    AnthropicClient client,
    IOptions<AssistantOptions> options,
    ILogger<ClaudeAssistantModel> logger) : IAssistantModel
{
    public string Mode => "claude";

    public async IAsyncEnumerable<ModelStreamItem> StreamAsync(ModelRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var settings = options.Value;
        var parameters = new MessageCreateParams
        {
            Model = settings.Model,
            MaxTokens = settings.MaxTokens,
            OutputConfig = new OutputConfig { Effort = ParseEffort(settings.Effort) },
            // The fixed prompt (and the tools before it) is cached; today's date comes after the breakpoint.
            System = new List<TextBlockParam>
            {
                new() { Text = request.SystemPrompt, CacheControl = new CacheControlEphemeral() },
                new() { Text = request.Context },
            },
            Tools = request.Tools.Select(ToTool).ToList(),
            Messages = request.History.Select(ToParam).ToList(),
        };

        var aggregator = new MessageContentAggregator();
        await foreach (var e in client.Messages.CreateStreaming(parameters, ct).CollectAsync(aggregator).WithCancellation(ct))
        {
            if (e.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
                yield return new TextDelta(text.Text);
        }

        var message = aggregator.Message();
        logger.LogInformation(
            "Claude {Model}: {Input} input, {CacheRead} cache-read, {Output} output tokens, stop {StopReason}",
            settings.Model, message.Usage.InputTokens, message.Usage.CacheReadInputTokens,
            message.Usage.OutputTokens, message.StopReason?.ToString());

        yield return new ModelReply(message.Content.Select(FromBlock).OfType<ChatPart>().ToList(),
            StopReasonName(message));
    }

    private static string StopReasonName(Message message) => message.StopReason?.Raw() ?? "end_turn";

    private static ToolUnion ToTool(ToolDefinition definition) =>
        new Tool
        {
            Name = definition.Name,
            Description = definition.Description,
            Strict = true,
            // Raw, so additionalProperties: false (which strict mode needs) goes through as written.
            InputSchema = InputSchema.FromRawUnchecked(
                definition.InputSchema.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())),
        };

    private static MessageParam ToParam(ChatMessage message)
    {
        List<ContentBlockParam> content = message.Parts.Select(ToBlockParam).ToList();
        return message.Role == ChatRole.User
            ? new UserMessageParam { Content = content }
            : new AssistantMessageParam { Content = content };
    }

    private static ContentBlockParam ToBlockParam(ChatPart part) => part switch
    {
        TextPart t => new TextBlockParam { Text = t.Text },
        ToolCallPart c => new ToolUseBlockParam
        {
            ID = c.Id,
            Name = c.Name,
            Input = c.Input.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
        },
        ToolResultPart r => new ToolResultBlockParam { ToolUseID = r.ToolCallId, Content = r.Json, IsError = r.IsError },
        OpaquePart { Value: ContentBlockParam block } => block,
        _ => throw new InvalidOperationException($"Claude can't take a {part.GetType().Name}."),
    };

    private static ChatPart? FromBlock(ContentBlock block)
    {
        if (block.TryPickText(out var text)) return new TextPart(text.Text);
        if (block.TryPickToolUse(out var toolUse))
            return new ToolCallPart(toolUse.ID, toolUse.Name, JsonSerializer.SerializeToElement(toolUse.Input));
        // Thinking blocks go back unchanged, signature included, on the next call.
        if (block.TryPickThinking(out var thinking))
            return new OpaquePart(new ContentBlockParam(new ThinkingBlockParam
            {
                Thinking = thinking.Thinking,
                Signature = thinking.Signature,
            }));
        if (block.TryPickRedactedThinking(out var redacted))
            return new OpaquePart(new ContentBlockParam(new RedactedThinkingBlockParam { Data = redacted.Data }));
        return null;
    }

    private static Effort ParseEffort(string value) => value.ToLowerInvariant() switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.Low,
    };
}
