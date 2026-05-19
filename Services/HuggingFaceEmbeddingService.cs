using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AIReceptionist.Api.Services;

public class HuggingFaceEmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<HuggingFaceEmbeddingService> _log;

    public HuggingFaceEmbeddingService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
    }

    public HuggingFaceEmbeddingService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts, ILogger<HuggingFaceEmbeddingService> log)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
        _log = log;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient("huggingface");
        var apiKey = _settings.HuggingFace?.ApiKey;
        var model = _settings.HuggingFace?.Model ?? "sentence-transformers/all-mpnet-base-v2";
        if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Hugging Face inference endpoint for embeddings
        var baseUrl = _settings.HuggingFace?.BaseUrl?.TrimEnd('/') ?? "https://api-inference.huggingface.co";
        var url = $"{baseUrl}/pipeline/feature-extraction/{model}";

        var body = Newtonsoft.Json.JsonConvert.SerializeObject(new { inputs = text });
        _log.LogDebug("HuggingFace embedding request: url={Url}, model={Model}, textLength={Len}", url, model, text?.Length ?? 0);

        try
        {
            var res = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);
            var txt = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var snippet = txt?.Length > 1000 ? txt.Substring(0, 1000) : txt;
                _log.LogWarning("HuggingFace embeddings request failed: {Status} {Reason}. Response snippet: {Snippet}", (int)res.StatusCode, res.ReasonPhrase, snippet);
                return Array.Empty<float>();
            }

            try
            {
                var j = JToken.Parse(txt);
                // HF returns nested arrays (tokens x dim) or a single array depending on model; average if nested
                if (j.Type == JTokenType.Array && j.First.Type == JTokenType.Array)
                {
                    var arrays = j.Children().Select(a => a.Select(v => (float)v.Value<double>()).ToArray()).ToList();
                    var dim = arrays[0].Length;
                    var avg = new float[dim];
                    foreach (var arr in arrays)
                    {
                        for (int i = 0; i < dim; i++) avg[i] += arr[i];
                    }
                    for (int i = 0; i < dim; i++) avg[i] /= arrays.Count;
                    _log.LogInformation("HuggingFace embeddings success: vector dim={Dim}", dim);
                    return avg;
                }

                // single flat array
                var arrFlat = j.Select(t => (float)t.Value<double>()).ToArray();
                _log.LogInformation("HuggingFace embeddings success: vector length={Len}", arrFlat.Length);
                return arrFlat;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse HuggingFace embeddings response: {Response}", txt);
                return Array.Empty<float>();
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "HuggingFace embeddings request exception");
            return Array.Empty<float>();
        }
    }
}
