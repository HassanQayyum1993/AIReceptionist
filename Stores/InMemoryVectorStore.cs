using System.Collections.Concurrent;

namespace AIReceptionist.Api.Stores;

public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, (float[] Emb, string Text)> _store = new();

    public Task UpsertAsync(string id, float[] embedding, string text)
    {
        _store[id] = (embedding, text);
        return Task.CompletedTask;
    }

    public Task<List<string>> QueryAsync(float[] queryEmb, int topK)
    {
        // naive cosine similarity
        var list = _store.Select(kvp => new { Text = kvp.Value.Text, Score = Cosine(queryEmb, kvp.Value.Emb) });
        var top = list.OrderByDescending(x => x.Score).Take(topK).Select(x => x.Text).ToList();
        return Task.FromResult(top);
    }

    private static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0; double na = 0; double nb = 0;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        if (na == 0 || nb == 0) return 0;
        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }
}
