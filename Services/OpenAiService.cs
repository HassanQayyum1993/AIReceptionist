using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace AIReceptionist.Api.Services;

public class OpenAiService : IAiService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OpenAiService> _log;
    private readonly AppSettings _settings;

    public OpenAiService(IHttpClientFactory httpFactory, ILogger<OpenAiService> log, Microsoft.Extensions.Options.IOptions<AppSettings> opts)
    {
        _httpFactory = httpFactory;
        _log = log;
        _settings = opts.Value;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient("openai");
        var apiKey = _settings.Groq?.ApiKey ?? _settings.OpenAI?.ApiKey;
        if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var model = _settings.Groq?.Model ?? "llama3-70b-8192";
        var baseUrl = _settings.Groq?.BaseUrl ?? _settings.OpenAI?.BaseUrl ?? "https://api.groq.com/openai/v1";
        var url = baseUrl.TrimEnd('/') + "/chat/completions";

        var body = new
        {
            model = model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.2,
            max_tokens = 300
        };

        var json = JsonConvert.SerializeObject(body);

        var res = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
        var txt = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            _log.LogWarning("Generation failed: {Status} {Reason}. Response: {Response}", (int)res.StatusCode, res.ReasonPhrase, txt);
            return txt;
        }

        try
        {
            dynamic d = JsonConvert.DeserializeObject(txt)!;
            return d.choices[0].message.content.ToString().Trim();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to parse generation response. Raw: {Response}", txt);
            return txt;
        }
    }
}
