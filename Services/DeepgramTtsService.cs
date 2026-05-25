using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AIReceptionist.Api.Services;

public class DeepgramTtsService : ITtsService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<DeepgramTtsService> _log;

    public DeepgramTtsService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts, ILogger<DeepgramTtsService> log)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
        _log = log;
    }

    public async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
    {
        var apiKey = _settings.Deepgram?.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogWarning("Deepgram API key is not configured.");
            return Array.Empty<byte>();
        }

        var url = "https://api.deepgram.com/v1/speak";
        var client = _httpFactory.CreateClient();

        var requestBody = new
        {
            text = text,
            voice = string.IsNullOrWhiteSpace(voice) ? "aura-asteria-en" : voice
        };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Token {apiKey}");

        try
        {
            var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                _log.LogError("Deepgram TTS request failed: {Status} {Reason}. Response: {Response}", (int)res.StatusCode, res.ReasonPhrase, err);
                return Array.Empty<byte>();
            }
            return await res.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Exception during Deepgram TTS synthesis");
            return Array.Empty<byte>();
        }
    }
}
