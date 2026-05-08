# Implementation Plan — Microsoft Agent Framework API

**Project:** MicrosoftAgentFrameworkAPI
**Stack:** ASP.NET Core 10.0 · C# · Microsoft Agent Framework 1.5.0
**Reference:** [Microsoft Agent Framework — Get Started](https://learn.microsoft.com/en-us/agent-framework/get-started/)

---

## Overview

This project builds a minimal but complete AI agent API by progressively implementing all six
tutorial steps from the official Microsoft Agent Framework documentation. Each phase adds one
new capability and exposes it as a testable HTTP endpoint. Every phase compiles and runs
independently before the next one begins.

```
Phase 1  →  Phase 2  →  Phase 3  →  Phase 4  →  Phase 5  →  Phase 6
Basic        Tools        Sessions     Memory       Workflows    Hosting
Agent
```

---

## NuGet Packages

| Package | Version | Added In |
|---------|---------|----------|
| `Azure.AI.Projects` | 2.1.0-beta.1 | Pre-existing (template) |
| `Azure.Identity` | 1.21.0 | Pre-existing (template) |
| `Microsoft.Agents.AI.Foundry` | 1.5.0 | Pre-existing (template) |
| `Microsoft.AspNetCore.OpenApi` | 10.0.2 | Pre-existing (template) |
| `Microsoft.Agents.AI.Workflows` | 1.5.0 | Phase 5 |
| `Microsoft.Agents.AI.Hosting` | 1.5.0-preview.260507.1 | Phase 6 |

---

## Environment Setup

Before running the application, configure the Azure OpenAI endpoint in
`appsettings.Development.json`:

```json
{
  "AzureAI": {
    "Endpoint": "https://<your-sandbox>.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

Authentication uses `DefaultAzureCredential`. Run `az login` before starting the app.

---

## Phase 1 — Basic Agent

**Tutorial step:** [Step 1: Your First Agent](https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent)
**Goal:** Create an AI agent and expose it via a REST endpoint.

### Files Modified / Created

| File | Action |
|------|--------|
| `appsettings.json` | Add `"AzureAI"` config section |
| `appsettings.Development.json` | Add Azure endpoint placeholder |
| `Controllers/AgentController.cs` | **Create** — `/api/agent/run` and `/api/agent/run-stream` |

### Key Pattern

```csharp
AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(model: deploymentName, instructions: "...", name: "HelloAgent");

string result = await agent.RunAsync("What is the largest city in France?");
```

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/agent/run` | Send a message; returns full response |
| `POST` | `/api/agent/run-stream` | Send a message; streams response as SSE |

### Test

```http
POST http://localhost:5122/api/agent/run
Content-Type: application/json

{ "message": "What is the largest city in France?" }
```

**Expected:** `{ "response": "Paris is the largest city in France." }`

---

## Phase 2 — Add Tools

**Tutorial step:** [Step 2: Add Tools](https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools)
**Goal:** Give the agent callable functions it invokes automatically.

### Files Modified / Created

| File | Action |
|------|--------|
| `Agents/AgentTools.cs` | **Create** — `GetWeather` and `GetWeatherForecast` tool methods |
| `Controllers/AgentController.cs` | Update — pass tools to agent constructor |
| `Program.cs` | Update — move agent to DI singleton with tools |

### Key Pattern

```csharp
[Description("Get the current weather for a given location.")]
public static string GetWeather(
    [Description("The city or location to get the weather for.")] string location) { ... }

var tools = new[]
{
    AIFunctionFactory.Create(AgentTools.GetWeather),
    AIFunctionFactory.Create(AgentTools.GetWeatherForecast),
};

AIAgent agent = client.AsAIAgent(model: deploymentName, instructions: "...", tools: tools);
```

### Reused Existing Code

The weather data logic (random temperature, condition strings) was adapted from
`WeatherForecast.cs` and `WeatherForecastController.cs` (existing template files).

### Test

```http
POST http://localhost:5122/api/agent/run
Content-Type: application/json

{ "message": "What is the weather like in Amsterdam?" }
```

**Expected:** Agent calls `GetWeather("Amsterdam")` internally and returns a natural-language answer.

---

## Phase 3 — Multi-Turn Sessions

**Tutorial step:** [Step 3: Multi-Turn Conversations](https://learn.microsoft.com/en-us/agent-framework/get-started/multi-turn)
**Goal:** Maintain conversation history across multiple HTTP requests.

### Files Modified / Created

| File | Action |
|------|--------|
| `Services/SessionService.cs` | **Create** — singleton `ConcurrentDictionary<string, AgentSession>` |
| `Controllers/SessionController.cs` | **Create** — CRUD endpoints for sessions |
| `Program.cs` | Update — register `SessionService` as singleton |

### Key Pattern

```csharp
AgentSession session = await agent.CreateSessionAsync();
string r1 = await agent.RunAsync("My name is Alice.", session);
string r2 = await agent.RunAsync("What do you remember about me?", session);
```

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/sessions` | Create session → returns `{ "sessionId": "..." }` |
| `POST` | `/api/sessions/{id}/run` | Send message within session |
| `GET` | `/api/sessions` | List all active session IDs |
| `DELETE` | `/api/sessions/{id}` | End and remove a session |

### Test Flow

```http
# 1. Create session
POST http://localhost:5122/api/sessions

# 2. First turn
POST http://localhost:5122/api/sessions/{sessionId}/run
{ "message": "My name is Alice and I love hiking." }

# 3. Second turn — agent should remember
POST http://localhost:5122/api/sessions/{sessionId}/run
{ "message": "What do you remember about me?" }
```

**Expected (turn 3):** Response references Alice's name and hiking.

---

## Phase 4 — Memory and Persistence

**Tutorial step:** [Step 4: Memory & Persistence](https://learn.microsoft.com/en-us/agent-framework/get-started/memory)
**Goal:** Extract user facts from conversation and inject them as persistent context.

### Files Modified / Created

| File | Action |
|------|--------|
| `Services/UserMemoryService.cs` | **Create** — extracts and stores user facts per session |
| `Controllers/SessionController.cs` | Update — call `UserMemoryService` before/after each run |
| `Program.cs` | Update — register `UserMemoryService` as singleton |

### Architecture

`UserMemoryService` maintains a `ConcurrentDictionary<string, UserMemory>` keyed by session ID.
On each `POST /api/sessions/{id}/run`:

1. `ExtractAndStore(sessionId, message)` — parses "my name is X" / "I love Y" patterns.
2. `BuildContextInstruction(sessionId)` — returns a system instruction string, e.g.
   `"The user's name is Alice. Always address them by name."`
3. That instruction is prepended as a `ChatMessage(ChatRole.System, ...)` before the user turn.

### Endpoints (added)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/sessions/{id}/memory` | Returns stored facts as `{ "memory": { ... } }` |

### Test Flow

```http
# 1. Tell the agent your name
POST http://localhost:5122/api/sessions/{sessionId}/run
{ "message": "My name is Alice and I love hiking." }

# 2. Ask something unrelated
POST http://localhost:5122/api/sessions/{sessionId}/run
{ "message": "What is 2 + 2?" }

# 3. Inspect stored memory
GET http://localhost:5122/api/sessions/{sessionId}/memory
```

**Expected (step 3):** `{ "memory": { "user_name": "Alice", "user_hobby": "hiking" } }`
**Expected (step 2):** Response addresses Alice by name even though the question is unrelated.

---

## Phase 5 — Workflows

**Tutorial step:** [Step 5: Workflows](https://learn.microsoft.com/en-us/agent-framework/get-started/workflows)
**Goal:** Chain multiple processing steps in a sequential pipeline.

### New Package

```
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
```

### Files Modified / Created

| File | Action |
|------|--------|
| `Agents/Workflows/TextWorkflow.cs` | **Create** — two-step pipeline (uppercase → reverse) |
| `Controllers/WorkflowController.cs` | **Create** — `POST /api/workflow/run` endpoint |

### Key Pattern

```csharp
// Step 1: convert a Func to an executor
Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
var uppercase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");

// Step 2: typed executor subclass
class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(string msg, IWorkflowContext ctx, CancellationToken ct)
        => ValueTask.FromResult(string.Concat(msg.Reverse()));
}

// Wire
WorkflowBuilder builder = new(uppercase);
builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
Workflow workflow = builder.Build();

// Run
await using Run run = await InProcessExecution.RunAsync(workflow, "Hello, World!");
foreach (WorkflowEvent evt in run.NewEvents)
{
    if (evt is ExecutorCompletedEvent e)
        Console.WriteLine($"{e.ExecutorId}: {e.Data}");
}
```

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/workflow/run` | Runs `{ "text": "..." }` through the two-step pipeline |

### Test

```http
POST http://localhost:5122/api/workflow/run
Content-Type: application/json

{ "text": "Hello, World! This is Agent Framework." }
```

**Expected:**
```json
{
  "input": "Hello, World! This is Agent Framework.",
  "steps": [
    { "step": "UppercaseExecutor", "output": "HELLO, WORLD! THIS IS AGENT FRAMEWORK." },
    { "step": "ReverseTextExecutor", "output": ".KROWEMARFTNEGASI SIHT !DLROW ,OLLEH" }
  ]
}
```

---

## Phase 6 — Hosting

**Tutorial step:** [Step 6: Host Your Agent](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting)
**Goal:** Register agents via the hosting library DI pattern and resolve them by name.

### New Package

```
dotnet add package Microsoft.Agents.AI.Hosting --prerelease
```

### Files Modified

| File | Action |
|------|--------|
| `Program.cs` | Replace `AddSingleton<AIAgent>` with `builder.AddAIAgent(...)` |
| `Controllers/AgentController.cs` | Update constructor — `[FromKeyedServices("main-agent")]` |
| `Controllers/SessionController.cs` | Update constructor — `[FromKeyedServices("main-agent")]` |

### Key Pattern

```csharp
// Register named agent using the hosting builder extension
builder.AddAIAgent(
    "main-agent",
    (sp, agentName) =>
    {
        // factory creates AIAgent using AIProjectClient.AsAIAgent()
        return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
            .AsAIAgent(model: deploymentName, instructions: "...", name: agentName, tools: tools);
    })
    .WithInMemorySessionStore();

// Register the text workflow and expose it as a named AIAgent
builder.AddWorkflow("text-pipeline", (_, _) => TextWorkflow.Build())
       .AddAsAIAgent();

// Resolve by key in controllers
public AgentController([FromKeyedServices("main-agent")] AIAgent agent) { ... }
```

### Test

```http
# Agent resolves from DI by key
POST http://localhost:5122/api/agent/run
{ "message": "What is 7 times 6?" }

# OpenAPI spec — shows all registered endpoints
GET http://localhost:5122/openapi/v1.json
```

---

## Project Structure (Final)

```
MicrosoftAgentFrameworkAPI/
├── Agents/
│   ├── AgentTools.cs                    Phase 2 — tool definitions
│   └── Workflows/
│       └── TextWorkflow.cs              Phase 5 — workflow + executors
├── Controllers/
│   ├── AgentController.cs               Phase 1, 2, 6
│   ├── SessionController.cs             Phase 3, 4, 6
│   ├── WeatherForecastController.cs     Original template (unchanged)
│   └── WorkflowController.cs            Phase 5
├── Services/
│   ├── SessionService.cs                Phase 3
│   └── UserMemoryService.cs             Phase 4
├── WeatherForecast.cs                   Original template (unchanged)
├── Program.cs                           Updated each phase
├── appsettings.json                     Phase 1 — AzureAI section added
├── appsettings.Development.json         Phase 1 — endpoint placeholder
└── MicrosoftAgentFrameworkAPI.http      All phase test requests
```

---

## Running the Application

```bash
# 1. Set Azure CLI credentials
az login

# 2. Update appsettings.Development.json with your Azure endpoint

# 3. Run the application
dotnet run

# 4. Application listens on:
#    HTTP  → http://localhost:5122
#    HTTPS → https://localhost:7009
```

Use the `.http` file in Visual Studio or VS Code (REST Client extension) to run all
phase-specific test requests.
