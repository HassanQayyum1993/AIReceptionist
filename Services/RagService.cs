using System.Text;
using Microsoft.Extensions.Logging;
using AIReceptionist.Api.Stores;

namespace AIReceptionist.Api.Services;

public class RagService : IRagService
{
    private readonly IVectorStore _store;
    private readonly IAiService _ai;
    private readonly IEmbeddingService _embeddings;
    private readonly ILogger<RagService> _log;

    public RagService(IVectorStore store, IAiService ai, IEmbeddingService embeddings, ILogger<RagService> log)
    {
        _store = store;
        _ai = ai;
        _embeddings = embeddings;
        _log = log;
    }

    public async Task AddDocumentAsync(string title, string content)
    {
        try
        {
            // Chunk the document into smaller pieces to improve retrieval
            var chunks = ChunkText(content, 800); // ~800 chars per chunk
            var idx = 0;
            foreach (var chunk in chunks)
            {
                var emb = await _embeddings.CreateEmbeddingAsync(chunk);
                var id = Guid.NewGuid().ToString();
                var metadataText = $"{title} [part:{idx}]\n{chunk}";
                await _store.UpsertAsync(id, emb, metadataText);
                idx++;
            }
            _log.LogInformation("Upserted doc {title} into {count} chunks", title, chunks.Count);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to add document to RAG store: {Title}", title);
            throw;
        }
    }

    public async Task<List<string>> RetrieveAsync(string query, int topK = 3)
    {
        try
        {
            var qemb = await _embeddings.CreateEmbeddingAsync(query);
            var res = await _store.QueryAsync(qemb, topK);
            return res;
        }
        catch (Exception ex)
        {
            // Log and rethrow so callers (controllers) can return proper error responses
            _log.LogError(ex, "Failed to retrieve RAG results for query: {Query}", query);
            throw;
        }
    }
    
    private static List<string> ChunkText(string text, int size)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text)) return chunks;
        var pos = 0;
        while (pos < text.Length)
        {
            var len = Math.Min(size, text.Length - pos);
            // try to break on newline or space for better chunk boundaries
            if (pos + len < text.Length)
            {
                var nextBreak = text.LastIndexOfAny(new[] { '\n', ' ' }, pos + len - 1, len);
                if (nextBreak > pos) len = nextBreak - pos + 1;
            }
            chunks.Add(text.Substring(pos, len).Trim());
            pos += len;
        }
        return chunks;
    }

}
