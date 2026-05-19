using AIReceptionist.Api.Domain;
using Microsoft.Extensions.Logging;

namespace AIReceptionist.Api.Services;

public class ConversationManager : IConversationManager
{
    private readonly IConversationStore _store;
    private readonly IRagService _rag;
    private readonly IAiService _ai;
    private readonly ITtsService _tts;
    private readonly IServiceProvider _services;
    private readonly ILogger<ConversationManager> _log;

    private const string SystemPrompt = "You are a professional AI receptionist for a company. Your responsibilities: - Answer customer questions clearly and politely - Provide accurate information from provided context - If unsure, say you will check and follow up - Keep responses short and conversational for voice - Always maintain professional tone";

    public ConversationManager(IConversationStore store, IRagService rag, IAiService ai, ITtsService tts, IServiceProvider services, ILogger<ConversationManager> log)
    {
        _store = store;
        _rag = rag;
        _ai = ai;
        _tts = tts;
        _services = services;
        _log = log;
    }

    public async Task HandleUtteranceAsync(string callSid, string utterance)
    {
        var state = _store.GetOrCreate(callSid);
        state.Messages.Add(new ChatMessage { Role = "user", Text = utterance });

        var retrieved = await _rag.RetrieveAsync(utterance, 3);

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine(SystemPrompt);
        promptBuilder.AppendLine("\n-- Retrieved Knowledge --\n");
        foreach (var r in retrieved) promptBuilder.AppendLine(r + "\n");
        promptBuilder.AppendLine("\n-- Conversation --\n");
        foreach (var m in state.Messages.TakeLast(10)) promptBuilder.AppendLine($"{m.Role}: {m.Text}");
        promptBuilder.AppendLine("\nAI:");

        var prompt = promptBuilder.ToString();
        _log.LogInformation("Built prompt: {p}", prompt);

        var response = await _ai.GenerateAsync(prompt);
        state.Messages.Add(new ChatMessage { Role = "assistant", Text = response });

        // Synthesize
        var audio = await _tts.SynthesizeAsync(response, "alloy");

        // Send audio back via WebSocket handler (resolved at call time to avoid DI cycle)
        var wsHandler = _services.GetRequiredService<IStreamWebSocketHandler>();
        await wsHandler.SendAudioToCallerAsync(callSid, audio);
    }
}
