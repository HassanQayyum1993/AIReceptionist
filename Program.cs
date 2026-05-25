using AIReceptionist.Api.Controllers;
using AIReceptionist.Api;
using AIReceptionist.Api.Services;
using AIReceptionist.Api.Stores;
using Microsoft.AspNetCore.Builder;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Config
// Bind AppSettings and make available via IOptions
builder.Services.Configure<AppSettings>(builder.Configuration);
var appSettings = builder.Configuration.Get<AIReceptionist.Api.AppSettings>() ?? new AIReceptionist.Api.AppSettings();

// DI - stores and services
builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
// Embeddings: OpenAI by default; switch to Hugging Face if configured
// Configure named HttpClients with base addresses from configuration when available
builder.Services.AddHttpClient("openai", client =>
{
    var url = appSettings.OpenAI?.BaseUrl;
    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var u)) client.BaseAddress = u;
});

builder.Services.AddHttpClient("huggingface", client =>
{
    var url = appSettings.HuggingFace?.BaseUrl;
    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var u)) client.BaseAddress = u;
})
// Prefer IPv4 resolution for environments with problematic IPv6/DNS setups
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    ConnectCallback = async (context, cancellationToken) =>
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;
        IPAddress[] addrs;
        try
        {
            addrs = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch
        {
            addrs = Array.Empty<IPAddress>();
        }

        // prefer IPv4
        var addr = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs.FirstOrDefault();
        if (addr == null) throw new HttpRequestException($"Could not resolve host {host}");

        var socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(addr, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            try { socket.Dispose(); } catch { }
            throw;
        }
    }
});
builder.Services.AddHttpClient("groq", client =>
{
    var url = appSettings.Groq?.BaseUrl;
    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var u)) client.BaseAddress = u;
});

// Register embedding service implementation based on configuration

// Choose embedding service based on configuration
//if (!string.IsNullOrEmpty(appSettings.Groq?.ApiKey))
//{
//    builder.Services.AddSingleton<IEmbeddingService, GroqEmbeddingService>();
//}
//else
if (!string.IsNullOrEmpty(appSettings.HuggingFace?.ApiKey))
{
    builder.Services.AddSingleton<IEmbeddingService, HuggingFaceEmbeddingService>();
}
else
{
    // default to Groq implementation but it will log if not configured
    builder.Services.AddSingleton<IEmbeddingService, GroqEmbeddingService>();
}

var vectorType = appSettings.VectorStore?.Type?.ToLowerInvariant() ?? "memory";
if (vectorType == "pinecone")
{
    builder.Services.AddSingleton<IVectorStore, PineconeVectorStore>();
}
else
{
    builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();
}

// HTTP clients for external services
builder.Services.AddHttpClient("deepgram");
builder.Services.AddHttpClient("elevenlabs");

// Use Deepgram STT and ElevenLabs TTS when keys provided, otherwise fall back to mocks
var dgKey = appSettings.Deepgram?.ApiKey;
if (!string.IsNullOrEmpty(dgKey)) builder.Services.AddSingleton<ISttService, DeepgramSttService>();
else builder.Services.AddSingleton<ISttService, MockSttService>();

// Use Deepgram as TTS if configured, otherwise fallback to ElevenLabs or Mock
var dgTtsKey = appSettings.Deepgram?.ApiKey;
if (!string.IsNullOrEmpty(dgTtsKey))
{
    builder.Services.AddSingleton<ITtsService, DeepgramTtsService>();
}
else if (!string.IsNullOrEmpty(appSettings.ElevenLabs?.ApiKey))
{
    builder.Services.AddSingleton<ITtsService, ElevenLabsTtsService>();
}
else
{
    builder.Services.AddSingleton<ITtsService, MockTtsService>();
}
// Choose generation service based on configuration (Groq preferred, fallback to OpenAI implementation)
if (!string.IsNullOrEmpty(appSettings.Groq?.ApiKey))
{
    builder.Services.AddSingleton<IAiService, GroqGenerationService>();
}
else
{
    builder.Services.AddSingleton<IAiService, OpenAiService>();
}
builder.Services.AddSingleton<IRagService, RagService>();
builder.Services.AddSingleton<IConversationManager, ConversationManager>();
builder.Services.AddSingleton<IStreamWebSocketHandler, StreamWebSocketHandler>();
builder.Services.AddSingleton<TwilioValidator>();



var app = builder.Build();

// Validate important settings at startup and log helpful messages
try
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var streamingUrl = appSettings.Streaming?.Url;
    if (string.IsNullOrEmpty(streamingUrl))
    {
        startupLogger.LogWarning("Streaming:Url is not configured. Twilio media stream will use the default placeholder in the webhook response.");
    }
    else if (!Uri.TryCreate(streamingUrl, UriKind.Absolute, out var _))
    {
        startupLogger.LogError("Configured Streaming:Url is not a valid absolute URI: {Url}", streamingUrl);
    }
}
catch { }

// Enable Swagger/OpenAPI for local development and quick inspection
app.UseSwagger();
app.UseSwaggerUI();

// Global exception handling
app.UseMiddleware<AIReceptionist.Api.Middleware.ExceptionHandlingMiddleware>();

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.UseWebSockets();

// WebSocket endpoint for Twilio Media Streams
app.Map("/api/call/stream", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    WebSocket? ws = null;
    try
    {
        ws = await context.WebSockets.AcceptWebSocketAsync();
        var handler = app.Services.GetRequiredService<IStreamWebSocketHandler>();
        await handler.HandleAsync(ws!);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "WebSocket stream handling failed");
        try
        {
            if (ws != null && ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "server error", CancellationToken.None);
            }
        }
        catch (Exception closeEx)
        {
            logger.LogWarning(closeEx, "Failed to close WebSocket after error");
        }
    }
});

app.Run();
