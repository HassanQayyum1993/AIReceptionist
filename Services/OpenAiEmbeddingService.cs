using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AIReceptionist.Api.Services;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<OpenAiEmbeddingService> _log;

    public OpenAiEmbeddingService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts, ILogger<OpenAiEmbeddingService> log)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
        _log = log;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient("openai");
        var apiKey = _settings.Groq?.ApiKey ?? _settings.OpenAI?.ApiKey;
        if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var model = _settings.Groq?.Model ?? "text-embedding-3-small";
        var body = new { input = text, model };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var url = (_settings.Groq?.BaseUrl ?? _settings.OpenAI?.BaseUrl ?? "https://api.groq.com/openai/v1").TrimEnd('/') + "/embeddings";

        _log.LogDebug("OpenAI embedding request: url={Url}, model={Model}, textLength={Len}", url, model, text?.Length ?? 0);

        try
        {
            var res = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
            var txt = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var snippet = txt?.Length > 1000 ? txt.Substring(0, 1000) : txt;
                _log.LogWarning("OpenAI embeddings request failed: {Status} {Reason}. Response snippet: {Snippet}", (int)res.StatusCode, res.ReasonPhrase, snippet);
                return Array.Empty<float>();
            }

            try
            {
                var j = JObject.Parse(txt);
                var arr = j["data"]?[0]?["embedding"] as JArray;
                if (arr == null)
                {
                    _log.LogWarning("OpenAI embeddings returned no embedding. Response: {Response}", txt);
                    return Array.Empty<float>();
                }
                var result = arr.Select(t => (float)t.Value<double>()).ToArray();
                _log.LogInformation("OpenAI embeddings success: vector length={Len}", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse OpenAI embeddings response: {Response}", txt);
                return Array.Empty<float>();
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OpenAI embeddings request exception");
            return Array.Empty<float>();
        }
    }
}
