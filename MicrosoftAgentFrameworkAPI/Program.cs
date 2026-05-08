using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using MicrosoftAgentFrameworkAPI.Agents;
using MicrosoftAgentFrameworkAPI.Agents.Workflows;
using MicrosoftAgentFrameworkAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Phase 3: in-memory session service (keeps AgentSession alive across HTTP calls)
builder.Services.AddSingleton<SessionService>();

// Phase 4: user memory extraction service
builder.Services.AddSingleton<UserMemoryService>();

// ─────────────────────────────────────────────────────────────────────────────
// Phase 6: Hosting — register the agent via IHostApplicationBuilder.AddAIAgent
// This replaces the manual AddSingleton<AIAgent> from Phases 1-4 and makes the
// agent resolvable by name (keyed service) across all controllers.
// ─────────────────────────────────────────────────────────────────────────────
builder.AddAIAgent(
    "main-agent",
    (sp, agentName) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var endpoint = config["AzureAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureAI:Endpoint is not configured.");
        var deploymentName = config["AzureAI:DeploymentName"] ?? "gpt-4o-mini";

        var tools = new[]
        {
            AIFunctionFactory.Create(AgentTools.GetWeather),
            AIFunctionFactory.Create(AgentTools.GetWeatherForecast),
        };

        return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
            .AsAIAgent(
                model: deploymentName,
                instructions: "You are a friendly and helpful assistant. Use the available tools when the user asks about weather. Keep your answers concise.",
                name: agentName,
                tools: tools);
    })
    .WithInMemorySessionStore();   // Phase 6: managed session store via hosting layer

// Phase 6: register the text workflow and expose it as a named AIAgent
builder.AddWorkflow(
    "text-pipeline",
    (_, _) => TextWorkflow.Build())
    .AddAsAIAgent();               // workflow is now resolvable as keyed AIAgent "text-pipeline"

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
