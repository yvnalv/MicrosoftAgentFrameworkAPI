using Microsoft.Agents.AI;
using System.Collections.Concurrent;

namespace MicrosoftAgentFrameworkAPI.Services;

/// <summary>
/// Phase 3 — Multi-Turn Sessions
/// Singleton service that maintains in-memory AgentSession instances.
/// Each session preserves conversation history so the agent can refer back
/// to earlier turns in the same conversation.
/// </summary>
public class SessionService
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    /// <summary>
    /// Creates a new session for the given agent and returns its ID.
    /// </summary>
    public async Task<string> CreateSessionAsync(AIAgent agent)
    {
        var session = await agent.CreateSessionAsync();
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = session;
        return sessionId;
    }

    /// <summary>
    /// Retrieves an existing session by ID. Returns null if not found.
    /// </summary>
    public AgentSession? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    /// <summary>
    /// Removes and disposes a session by ID.
    /// </summary>
    public void RemoveSession(string sessionId) =>
        _sessions.TryRemove(sessionId, out _);

    /// <summary>
    /// Returns the IDs of all active sessions.
    /// </summary>
    public IEnumerable<string> GetActiveSessionIds() => _sessions.Keys;
}
