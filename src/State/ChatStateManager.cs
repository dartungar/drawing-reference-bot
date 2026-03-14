using System.Collections.Concurrent;

public sealed class ChatStateManager
{
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<long, ChatState> _states = new();

    public void SetPendingTopic(long chatId, string topic)
    {
        var state = _states.GetOrAdd(chatId, _ => new ChatState());
        state.PendingTopic = topic;
        state.PendingTopicAt = DateTimeOffset.UtcNow;
    }

    public string? GetAndRemovePendingTopic(long chatId)
    {
        if (!_states.TryGetValue(chatId, out var state))
        {
            return null;
        }

        if (state.PendingTopic is null || state.PendingTopicAt is null)
        {
            return null;
        }

        if (DateTimeOffset.UtcNow - state.PendingTopicAt.Value > _ttl)
        {
            state.PendingTopic = null;
            state.PendingTopicAt = null;
            return null;
        }

        var topic = state.PendingTopic;
        state.PendingTopic = null;
        state.PendingTopicAt = null;
        return topic;
    }

    public void SetLastSubject(long chatId, string subject)
    {
        var state = _states.GetOrAdd(chatId, _ => new ChatState());
        state.LastSubject = subject;
    }

    public string? GetLastSubject(long chatId)
    {
        return _states.TryGetValue(chatId, out var state) ? state.LastSubject : null;
    }

    public void SetLastSource(long chatId, string source)
    {
        var state = _states.GetOrAdd(chatId, _ => new ChatState());
        state.LastSource = source;
    }

    public string? GetLastSource(long chatId)
    {
        return _states.TryGetValue(chatId, out var state) ? state.LastSource : null;
    }

    private sealed class ChatState
    {
        public string? PendingTopic { get; set; }
        public DateTimeOffset? PendingTopicAt { get; set; }
        public string? LastSubject { get; set; }
        public string? LastSource { get; set; }
    }
}
