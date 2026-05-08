using Microsoft.Agents.AI.Workflows;

namespace MicrosoftAgentFrameworkAPI.Agents.Workflows;

/// <summary>
/// Phase 5 — Workflows
/// A two-step text processing pipeline built with WorkflowBuilder.
///
/// Steps:
///   1. UppercaseExecutor — converts input text to uppercase
///   2. ReverseExecutor   — reverses the uppercase string
///
/// Both steps expose their output so the controller can collect all results.
/// </summary>
public static class TextWorkflow
{
    /// <summary>
    /// Builds and returns the two-step text processing Workflow.
    /// </summary>
    public static Workflow Build()
    {
        // Step 1: uppercase — uses BindAsExecutor on a plain Func<string, string>
        Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
        var uppercase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");

        // Step 2: reverse — uses a typed Executor<TIn, TOut> subclass
        var reverse = new ReverseTextExecutor();

        // Wire the pipeline: input → uppercase → reverse → output
        WorkflowBuilder builder = new(uppercase);
        builder.AddEdge(uppercase, reverse)
               .WithOutputFrom(reverse);

        return builder.Build();
    }
}

/// <summary>
/// Phase 5 — Reverse text executor.
/// Reverses the characters in the input string and yields the result as output.
/// </summary>
internal sealed class ReverseTextExecutor()
    : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(string.Concat(message.Reverse()));
}
