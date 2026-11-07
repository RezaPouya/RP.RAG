using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RP.Rag.Application.Conversations;
using RP.RAG.Application.Ask;
using System.Security.Claims;

namespace RP.RAG.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(
    IConversationMemoryService conversations,
    IAskService ask,
    ILogger<ConversationsController> logger) : ControllerBase
{

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var conversation = await conversations.CreateAsync(userId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { conversationId = conversation.ConversationId }, conversation);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> Get(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var conversation = await conversations.GetAsync(userId, conversationId, cancellationToken);
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("An upstream service timed out while processing a conversation message.");
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new { error = "A required service took too long to respond. Please retry the request." });
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
