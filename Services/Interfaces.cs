using System.Net.WebSockets;
using AIReceptionist.Api.Domain;

namespace AIReceptionist.Api.Services;

public interface ISttService
{
    Task<string> TranscribeAsync(byte[] audioChunk, string sessionId, CancellationToken ct = default);
}

public interface ITtsService
{
    Task<byte[]> SynthesizeAsync(string text, string voice, CancellationToken ct = default);
}

public interface IAiService
{
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}

public interface IRagService
{
    Task AddDocumentAsync(string title, string content);
    Task<List<string>> RetrieveAsync(string query, int topK = 3);
}

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default);
}

public interface IConversationStore
{
    ConversationState GetOrCreate(string callSid);
    void Remove(string callSid);
}

public interface IConversationManager
{
    Task HandleUtteranceAsync(string callSid, string utterance);
}

public interface IStreamWebSocketHandler
{
    Task HandleAsync(WebSocket ws);
    Task SendAudioToCallerAsync(string callSid, byte[] audio);
}
