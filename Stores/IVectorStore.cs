namespace AIReceptionist.Api.Stores;

public interface IVectorStore
{
    Task UpsertAsync(string id, float[] embedding, string text);
    Task<List<string>> QueryAsync(float[] queryEmb, int topK = 3);
}
