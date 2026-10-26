using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OnlineBankingAPI.Assistant;
using OnlineBankingAPI.Assistant.Models;
using OnlineBankingAPI.Assistant.Tools;

namespace OnlineBankingAPI.Tests;

/// <summary>Checks what ClaudeAssistantModel sends and how it reads a streamed reply, against a stub server.</summary>
public class ClaudeAssistantModelTests
{
    [Fact]
    public async Task Sends_strict_tools_cached_prompt_and_low_effort_then_reads_a_tool_call()
    {
        var stub = new StubHandler(Sse(
            ("message_start", """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":12,"output_tokens":1,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}"""),
            ("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig-123"}}"""),
            ("content_block_stop", """{"type":"content_block_stop","index":0}"""),
            ("content_block_start", """{"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"Let me check."}}"""),
            ("content_block_stop", """{"type":"content_block_stop","index":1}"""),
            ("content_block_start", """{"type":"content_block_start","index":2,"content_block":{"type":"tool_use","id":"toolu_1","name":"list_accounts","input":{}}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":2,"delta":{"type":"input_json_delta","partial_json":"{}"}}"""),
            ("content_block_stop", """{"type":"content_block_stop","index":2}"""),
            ("message_delta", """{"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":30}}"""),
            ("message_stop", """{"type":"message_stop"}""")));
        var model = Model(stub);
        var tool = new ListAccountsTool(null!);
        var request = new ModelRequest("System prompt.", "Today is Monday.",
            [new ToolDefinition(tool.Name, tool.Description, tool.InputSchema)],
            [new ChatMessage(ChatRole.User, [new TextPart("What is my balance?")])]);

        var items = new List<ModelStreamItem>();
        await foreach (var item in model.StreamAsync(request, CancellationToken.None)) items.Add(item);

        var body = JsonDocument.Parse(Assert.Single(stub.Bodies)).RootElement;
        Assert.Equal("claude-opus-5-5", body.GetProperty("model").GetString());
        Assert.True(body.GetProperty("stream").GetBoolean());
        Assert.Equal("low", body.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.False(body.TryGetProperty("thinking", out _));
        var system = body.GetProperty("system");
        Assert.Equal("ephemeral", system[0].GetProperty("cache_control").GetProperty("type").GetString());
        Assert.Equal("Today is Monday.", system[1].GetProperty("text").GetString());
        var toolJson = Assert.Single(body.GetProperty("tools").EnumerateArray());
        Assert.True(toolJson.GetProperty("strict").GetBoolean());
        Assert.False(toolJson.GetProperty("input_schema").GetProperty("additionalProperties").GetBoolean());

        Assert.Equal("Let me check.", Assert.IsType<TextDelta>(items[0]).Text);
        var reply = Assert.IsType<ModelReply>(items[^1]);
        Assert.Equal("tool_use", reply.StopReason);
        Assert.IsType<OpaquePart>(reply.Parts[0]);
        Assert.Equal("Let me check.", Assert.IsType<TextPart>(reply.Parts[1]).Text);
        var call = Assert.IsType<ToolCallPart>(reply.Parts[2]);
        Assert.Equal(("toolu_1", "list_accounts"), (call.Id, call.Name));
    }

    [Fact]
    public async Task Sends_thinking_tool_calls_and_results_back_in_order()
    {
        var stub = new StubHandler(Sse(
            ("message_start", """{"type":"message_start","message":{"id":"msg_2","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":12,"output_tokens":1}}}"""),
            ("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"You have $10.00."}}"""),
            ("content_block_stop", """{"type":"content_block_stop","index":0}"""),
            ("message_delta", """{"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":5}}"""),
            ("message_stop", """{"type":"message_stop"}""")));
        var thinking = new Anthropic.Models.Messages.ContentBlockParam(
            new Anthropic.Models.Messages.ThinkingBlockParam { Thinking = "", Signature = "sig-123" });
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, [new TextPart("What is my balance?")]),
            new(ChatRole.Assistant, [new OpaquePart(thinking), new ToolCallPart("toolu_1", "list_accounts", JsonSerializer.SerializeToElement(new { }))]),
            new(ChatRole.User, [new ToolResultPart("toolu_1", """{"accounts":[]}""", false)]),
        };

        var items = new List<ModelStreamItem>();
        await foreach (var item in Model(stub).StreamAsync(new ModelRequest("S", "C", [], history), CancellationToken.None))
            items.Add(item);

        var messages = JsonDocument.Parse(Assert.Single(stub.Bodies)).RootElement.GetProperty("messages");
        Assert.Equal(["user", "assistant", "user"], messages.EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        var assistant = messages[1].GetProperty("content");
        Assert.Equal("thinking", assistant[0].GetProperty("type").GetString());
        Assert.Equal("sig-123", assistant[0].GetProperty("signature").GetString());
        Assert.Equal("tool_use", assistant[1].GetProperty("type").GetString());
        var result = messages[2].GetProperty("content")[0];
        Assert.Equal("tool_result", result.GetProperty("type").GetString());
        Assert.Equal("toolu_1", result.GetProperty("tool_use_id").GetString());

        Assert.Equal("end_turn", Assert.IsType<ModelReply>(items[^1]).StopReason);
    }

    private static ClaudeAssistantModel Model(StubHandler stub) => new(
        new AnthropicClient { ApiKey = "test-key", BaseUrl = "https://stub.test", HttpClient = new HttpClient(stub), MaxRetries = 0 },
        Options.Create(new AssistantOptions()),
        NullLogger<ClaudeAssistantModel>.Instance);

    private static string Sse(params (string Event, string Data)[] events) =>
        string.Concat(events.Select(e => $"event: {e.Event}\ndata: {e.Data}\n\n"));

    private class StubHandler(string sse) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
            };
        }
    }
}
