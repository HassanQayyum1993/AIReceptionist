using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AIReceptionist.Api.Services;

public class ElevenLabsTtsService : ITtsService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<ElevenLabsTtsService> _log;

    public ElevenLabsTtsService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts, ILogger<ElevenLabsTtsService> log)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
        _log = log;
    }

    public async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient("elevenlabs");
        var apiKey = _settings.ElevenLabs?.ApiKey;
        if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        var voiceId = string.IsNullOrEmpty(voice) ? (_settings.ElevenLabs?.Voice ?? "EOVAuWqgSZN2Oel78Psj") : voice;
        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}/stream";
        var body = new { text = text };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        // prefer streaming low-latency
        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var txt = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _log?.LogError("ElevenLabs TTS request failed: {Status} {Reason}. Response: {Response}", (int)res.StatusCode, res.ReasonPhrase, txt);
            throw new HttpRequestException($"ElevenLabs TTS request failed: {(int)res.StatusCode} {res.ReasonPhrase}. Response: {txt}");
        }
        var ms = new MemoryStream();
        await (await res.Content.ReadAsStreamAsync(ct)).CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
