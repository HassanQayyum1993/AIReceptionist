using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;

namespace AIReceptionist.Api.Stores;

public class PineconeVectorStore : IVectorStore
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly PineconeSettings _settings;

    public PineconeVectorStore(IHttpClientFactory httpFactory, IOptions<AppSettings> opts)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value.Pinecone ?? new PineconeSettings();
    }

    private string BaseUrl => BuildBaseUrl();

    private string BuildBaseUrl()
    {
        // Pinecone uses: https://{index}.svc.{environment}.pinecone.io
        var env = _settings.Environment ?? string.Empty;
        var idx = _settings.IndexName ?? string.Empty;
        return $"https://{idx}.svc.{env}.pinecone.io".TrimEnd('/');
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, BaseUrl + path);
        if (!string.IsNullOrEmpty(_settings.ApiKey)) req.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", _settings.ApiKey);
        if (content != null) req.Content = content;
        return req;
    }

    public async Task UpsertAsync(string id, float[] embedding, string text)
    {
        var client = _httpFactory.CreateClient();
        var body = new
        {
            vectors = new[] {
                new {
                    id = id,
                    values = embedding,
                    metadata = new { text = text }
                }
            }
        };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var req = CreateRequest(HttpMethod.Post, "/vectors/upsert", new StringContent(json, Encoding.UTF8, "application/json"));
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }

    public async Task<List<string>> QueryAsync(float[] queryEmb, int topK = 3)
    {
        Console.WriteLine("Querying Pinecone with embedding: [" + string.Join(", ", queryEmb.Take(5)) + "...]");
        var client = _httpFactory.CreateClient();
        var body = new
        {
            vector = queryEmb,
            topK = topK,
            includeMetadata = true
        };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var req = CreateRequest(HttpMethod.Post, "/query", new StringContent(json, Encoding.UTF8, "application/json"));
        var res = await client.SendAsync(req);
        var txt = await res.Content.ReadAsStringAsync();
        try
        {
            Console.WriteLine("Pinecone query response: " + txt);
            var j = JObject.Parse(txt);
            var matches = j["matches"] as JArray;
            if (matches == null) return new List<string>();
            return matches.Select(m => m["metadata"]?["text"]?.Value<string>() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}
