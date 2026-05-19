using AIReceptionist.Api.Domain;
using AIReceptionist.Api.Services;
using System.Collections.Concurrent;

namespace AIReceptionist.Api.Stores;

public class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, ConversationState> _store = new();

    public ConversationState GetOrCreate(string callSid)
    {
        return _store.GetOrAdd(callSid, sid => new ConversationState { CallSid = sid });
    }

    public void Remove(string callSid)
    {
        _store.TryRemove(callSid, out _);
    }
}
