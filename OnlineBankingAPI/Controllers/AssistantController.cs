using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OnlineBankingAPI.Assistant;
using OnlineBankingAPI.Contracts;
using OnlineBankingAPI.Infrastructure;

namespace OnlineBankingAPI.Controllers;

[ApiController]
[Route("api/v1/assistant")]
public class AssistantController(AssistantService assistant) : ControllerBase
{
    private static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Asks the read-only assistant a question about the signed-in customer's own accounts.
    /// The reply streams as server-sent events: conversation, then status and text, then done or error.
    /// </summary>
    [EndpointName("askAssistant")]
    [HttpPost("messages")]
    [Consumes("application/json")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/event-stream")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task Ask(AssistantMessageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ApiProblemException(StatusCodes.Status400BadRequest, ProblemCodes.BadRequest, "text is required.");

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var e in assistant.AskAsync(User.SessionId(), User.CustomerId(), request.ConversationId,
                           request.Text, ct))
        {
            var data = JsonSerializer.Serialize(e, e.GetType(), EventJson);
            await Response.WriteAsync($"event: {e.Name}\ndata: {data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    /// <summary>Ends a chat so the next question starts a new one.</summary>
    [EndpointName("endAssistantConversation")]
    [HttpDelete("conversations/{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult EndConversation(Guid conversationId) =>
        assistant.EndConversation(User.SessionId(), User.CustomerId(), conversationId)
            ? NoContent()
            : throw ApiProblemException.NotFound("The conversation was not found.");
}
