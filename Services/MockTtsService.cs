using System.Text;

namespace AIReceptionist.Api.Services;

public class MockTtsService : ITtsService
{
    public Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default)
    {
        // Return a tiny WAV header + text bytes for testing. Not real audio.
        var payload = Encoding.UTF8.GetBytes(text);
        return Task.FromResult(payload);
    }
}
