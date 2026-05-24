using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace AIReceptionist.Api.Services;

public class GroqEmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<GroqEmbeddingService> _log;
    private readonly IHostEnvironment _env;

    public GroqEmbeddingService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts, ILogger<GroqEmbeddingService> log, IHostEnvironment env)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
        _log = log;
        _env = env;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient("groq");
        var apiKey = _settings.Groq?.ApiKey ?? _settings.OpenAI?.ApiKey;
        if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var model = _settings.Groq?.Model ?? "text-embedding-3-small";
        var body = new { input = text, model };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var url = (_settings.Groq?.BaseUrl ?? _settings.OpenAI?.BaseUrl ?? "https://api.groq.com/openai/v1").TrimEnd('/') + "/embeddings";

        _log.LogDebug("Groq embedding request: url={Url}, model={Model}, textLength={Len}", url, model, text?.Length ?? 0);

        // Prefer using the configured HttpClient BaseAddress when available to avoid accidental double-paths
        HttpResponseMessage res;
        string txt;
        try
        {
            if (client.BaseAddress != null)
            {
                // Build request Uri from BaseAddress to ensure path segments are combined correctly
                var requestUri = new Uri(client.BaseAddress, "embeddings");
                res = await client.PostAsync(requestUri, new StringContent(json, Encoding.UTF8, "application/json"), ct);
            }
            else
            {
                // Fall back to absolute URL constructed from settings
                // Validate URL to avoid HttpClient invalid URI errors
                if (!Uri.TryCreate(url, UriKind.Absolute, out var _))
                {
                    _log.LogError("Invalid Groq embedding request URL: {Url}. Check configuration (Groq:BaseUrl or OpenAI:BaseUrl).", url);
                    throw new InvalidOperationException("Invalid Groq base URL configuration");
                }
                res = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
            }
            txt = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var snippet = txt?.Length > 1000 ? txt.Substring(0, 1000) : txt;
                _log.LogError("Groq embeddings request failed: {Status} {Reason}. Response snippet: {Snippet}", (int)res.StatusCode, res.ReasonPhrase, snippet);
                if (_env?.IsDevelopment() == true)
                {
                    _log.LogDebug("Full Groq response: {Response}", txt);
                }
                throw new HttpRequestException($"Groq embeddings request failed: {(int)res.StatusCode} {res.ReasonPhrase}. Response snippet: {snippet}");
            }

            try
            {
                var j = JObject.Parse(txt);
                var arr = j["data"]?[0]?["embedding"] as JArray;
                if (arr == null)
                {
                    _log.LogWarning("Groq embeddings returned no embedding. Response: {Response}", txt);
                    return Array.Empty<float>();
                }
                var result = arr.Select(t => (float)t.Value<double>()).ToArray();
                _log.LogInformation("Groq embeddings success: vector length={Len}", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse Groq embeddings response: {Response}", txt);
                throw new InvalidOperationException("Failed to parse Groq embeddings response", ex);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Groq embeddings request exception for url {Url}", url);
            throw;
        }
    }
}
