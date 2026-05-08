using System.Collections.Concurrent;

namespace MicrosoftAgentFrameworkAPI.Services;

/// <summary>
/// Phase 4 — Memory and Persistence
/// Stores user-specific facts (e.g., name, preferences) keyed by session ID.
/// The data is extracted from conversation text after each agent turn and injected
/// as additional system context on the next turn.
/// </summary>
public class UserMemoryService
{
    private readonly ConcurrentDictionary<string, UserMemory> _memories = new();

    /// <summary>
    /// Retrieves stored memory for a session. Creates an empty record if none exists yet.
    /// </summary>
    public UserMemory GetOrCreate(string sessionId) =>
        _memories.GetOrAdd(sessionId, _ => new UserMemory());

    /// <summary>
    /// Returns all memory keys for a session as a dictionary snapshot (for the state endpoint).
    /// </summary>
    public Dictionary<string, string> GetSnapshot(string sessionId) =>
        _memories.TryGetValue(sessionId, out var mem)
            ? new Dictionary<string, string>(mem.Facts)
            : [];

    /// <summary>
    /// Removes stored memory for a session (called when a session is deleted).
    /// </summary>
    public void Remove(string sessionId) => _memories.TryRemove(sessionId, out _);

    /// <summary>
    /// Parses a user message for simple facts (e.g., "my name is X") and stores them.
    /// </summary>
    public void ExtractAndStore(string sessionId, string userMessage)
    {
        var mem = GetOrCreate(sessionId);
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("my name is"))
        {
            var part = lower.Split("my name is").Last().Trim();
            var name = part.Split([' ', '.', ',', '!', '?'])[0];
            if (!string.IsNullOrEmpty(name))
                mem.Facts["user_name"] = char.ToUpper(name[0]) + name[1..];
        }

        if (lower.Contains("i love") || lower.Contains("i like"))
        {
            var keyword = lower.Contains("i love") ? "i love " : "i like ";
            var hobby = lower.Split(keyword).Last().Split(['.', ',', '!', '?'])[0].Trim();
            if (!string.IsNullOrEmpty(hobby))
                mem.Facts["user_hobby"] = hobby;
        }
    }

    /// <summary>
    /// Builds an instruction string from stored memory to inject as context for the agent.
    /// </summary>
    public string BuildContextInstruction(string sessionId)
    {
        var mem = GetOrCreate(sessionId);
        if (mem.Facts.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        if (mem.Facts.TryGetValue("user_name", out var name))
            parts.Add($"The user's name is {name}. Always address them by name.");
        if (mem.Facts.TryGetValue("user_hobby", out var hobby))
            parts.Add($"The user enjoys {hobby}.");

        return string.Join(" ", parts);
    }
}

/// <summary>
/// Holds the extracted facts for a single user session.
/// </summary>
public class UserMemory
{
    public Dictionary<string, string> Facts { get; } = new(StringComparer.OrdinalIgnoreCase);
}
