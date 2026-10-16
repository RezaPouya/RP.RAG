using Microsoft.AspNetCore.Mvc;
using RP.RAG.Application.Search;

namespace RP.RAG.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(
    ILexicalSearch lexical,
    ISemanticSearch semantic,
    IHybridSearch hybrid,
    ILogger<SearchController> logger) : ControllerBase
{
    [HttpGet("lexical")]
    public async Task<IActionResult> Lexical([FromQuery] string q, [FromQuery] int? topK, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query 'q' is required." });

        logger.LogInformation("Lexical search: '{Query}' (topK={TopK})", q, topK ?? 10);

        return Ok(await lexical.SearchAsync(q, topK ?? 10, onlyActive: true, ct));
    }

    [HttpGet("semantic")]
    public async Task<IActionResult> Semantic([FromQuery] string q, [FromQuery] int? topK, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query 'q' is required." });

        logger.LogInformation("Semantic search: '{Query}' (topK={TopK})", q, topK ?? 10);

        return Ok(await semantic.SearchAsync(q, topK ?? 10, onlyActive: true, ct));
    }

    [HttpGet("hybrid")]
    public async Task<IActionResult> Hybrid([FromQuery] string q, [FromQuery] int? topK, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query 'q' is required." });

        logger.LogInformation("Hybrid search: '{Query}' (topK={TopK})", q, topK ?? 10);

        return Ok(await hybrid.SearchAsync(q, topK ?? 10, onlyActive: true, ct));
    }
}
