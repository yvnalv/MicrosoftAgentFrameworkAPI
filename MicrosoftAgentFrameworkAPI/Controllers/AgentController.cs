using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;

namespace MicrosoftAgentFrameworkAPI.Controllers;

/// <summary>
/// Phase 1 — Basic Agent: POST /api/agent/run and /api/agent/run-stream
/// Phase 2 — Add Tools: agent has GetWeather and GetWeatherForecast tools
/// Phase 6 — Hosting: agent resolved by name from DI via builder.AddAIAgent()
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly AIAgent _agent;

    // Phase 6: [FromKeyedServices] resolves the "main-agent" registered via builder.AddAIAgent()
    public AgentController([FromKeyedServices("main-agent")] AIAgent agent)
    {
        _agent = agent;
    }

    /// <summary>
    /// POST /api/agent/run
    /// Runs the agent with the provided message and returns the full response.
    /// The agent will automatically call tools (e.g., GetWeather) when relevant.
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] AgentRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message cannot be empty.");

        var result = await _agent.RunAsync(request.Message);
        return Ok(new { response = result });
    }

    /// <summary>
    /// POST /api/agent/run-stream
    /// Streams the agent response token by token using Server-Sent Events.
    /// </summary>
    [HttpPost("run-stream")]
    public async Task RunStream([FromBody] AgentRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        await foreach (var chunk in _agent.RunStreamingAsync(request.Message))
        {
            await Response.WriteAsync($"data: {chunk}\n\n");
            await Response.Body.FlushAsync();
        }
    }
}

public record AgentRunRequest(string Message);
