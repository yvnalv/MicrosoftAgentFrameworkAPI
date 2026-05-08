using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using MicrosoftAgentFrameworkAPI.Agents;
using MicrosoftAgentFrameworkAPI.Agents.Workflows;
using MicrosoftAgentFrameworkAPI.Services;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Phase 3: in-memory session service (keeps AgentSession alive across HTTP calls)
builder.Services.AddSingleton<SessionService>();

// Phase 4: user memory extraction service
builder.Services.AddSingleton<UserMemoryService>();

// ─────────────────────────────────────────────────────────────────────────────
// Phase 6: Hosting — register the agent via IHostApplicationBuilder.AddAIAgent
// Uses Chat Completions API (OpenAI.Chat.ChatClient) instead of the Responses
// API so this works with standalone Azure OpenAI endpoints (/openai/v1).
// ─────────────────────────────────────────────────────────────────────────────
builder.AddAIAgent(
    "main-agent",
    (sp, agentName) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var endpoint = config["AzureAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureAI:Endpoint is not configured.");
        var deploymentName = config["AzureAI:DeploymentName"] ?? "gpt-4o-mini";
        var apiKey = config["AzureAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureAI:ApiKey is not configured.");

        var tools = new[]
        {
            AIFunctionFactory.Create(AgentTools.GetWeather),
            AIFunctionFactory.Create(AgentTools.GetWeatherForecast),
        };

        IChatClient chatClient = new ChatClient(
            deploymentName,
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
        ).AsIChatClient();

        return new ChatClientAgent(
            chatClient,
            instructions: "You are a friendly and helpful assistant. Use the available tools when the user asks about weather. Keep your answers concise.",
            name: agentName,
            description: "",
            tools: tools,
            loggerFactory: null,
            services: null);
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
