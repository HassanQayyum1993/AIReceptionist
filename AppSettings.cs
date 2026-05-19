namespace AIReceptionist.Api;

public class AppSettings
{
    public DeepgramSettings? Deepgram { get; set; }
    public OpenAiSettings? OpenAI { get; set; }
    public ElevenLabsSettings? ElevenLabs { get; set; }
    public VectorStoreSettings? VectorStore { get; set; }
    public TwilioSettings? Twilio { get; set; }
    public PineconeSettings? Pinecone { get; set; }
    public StreamingSettings? Streaming { get; set; }
    public HuggingFaceSettings? HuggingFace { get; set; }
    public GroqSettings? Groq { get; set; }
}

public class DeepgramSettings { public string? ApiKey { get; set; } public string? RealtimeUrl { get; set; } }
public class OpenAiSettings { public string? ApiKey { get; set; } public string? BaseUrl { get; set; } }
public class ElevenLabsSettings { public string? ApiKey { get; set; } public string? Voice { get; set; } }
public class VectorStoreSettings { public string? Type { get; set; } }

public class TwilioSettings { public string? AccountSid { get; set; } public string? AuthToken { get; set; } public string? ApiKey { get; set; } public string? ApiSecret { get; set; } }
public class PineconeSettings { public string? ApiKey { get; set; } public string? Environment { get; set; } public string? IndexName { get; set; } }
public class StreamingSettings { public string? Url { get; set; } }
public class HuggingFaceSettings { public string? ApiKey { get; set; } public string? BaseUrl { get; set; } public string? Model { get; set; } }
public class GroqSettings { public string? ApiKey { get; set; } public string? BaseUrl { get; set; } public string? Model { get; set; } }
