using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using MicrosoftAgentFrameworkAPI.Services;

namespace MicrosoftAgentFrameworkAPI.Controllers;

/// <summary>
/// Phase 3 — Multi-Turn Conversations: create sessions and run agent with context.
/// Phase 4 — Memory and Persistence: extract user facts and inject them as context.
///
/// Flow:
///   1. POST /api/sessions          → create a session, get back sessionId
///   2. POST /api/sessions/{id}/run → send messages; agent remembers turns + user facts
///   3. GET  /api/sessions/{id}/memory → inspect stored user facts
///   4. DELETE /api/sessions/{id}   → end the session
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly AIAgent _agent;
    private readonly SessionService _sessionService;
    private readonly UserMemoryService _memoryService;

    // Phase 6: resolve "main-agent" by key from the hosting DI registration
    public SessionController(
        [FromKeyedServices("main-agent")] AIAgent agent,
        SessionService sessionService,
        UserMemoryService memoryService)
    {
        _agent = agent;
        _sessionService = sessionService;
        _memoryService = memoryService;
    }

    /// <summary>
    /// POST /api/sessions
    /// Creates a new conversation session and returns its ID.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateSession()
    {
        var sessionId = await _sessionService.CreateSessionAsync(_agent);
        return Ok(new { sessionId });
    }

    /// <summary>
    /// POST /api/sessions/{sessionId}/run
    /// Sends a message within an existing session.
    /// Phase 4: Extracts user facts from the message and injects stored context.
    /// </summary>
    [HttpPost("{sessionId}/run")]
    public async Task<IActionResult> Run(string sessionId, [FromBody] AgentRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message cannot be empty.");

        var session = _sessionService.GetSession(sessionId);
        if (session is null)
            return NotFound($"Session '{sessionId}' not found.");

        // Phase 4: extract facts from the user's message before sending to agent
        _memoryService.ExtractAndStore(sessionId, request.Message);

        // Phase 4: build a context-enriched message list
        // If we have stored user facts, prepend a system instruction with that context
        string? contextInstruction = _memoryService.BuildContextInstruction(sessionId);
        IEnumerable<ChatMessage> messages;

        if (!string.IsNullOrEmpty(contextInstruction))
        {
            messages =
            [
                new ChatMessage(ChatRole.System, contextInstruction),
                new ChatMessage(ChatRole.User, request.Message),
            ];
        }
        else
        {
            messages = [new ChatMessage(ChatRole.User, request.Message)];
        }

        var result = await _agent.RunAsync(messages, session);
        return Ok(new { sessionId, response = result.ToString() });
    }

    /// <summary>
    /// GET /api/sessions/{sessionId}/memory
    /// Phase 4: Returns the user facts stored for this session.
    /// </summary>
    [HttpGet("{sessionId}/memory")]
    public IActionResult GetMemory(string sessionId)
    {
        var session = _sessionService.GetSession(sessionId);
        if (session is null)
            return NotFound($"Session '{sessionId}' not found.");

        var snapshot = _memoryService.GetSnapshot(sessionId);
        return Ok(new { sessionId, memory = snapshot });
    }

    /// <summary>
    /// GET /api/sessions
    /// Returns all active session IDs.
    /// </summary>
    [HttpGet]
    public IActionResult GetSessions()
    {
        var ids = _sessionService.GetActiveSessionIds();
        return Ok(new { sessions = ids });
    }

    /// <summary>
    /// DELETE /api/sessions/{sessionId}
    /// Ends and removes a session, also clearing its stored memory.
    /// </summary>
    [HttpDelete("{sessionId}")]
    public IActionResult DeleteSession(string sessionId)
    {
        var session = _sessionService.GetSession(sessionId);
        if (session is null)
            return NotFound($"Session '{sessionId}' not found.");

        _sessionService.RemoveSession(sessionId);
        _memoryService.Remove(sessionId);
        return Ok(new { message = $"Session '{sessionId}' ended." });
    }
}
