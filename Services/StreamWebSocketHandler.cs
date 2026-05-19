using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AIReceptionist.Api.Services;

public class StreamWebSocketHandler : IStreamWebSocketHandler
{
    private readonly ILogger<StreamWebSocketHandler> _log;
    private readonly ISttService _stt;
    private readonly IConversationManager _conv;
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    public StreamWebSocketHandler(ILogger<StreamWebSocketHandler> log, ISttService stt, IConversationManager conv)
    {
        _log = log;
        _stt = stt;
        _conv = conv;
    }

    public async Task HandleAsync(WebSocket ws)
    {
        var buffer = new byte[8192];
        WebSocketReceiveResult res;
        var sb = new StringBuilder();
        while (ws.State == WebSocketState.Open)
        {
            sb.Clear();
            res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (res.MessageType == WebSocketMessageType.Close) break;
            sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
            var text = sb.ToString();
            try
            {
                var json = JObject.Parse(text);
                var evt = json.Value<string>("event");
                if (evt == "connected" || evt == "start")
                {
                    var callSid = json.SelectToken("start.callSid")?.Value<string>() ?? json.Value<string>("callSid");
                    if (!string.IsNullOrEmpty(callSid)) _sockets[callSid] = ws;
                    _log.LogInformation("WS start for {callSid}", callSid);
                }
                else if (evt == "media")
                {
                    var payload = json.SelectToken("media.payload")?.Value<string>();
                    if (!string.IsNullOrEmpty(payload))
                    {
                        var bytes = Convert.FromBase64String(payload);
                        // for this demo, STT expects UTF8 text encoded in bytes (MockSttService)
                        var callSid = json.SelectToken("start.callSid")?.Value<string>() ?? json.Value<string>("callSid") ?? "unknown";
                        try
                        {
                            var transcript = await _stt.TranscribeAsync(bytes, callSid);
                            if (!string.IsNullOrWhiteSpace(transcript))
                            {
                                await _conv.HandleUtteranceAsync(callSid, transcript);
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Error processing media payload for callSid={callSid}", callSid);
                        }
                    }
                }
                else if (evt == "stop")
                {
                    var callSid = json.SelectToken("stop.callSid")?.Value<string>() ?? json.Value<string>("callSid");
                    if (!string.IsNullOrEmpty(callSid)) _sockets.TryRemove(callSid, out _);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to parse WS message");
            }
        }
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None); } catch { }
    }

    public async Task SendAudioToCallerAsync(string callSid, byte[] audio)
    {
        if (_sockets.TryGetValue(callSid, out var ws) && ws.State == WebSocketState.Open)
        {
            var payload = Convert.ToBase64String(audio);
            var obj = new
            {
                @event = "media",
                media = new { payload = payload }
            };
            var bytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send audio to caller {callSid}", callSid);
            }
        }
    }
}
