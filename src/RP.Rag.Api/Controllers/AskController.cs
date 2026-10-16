using Microsoft.AspNetCore.Mvc;
using RP.RAG.Application.Ask;

namespace RP.RAG.Api.Controllers;

[ApiController]
[Route("api/ask")]
public sealed class AskController(
    IAskService ask,
    ILogger<AskController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Field 'question' is required." });

        try
        {
            return Ok(await ask.AskAsync(request.Question, request.TopK, ct));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The language model request failed.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The language model is not available." });
        }
    }
}

public sealed class AskRequest
{
    public string Question { get; init; } = "";
    public int? TopK { get; init; }
}
