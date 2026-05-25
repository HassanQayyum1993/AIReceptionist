using AIReceptionist.Api.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
        if (string.IsNullOrWhiteSpace(callSid)) throw new ArgumentException("callSid is required", nameof(callSid));

        var state = _store.GetOrCreate(callSid);

        if (string.IsNullOrWhiteSpace(utterance))
        {
            _log.LogWarning("Empty utterance received for call {callSid}", callSid);
            return;
        }

        // Add user message immediately so conversation state reflects what was received
        state.Messages.Add(new ChatMessage { Role = "user", Text = utterance });

        try
        {
            // Skip vector DB retrieval; use only conversation history and system prompt
            var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine(SystemPrompt);
            promptBuilder.AppendLine("\n-- Conversation --\n");
            foreach (var m in state.Messages.TakeLast(10)) promptBuilder.AppendLine($"{m.Role}: {m.Text}");
            promptBuilder.AppendLine("\nAI:");

            var prompt = promptBuilder.ToString();
            _log.LogDebug("Built prompt for call {callSid}: {prompt}", callSid, prompt);

            var response = await _ai.GenerateAsync(prompt);
            if (string.IsNullOrWhiteSpace(response))
            {
                _log.LogWarning("AI returned empty response for call {callSid}", callSid);
                response = "I'm sorry, I don't have an answer to that right now. I will follow up shortly.";
            }

            state.Messages.Add(new ChatMessage { Role = "assistant", Text = response });

            // Synthesize
            byte[]? audio = null;
            string ttsVoice = "aura-asteria-en"; // Default Deepgram voice, update as needed
            try
            {
                audio = await _tts.SynthesizeAsync(response, ttsVoice);
            }
            catch (Exception ttsEx)
            {
                _log.LogError(ttsEx, "TTS synthesis failed for call {callSid}", callSid);
            }

            // Send audio back via WebSocket handler (resolved at call time to avoid DI cycle)
            var wsHandler = _services.GetRequiredService<IStreamWebSocketHandler>();
            if (audio != null && audio.Length > 0)
            {
                await wsHandler.SendAudioToCallerAsync(callSid, audio);
            }
            else
            {
                // Fallback: attempt to synthesize a short error message and send it
                try
                {
                    var fallback = "Sorry, I'm having trouble generating audio right now. I will follow up shortly.";
                    var fallbackAudio = await _tts.SynthesizeAsync(fallback, ttsVoice);
                    await wsHandler.SendAudioToCallerAsync(callSid, fallbackAudio);
                }
                catch (Exception fallbackEx)
                {
                    _log.LogError(fallbackEx, "Failed to send fallback audio for call {callSid}", callSid);
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception and try to notify the caller with a short apology audio
            _log.LogError(ex, "Unhandled error while handling utterance for call {callSid}", callSid);

            state.Messages.Add(new ChatMessage { Role = "assistant", Text = "Sorry, something went wrong while handling your request." });

            try
            {
                var wsHandler = _services.GetRequiredService<IStreamWebSocketHandler>();
                var errAudio = await _tts.SynthesizeAsync("Sorry, something went wrong. Please try again later.", "aura-asteria-en");
                await wsHandler.SendAudioToCallerAsync(callSid, errAudio);
            }
            catch (Exception notifyEx)
            {
                _log.LogError(notifyEx, "Failed to notify caller about error for call {callSid}", callSid);
            }
        }
    }
}
