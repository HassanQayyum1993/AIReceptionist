using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AIReceptionist.Api.Services;

// Simple Deepgram realtime WebSocket client that forwards audio and exposes final transcripts per session.
public class DeepgramSttService : ISttService, IDisposable
{
    private readonly AppSettings _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<DeepgramSttService> _log;
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    private class Session
    {
        public ClientWebSocket Ws = new ClientWebSocket();
        public Task ReceiveLoopTask = Task.CompletedTask;
        public readonly ConcurrentQueue<string> Transcripts = new();
        public TaskCompletionSource<string?> NextTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public DeepgramSttService(IHttpClientFactory httpFactory, IOptions<AppSettings> opts, ILogger<DeepgramSttService> log)
    {
        _httpFactory = httpFactory;
        _settings = opts.Value;
        _log = log;
    }

    public async Task<string> TranscribeAsync(byte[] audioChunk, string sessionId, CancellationToken ct = default)
    {
        var session = _sessions.GetOrAdd(sessionId, sid => CreateSessionAsync(sid));

        // Send audio as base64 in Deepgram append message
        var base64 = Convert.ToBase64String(audioChunk);
        var msg = new { type = "input_audio_buffer.append", audio = base64 };
        var bytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
        try
        {
            await session.Ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Failed to send audio chunk to Deepgram websocket for session {sessionId}", sessionId);
            throw;
        }

        // Wait briefly for a transcript to appear (final). Timeout after 1500ms.
        var tcs = session.NextTcs;
        using var cts = new CancellationTokenSource(1500);
        try
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed == tcs.Task)
            {
                var res = tcs.Task.Result;
                // prepare next TCS
                session.NextTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                return res ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Error waiting for Deepgram transcript for session {sessionId}", sessionId);
            throw;
        }
        // timeout without transcript
        return string.Empty;
    }

    private Session CreateSessionAsync(string sessionId)
    {
        var s = new Session();
        var key = _settings.Deepgram?.ApiKey ?? string.Empty;
        var url = _settings.Deepgram?.RealtimeUrl ?? "wss://api.deepgram.com/v1/listen";

        s.Ws.Options.SetRequestHeader("Authorization", $"Token {key}");
        // connect
        s.ReceiveLoopTask = Task.Run(async () =>
        {
            try
            {
                await s.Ws.ConnectAsync(new Uri(url), CancellationToken.None);
                var buffer = new byte[8192];
                while (s.Ws.State == WebSocketState.Open)
                {
                    var res = await s.Ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    var txt = Encoding.UTF8.GetString(buffer, 0, res.Count);
                    try
                    {
                        var j = JObject.Parse(txt);
                        // Deepgram sends transcript messages; pick final alternatives
                        var type = j.Value<string>("type");
                        if (type == "transcript")
                        {
                            var isFinal = j.SelectToken("is_final")?.Value<bool?>() ?? false;
                            var alt = j.SelectToken("channel.alternatives[0].transcript")?.Value<string>() ?? string.Empty;
                            if (!string.IsNullOrEmpty(alt) && isFinal)
                            {
                                s.Transcripts.Enqueue(alt);
                                s.NextTcs.TrySetResult(alt);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log?.LogDebug(ex, "Failed to parse Deepgram message for session {sessionId}: {Raw}", sessionId, txt);
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, "Deepgram receive loop terminated with error for session {sessionId}", sessionId);
            }
        });

        return s;
    }

    public void Dispose()
    {
        foreach (var kv in _sessions)
        {
            try
            {
                kv.Value.Ws?.Abort();
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "Failed to abort Deepgram websocket for session {sessionId}", kv.Key);
            }
        }
    }
}
