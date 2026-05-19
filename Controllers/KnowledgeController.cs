using Microsoft.AspNetCore.Mvc;
using AIReceptionist.Api.Services;
using AIReceptionist.Api.Models;
using AIReceptionist.Api.Models.Dtos;

namespace AIReceptionist.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeController : ControllerBase
{
    private readonly IRagService _rag;
    private readonly ILogger<KnowledgeController> _log;

    public KnowledgeController(IRagService rag, ILogger<KnowledgeController> log)
    {
        _rag = rag;
        _log = log;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] KnowledgeUploadDto dto)
    {
        try
        {
            var title = dto.Title ?? "doc";
            var len = dto.Content?.Length ?? 0;
            _log.LogInformation("Uploading knowledge doc: Title={Title}, Length={Len}", title, len);
            await _rag.AddDocumentAsync(title, dto.Content ?? "");
            _log.LogInformation("Upload complete: Title={Title}", title);
            return Ok(new { status = "uploaded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to upload knowledge doc: Title={Title}", dto.Title);
            return Problem("Failed to upload knowledge");
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int topK = 3)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { error = "query parameter 'q' is required" });
        try
        {
            _log.LogInformation("Knowledge search: q={Query}, topK={TopK}", q, topK);
            var results = await _rag.RetrieveAsync(q, topK);
            _log.LogInformation("Knowledge search returned {Count} results for q={Query}", results?.Count ?? 0, q);
            return Ok(new { query = q, results });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Knowledge search failed for q={Query}", q);
            return Problem("Failed to perform knowledge search");
        }
    }
}
