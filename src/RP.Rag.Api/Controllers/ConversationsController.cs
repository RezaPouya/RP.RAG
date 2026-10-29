using Microsoft.AspNetCore.Mvc;
using RP.RAG.Application.Ask;

namespace RP.RAG.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(
    IConversationMemoryService conversations,
    IAskService ask,
    ILogger<ConversationsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var conversation = await conversations.CreateAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { conversationId = conversation.ConversationId }, conversation);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> Get(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(conversationId, cancellationToken);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> Delete(Guid conversationId, CancellationToken cancellationToken)
    {
        var deleted = await conversations.DeleteAsync(conversationId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{conversationId:guid}/messages")]
    public async Task<IActionResult> PostMessage(
        Guid conversationId,
        [FromBody] ConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Field 'message' is required." });

        try
        {
            var response = await ask.AskAsync(
                request.Message,
                request.TopK,
                conversationId,
                cancellationToken);
            return response.IsBlocked ? StatusCode(StatusCodes.Status403Forbidden, response) : Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Conversation was not found." });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "A required upstream service failed while processing a conversation message.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "A required upstream service is not available." });
        }
    }
}

public sealed class ConversationMessageRequest
{
    public string Message { get; init; } = "";
    public int? TopK { get; init; }
}
