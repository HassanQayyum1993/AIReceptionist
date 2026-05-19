using System.Text;
using Microsoft.Extensions.Logging;

namespace AIReceptionist.Api.Services;

public class MockSttService : ISttService
{
    private readonly ILogger<MockSttService> _log;

    public MockSttService(ILogger<MockSttService> log)
    {
        _log = log;
    }

    public Task<string> TranscribeAsync(byte[] audioChunk, string sessionId, CancellationToken ct = default)
    {
        // Very naive mock: treat bytes as UTF8 text for local testing
        try
        {
            var s = Encoding.UTF8.GetString(audioChunk);
            if (string.IsNullOrWhiteSpace(s)) return Task.FromResult(string.Empty);
            _log.LogInformation("[MockStt] Transcribed: {text}", s);
            return Task.FromResult(s);
        }
        catch
        {
            return Task.FromResult(string.Empty);
        }
    }
}
