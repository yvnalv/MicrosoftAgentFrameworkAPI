using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using MicrosoftAgentFrameworkAPI.Agents.Workflows;

namespace MicrosoftAgentFrameworkAPI.Controllers;

/// <summary>
/// Phase 5 — Workflows
/// Runs the two-step text processing workflow and returns each executor's output.
///
/// POST /api/workflow/run — accepts { "text": "..." } and runs it through:
///   uppercase → reverse
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private static readonly Workflow _workflow = TextWorkflow.Build();

    /// <summary>
    /// POST /api/workflow/run
    /// Runs the text workflow and returns per-step results.
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] WorkflowRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text cannot be empty.");

        // Run the workflow in-process (non-streaming)
        await using var run = await InProcessExecution.RunAsync(_workflow, request.Text);

        var stepResults = new List<object>();

        foreach (var evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent completed)
            {
                stepResults.Add(new
                {
                    step = completed.ExecutorId,
                    output = completed.Data?.ToString()
                });
            }
            else if (evt is WorkflowOutputEvent output)
            {
                stepResults.Add(new
                {
                    step = "WorkflowOutput",
                    output = output.Data?.ToString()
                });
            }
        }

        return Ok(new
        {
            input = request.Text,
            steps = stepResults
        });
    }
}

public record WorkflowRunRequest(string Text);
