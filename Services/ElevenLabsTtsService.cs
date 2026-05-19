using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace AIReceptionist.Api.Services;

public class ElevenLabsTtsService : ITtsService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppSettings _settings;

    public ElevenLabsTtsService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
    }

    public async Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient("elevenlabs");
        var apiKey = _settings.ElevenLabs?.ApiKey;
        if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        var voiceId = string.IsNullOrEmpty(voice) ? (_settings.ElevenLabs?.Voice ?? "alloy") : voice;
        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}/stream";
        var body = new { text = text };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        // prefer streaming low-latency
        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        var ms = new MemoryStream();
        await (await res.Content.ReadAsStreamAsync(ct)).CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
