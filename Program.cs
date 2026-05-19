using AIReceptionist.Api.Controllers;
using AIReceptionist.Api;
using AIReceptionist.Api.Services;
using AIReceptionist.Api.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Config
builder.Services.Configure<AppSettings>(builder.Configuration);

// DI - stores and services
builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
// Embeddings: OpenAI by default; switch to Hugging Face if configured
builder.Services.AddHttpClient("openai");
builder.Services.AddHttpClient("huggingface");
builder.Services.AddHttpClient("groq");

// Register embedding service implementation based on configuration

// Use Groq embedding service
builder.Services.AddSingleton<IEmbeddingService, GroqEmbeddingService>();

var vectorType = builder.Configuration["VectorStore:Type"]?.ToLowerInvariant() ?? "memory";
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
var dgKey = builder.Configuration["Deepgram:ApiKey"];
if (!string.IsNullOrEmpty(dgKey)) builder.Services.AddSingleton<ISttService, DeepgramSttService>();
else builder.Services.AddSingleton<ISttService, MockSttService>();

var elKey = builder.Configuration["ElevenLabs:ApiKey"];
if (!string.IsNullOrEmpty(elKey)) builder.Services.AddSingleton<ITtsService, ElevenLabsTtsService>();
else builder.Services.AddSingleton<ITtsService, MockTtsService>();
// Use Groq generation service
builder.Services.AddSingleton<IAiService, GroqGenerationService>();
builder.Services.AddSingleton<IRagService, RagService>();
builder.Services.AddSingleton<IConversationManager, ConversationManager>();
builder.Services.AddSingleton<IStreamWebSocketHandler, StreamWebSocketHandler>();
builder.Services.AddSingleton<TwilioValidator>();



var app = builder.Build();

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

    var ws = await context.WebSockets.AcceptWebSocketAsync();
    var handler = app.Services.GetRequiredService<IStreamWebSocketHandler>();
    await handler.HandleAsync(ws);
});

app.Run();
