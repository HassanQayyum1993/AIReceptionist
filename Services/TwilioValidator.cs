using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AIReceptionist.Api.Services;

public class TwilioValidator
{
    private readonly AppSettings _settings;
    private readonly ILogger<TwilioValidator> _log;

    public TwilioValidator(IOptions<AppSettings> opts, ILogger<TwilioValidator> log)
    {
        _settings = opts.Value;
        _log = log;
    }

    public bool Validate(HttpRequest request, string body = "")
    {
        var authToken = _settings.Twilio?.AuthToken;
        if (string.IsNullOrEmpty(authToken))
        {
            _log.LogInformation("Twilio auth token not configured; skipping signature validation.");
            return true; // skip if not configured
        }

        if (!request.Headers.TryGetValue("X-Twilio-Signature", out var sig))
        {
            _log.LogWarning("Missing X-Twilio-Signature header");
            return false;
        }

        // Prefer forwarded headers (when behind a tunnel) so the URL matches what Twilio signed
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        var url = string.Concat(scheme, "://", host, request.Path);
        _log.LogDebug("Validating Twilio signature for URL {Url}", url);

        var paramBuilder = new StringBuilder();
        if (request.HasFormContentType)
        {
            var dict = request.Form.ToDictionary(k => k.Key, v => v.Value.ToString());
            foreach (var kv in dict.OrderBy(k => k.Key)) paramBuilder.Append(kv.Key).Append(kv.Value);
            // For form posts, Twilio expects signing of url + sorted paramName+value (no extra body)
            var toSign = url + paramBuilder;
            var hash = ComputeHmacSha1(authToken, toSign);
            var ok = hash == sig;
            _log.LogInformation("Twilio form signature check: ok={Ok}, sigHeaderLength={SigLen}, paramsCount={Count}", ok, sig.ToString().Length, dict.Count);
            return ok;
        }

        // For non-form bodies (JSON/etc), sign url + raw body
        var toSignRaw = url + body;
        var hashRaw = ComputeHmacSha1(authToken, toSignRaw);
        var okRaw = hashRaw == sig;
        _log.LogInformation("Twilio raw signature check: ok={Ok}, sigHeaderLength={SigLen}, bodyLength={BodyLen}", okRaw, sig.ToString().Length, body?.Length ?? 0);
        return okRaw;
    }

    private static string ComputeHmacSha1(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA1(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }
}
