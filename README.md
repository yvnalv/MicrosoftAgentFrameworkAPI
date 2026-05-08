# Microsoft Agent Framework API

A minimal, production-pattern AI agent API built with **ASP.NET Core 10** and the
**Microsoft Agent Framework**. The project implements all six official tutorial steps
progressively, each exposed as a testable REST endpoint.

> **Reference docs:** https://learn.microsoft.com/en-us/agent-framework/get-started/

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Project Structure](#2-project-structure)
3. [Configuration](#3-configuration)
4. [Running the Application](#4-running-the-application)
5. [API Reference](#5-api-reference)
6. [Testing Guide](#6-testing-guide)
   - [6.1 Visual Studio .http file](#61-using-the-http-file-visual-studio)
   - [6.2 VS Code REST Client](#62-using-the-http-file-vs-code)
   - [6.3 curl](#63-using-curl)
   - [6.4 Postman](#64-using-postman)
   - [6.5 Test Scenarios](#65-test-scenarios-by-phase)
7. [Use Cases](#7-use-cases)
8. [Extending the Project](#8-extending-the-project)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Prerequisites

### Required Software

| Tool | Minimum Version | Purpose |
|------|----------------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 | Build and run the application |
| [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) | 2.60+ | `DefaultAzureCredential` authentication |
| [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) | Latest | IDE (optional but recommended) |

### Required Azure Resources

| Resource | Purpose |
|----------|---------|
| Azure OpenAI resource or Azure AI Foundry project | Hosts the LLM deployment |
| A model deployment (e.g., `gpt-4o-mini`) | The model the agent uses |

### Verify Installation

```bash
dotnet --version          # should show 10.x.x
az --version              # should show 2.60+
az account show           # confirms you are logged in
```

---

## 2. Project Structure

```
MicrosoftAgentFrameworkAPI/
├── Agents/
│   ├── AgentTools.cs               Tool functions the agent can call automatically
│   └── Workflows/
│       └── TextWorkflow.cs         Sequential workflow pipeline (uppercase → reverse)
├── Controllers/
│   ├── AgentController.cs          Basic agent + streaming endpoints
│   ├── SessionController.cs        Multi-turn session + memory endpoints
│   ├── WeatherForecastController.cs  Original template (kept for reference)
│   └── WorkflowController.cs       Workflow execution endpoint
├── Services/
│   ├── SessionService.cs           In-memory AgentSession store
│   └── UserMemoryService.cs        User fact extraction and context injection
├── Properties/
│   └── launchSettings.json         HTTP/HTTPS launch profiles
├── appsettings.json                Base configuration (Azure endpoint placeholder)
├── appsettings.Development.json    Development overrides (set your endpoint here)
├── Program.cs                      DI registration and middleware pipeline
└── MicrosoftAgentFrameworkAPI.http  HTTP test requests for all phases
```

**Documentation files (solution root):**

```
Implementation Plan.md                              Phase-by-phase implementation plan with code patterns
Changelog.md                                        Full history of every file added or modified
README.md                                           This file
MicrosoftAgentFrameworkAPI.postman_collection.json  Postman collection — all phases with test scripts
MicrosoftAgentFrameworkAPI.postman_environment.json Postman environment — base_url and sessionId variables
```

---

## 3. Configuration

### 3.1 Azure Endpoint

Open `appsettings.Development.json` and set your Azure OpenAI or Azure AI Foundry endpoint:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureAI": {
    "Endpoint": "https://<your-resource-name>.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

**Finding your endpoint:**

- **Azure OpenAI:** Azure Portal → your OpenAI resource → *Keys and Endpoint* → copy the Endpoint URL.
- **Azure AI Foundry:** Azure AI Foundry portal → your project → *Overview* → copy the *Project endpoint*.

**Finding your deployment name:**

- **Azure OpenAI:** Azure Portal → your OpenAI resource → *Model deployments* → copy the deployment name.
- **Azure AI Foundry:** AI Foundry portal → *Deployments* tab → copy the name shown.

### 3.2 Authentication

This project uses `DefaultAzureCredential`, which automatically tries several authentication
methods in order. For local development the easiest method is the **Azure CLI**:

```bash
az login
```

If you have multiple subscriptions, select the correct one:

```bash
az account set --subscription "<subscription-name-or-id>"
```

### 3.3 Environment Variables (Alternative)

Instead of editing `appsettings.Development.json`, you can set environment variables:

**Windows (PowerShell):**
```powershell
$env:AzureAI__Endpoint = "https://<your-resource>.openai.azure.com/"
$env:AzureAI__DeploymentName = "gpt-4o-mini"
```

**Windows (Command Prompt):**
```cmd
set AzureAI__Endpoint=https://<your-resource>.openai.azure.com/
set AzureAI__DeploymentName=gpt-4o-mini
```

**Linux / macOS:**
```bash
export AzureAI__Endpoint="https://<your-resource>.openai.azure.com/"
export AzureAI__DeploymentName="gpt-4o-mini"
```

> **Note:** ASP.NET Core maps double-underscore `__` to the colon `:` separator in config keys.

### 3.4 Configuration Reference

| Key | Default | Description |
|-----|---------|-------------|
| `AzureAI:Endpoint` | *(empty — required)* | Azure OpenAI or AI Foundry project endpoint URL |
| `AzureAI:DeploymentName` | `gpt-4o-mini` | Name of the model deployment to use |
| `Logging:LogLevel:Default` | `Information` | Application log level |

---

## 4. Running the Application

### 4.1 From the Terminal

```bash
# Navigate to the project folder
cd MicrosoftAgentFrameworkAPI/MicrosoftAgentFrameworkAPI

# Restore packages and run
dotnet run
```

The application starts on:

| Scheme | URL |
|--------|-----|
| HTTP | `http://localhost:5122` |
| HTTPS | `https://localhost:7009` |

### 4.2 From Visual Studio

1. Open `MicrosoftAgentFrameworkAPI.slnx`.
2. Set the startup project to `MicrosoftAgentFrameworkAPI`.
3. Select the `https` profile from the toolbar dropdown.
4. Press **F5** (Debug) or **Ctrl+F5** (Run without debugger).

### 4.3 Verify the Application is Running

```bash
curl http://localhost:5122/weatherforecast
```

You should see a JSON array of 5 random weather forecasts. If the application starts
but the AI endpoints fail, see [Troubleshooting](#9-troubleshooting).

### 4.4 OpenAPI / Swagger (Development only)

When `ASPNETCORE_ENVIRONMENT=Development`, the OpenAPI spec is available at:

```
http://localhost:5122/openapi/v1.json
```

Paste the URL into [Swagger Editor](https://editor.swagger.io/) or open it in any
OpenAPI-compatible tool to explore and test all endpoints interactively.

---

## 5. API Reference

### Agent Endpoints

#### `POST /api/agent/run`

Sends a single message to the AI agent and returns the complete response.
The agent automatically calls registered tools (weather functions) when relevant.

**Request:**
```json
{ "message": "What is the weather like in Amsterdam?" }
```

**Response:**
```json
{
  "response": "The weather in Amsterdam is cloudy with a temperature of 14°C (57°F)."
}
```

---

#### `POST /api/agent/run-stream`

Sends a message and streams the response token-by-token using
[Server-Sent Events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events).

**Request:**
```json
{ "message": "Tell me a fun fact about the Netherlands." }
```

**Response (content-type: `text/event-stream`):**
```
data: The
data:  Netherlands
data:  is
data:  famous
data:  for...
```

---

### Session Endpoints

#### `POST /api/sessions`

Creates a new conversation session. The session keeps full message history so the
agent remembers everything said within it.

**Response:**
```json
{ "sessionId": "a3f2c1d0e4b5f6a7b8c9d0e1f2a3b4c5" }
```

---

#### `POST /api/sessions/{sessionId}/run`

Sends a message within an existing session. The agent has full context of all
previous turns. Also extracts and persists user facts (name, hobbies) for the
memory feature.

**Request:**
```json
{ "message": "My name is Alice and I love hiking." }
```

**Response:**
```json
{
  "sessionId": "a3f2c1d0...",
  "response": "Nice to meet you, Alice! Hiking sounds wonderful."
}
```

---

#### `GET /api/sessions/{sessionId}/memory`

Returns the user facts extracted and stored for this session.

**Response:**
```json
{
  "sessionId": "a3f2c1d0...",
  "memory": {
    "user_name": "Alice",
    "user_hobby": "hiking"
  }
}
```

---

#### `GET /api/sessions`

Lists all currently active session IDs.

**Response:**
```json
{
  "sessions": [
    "a3f2c1d0e4b5f6a7b8c9d0e1f2a3b4c5",
    "b4c3d2e1f0a9b8c7d6e5f4a3b2c1d0e9"
  ]
}
```

---

#### `DELETE /api/sessions/{sessionId}`

Ends a session and clears its stored conversation history and memory.

**Response:**
```json
{ "message": "Session 'a3f2c1d0...' ended." }
```

---

### Workflow Endpoints

#### `POST /api/workflow/run`

Runs the text processing workflow. The input passes through two executors in sequence:

1. **UppercaseExecutor** — converts the text to uppercase.
2. **ReverseTextExecutor** — reverses the characters of the uppercase text.

**Request:**
```json
{ "text": "Hello, World!" }
```

**Response:**
```json
{
  "input": "Hello, World!",
  "steps": [
    { "step": "UppercaseExecutor", "output": "HELLO, WORLD!" },
    { "step": "ReverseTextExecutor", "output": "!DLROW ,OLLEH" }
  ]
}
```

---

### Legacy / Template Endpoint

#### `GET /weatherforecast`

Original ASP.NET Core template endpoint — returns 5 random weather forecasts.
Kept for baseline verification that the app is running.

---

## 6. Testing Guide

### 6.1 Using the `.http` File (Visual Studio)

The file `MicrosoftAgentFrameworkAPI.http` contains ready-made requests for every
phase. In Visual Studio 2022:

1. Open the `.http` file.
2. Click the **Send Request** link above any request block.
3. The response appears in the pane on the right.

**Important:** For session-based requests (Phase 3, 4), copy the `sessionId` from the
response of `POST /api/sessions` and paste it into the subsequent request URLs
(replace `<sessionId>`).

### 6.2 Using the `.http` File (VS Code)

Install the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
extension, then open `MicrosoftAgentFrameworkAPI.http` and click **Send Request**.

### 6.3 Using curl

All examples below assume the app is running on `http://localhost:5122`.

**Phase 1 — Ask the agent a question:**
```bash
curl -X POST http://localhost:5122/api/agent/run \
  -H "Content-Type: application/json" \
  -d '{ "message": "What is the capital of Japan?" }'
```

**Phase 2 — Trigger a tool call:**
```bash
curl -X POST http://localhost:5122/api/agent/run \
  -H "Content-Type: application/json" \
  -d '{ "message": "What is the weather like in Tokyo?" }'
```

**Phase 3 — Multi-turn conversation:**
```bash
# Step 1: create session
SESSION=$(curl -s -X POST http://localhost:5122/api/sessions \
  -H "Content-Type: application/json" | jq -r '.sessionId')

# Step 2: first turn
curl -X POST http://localhost:5122/api/sessions/$SESSION/run \
  -H "Content-Type: application/json" \
  -d '{ "message": "My name is Bob and I love cycling." }'

# Step 3: second turn — agent remembers Bob
curl -X POST http://localhost:5122/api/sessions/$SESSION/run \
  -H "Content-Type: application/json" \
  -d '{ "message": "What sport do I enjoy?" }'
```

**Phase 4 — Inspect stored memory:**
```bash
curl http://localhost:5122/api/sessions/$SESSION/memory
```

**Phase 5 — Run the workflow:**
```bash
curl -X POST http://localhost:5122/api/workflow/run \
  -H "Content-Type: application/json" \
  -d '{ "text": "Microsoft Agent Framework" }'
```

### 6.4 Using Postman

#### Import the Collection

1. Open Postman.
2. Click **Import** (top-left).
3. Select **File** → choose `MicrosoftAgentFrameworkAPI.postman_collection.json` from the solution root.
4. Optionally import `MicrosoftAgentFrameworkAPI.postman_environment.json` as an environment.

The collection is organised into six phase folders plus a **Utilities** folder.

#### Collection Variables

The collection uses two built-in variables — no separate environment is required:

| Variable | Default value | Set by |
|----------|--------------|--------|
| `base_url` | `http://localhost:5122` | Pre-configured; change if your port differs |
| `sessionId` | *(empty)* | Auto-captured by the **Create Session** request test script |

To use HTTPS, click **…** on the collection → **Edit** → **Variables** → change `base_url` to `https://localhost:7009`.

#### Phase-by-Phase Postman Workflow

**Phase 1 — Basic Agent**

Run either request independently. No setup needed.

**Phase 2 — Agent with Tools**

Run either request independently. Check that the response mentions the city and includes temperature data — this confirms the tool was called.

**Phase 3 — Multi-Turn Sessions** (run in order)

| # | Request | What happens |
|---|---------|-------------|
| 1 | Create Session | Creates session; test script saves `sessionId` automatically |
| 2 | First Turn — Introduce Yourself | Sends first message into session |
| 3 | Second Turn — Test Context Recall | Agent should mention Alice and hiking |
| 4 | List All Active Sessions | Confirms session appears in the list |
| 5 | Delete Session | Removes session (204 No Content) |

> **Tip:** Use the Postman **Collection Runner** (click the folder → **Run**) to execute all five requests in sequence automatically.

**Phase 4 — Memory & Persistence** (run in order)

| # | Request | What happens |
|---|---------|-------------|
| 1 | Create Session (Memory Test) | New session; sessionId auto-saved |
| 2 | Introduce Yourself | Facts extracted: `user_name = Alice`, `user_hobby = hiking` |
| 3 | Unrelated Question | Agent addresses user as Alice despite unrelated question |
| 4 | Inspect Stored Memory | Returns `{ "user_name": "Alice", "user_hobby": "hiking" }` |

**Phase 5 — Workflows**

Run any of the three workflow requests independently. The test scripts validate each step's exact output.

**Phase 6 — Hosting (DI)**

Run requests independently to confirm DI resolution works. The OpenAPI spec request returns the full endpoint list.

#### Reading Test Results

After sending any request, click the **Test Results** tab in the response panel. Each test assertion is shown as **PASS** (green) or **FAIL** (red). All requests include pre-written test scripts covering:

- HTTP status codes
- Response field presence and types
- Content correctness (e.g., agent mentions the city, memory contains the right name)

#### Automated Run with Collection Runner

To run all phases at once:

1. Click the collection name → **Run collection**.
2. Deselect individual requests if needed (e.g. skip Phase 3 delete before running Phase 4).
3. Click **Run Microsoft Agent Framework API**.
4. View pass/fail summary across all requests.

> **SSE Streaming note:** Postman buffers the full SSE response body before displaying it. The `run-stream` request will show the complete streamed content at once rather than token-by-token. Use `curl -N` for true streaming observation.

---

### 6.5 Test Scenarios by Phase

#### Phase 1 — Basic Agent

| Scenario | Input message | Expected behaviour |
|----------|---------------|--------------------|
| Factual question | `"What is the tallest mountain?"` | Returns a direct answer |
| Math | `"What is 17 × 23?"` | Returns `391` |
| General knowledge | `"Who wrote Hamlet?"` | Returns `Shakespeare` |

#### Phase 2 — Tools

| Scenario | Input message | Expected behaviour |
|----------|---------------|--------------------|
| Current weather | `"What's the weather in Paris?"` | Agent calls `GetWeather`, returns simulated data |
| Forecast | `"5-day forecast for London"` | Agent calls `GetWeatherForecast`, returns 5-day table |
| No tool needed | `"What is the capital of France?"` | Agent answers directly without calling any tool |

#### Phase 3 — Multi-Turn Sessions

| Turn | Input | Expected behaviour |
|------|-------|--------------------|
| 1 | `"My favourite colour is blue."` | Agent acknowledges |
| 2 | `"What is my favourite colour?"` | Agent recalls blue from session history |
| 3 (new session) | `"What is my favourite colour?"` | Agent does NOT recall (different session) |

#### Phase 4 — Memory

| Scenario | Input | Expected behaviour |
|----------|-------|--------------------|
| Introduce name | `"My name is Carol."` | Agent stores `user_name = Carol` |
| Introduce hobby | `"I love painting."` | Agent stores `user_hobby = painting` |
| Later question | `"What is 10 + 5?"` | Agent addresses Carol by name in the response |
| Memory endpoint | `GET /api/sessions/{id}/memory` | Returns `{ "user_name": "Carol", "user_hobby": "painting" }` |

#### Phase 5 — Workflows

| Input | Step 1 output | Step 2 output |
|-------|--------------|--------------|
| `"hello world"` | `"HELLO WORLD"` | `"DLROW OLLEH"` |
| `"Agent Framework"` | `"AGENT FRAMEWORK"` | `"KROWEMARFTNEGAا"` |
| `"abc 123"` | `"ABC 123"` | `"321 CBA"` |

#### Phase 6 — Hosting Verification

| Check | How to verify |
|-------|--------------|
| Agent resolves from DI | `POST /api/agent/run` works (same behaviour as Phase 1) |
| Session resolves from DI | `POST /api/sessions` then `POST /api/sessions/{id}/run` works |
| OpenAPI spec loads | `GET /openapi/v1.json` returns valid JSON |

---

## 7. Use Cases

### 7.1 Conversational Customer Support

**Pattern:** Phase 3 (Sessions) + Phase 4 (Memory)

Create a session per customer interaction. The agent remembers what was discussed
earlier in the conversation (session history) and can also recall stored customer
facts (name, account type, previous issues) across separate sessions if the memory
store is made persistent.

**Example flow:**
```
User:  "My name is David and I'm on the Pro plan."
Agent: "Got it, David! How can I help you with your Pro plan today?"
User:  "My invoice is wrong this month."
Agent: "I'll look into your invoice, David. Can you share the invoice number?"
```

**Endpoint sequence:**
```
POST /api/sessions              → create session
POST /api/sessions/{id}/run     → turn 1 (introduce)
POST /api/sessions/{id}/run     → turn 2 (issue)
GET  /api/sessions/{id}/memory  → confirm David's data is stored
```

---

### 7.2 Real-Time Data Lookup with Function Calling

**Pattern:** Phase 2 (Tools)

The agent automatically decides when to call a registered tool and passes the
correct parameters. Replace the simulated `GetWeather` with a real HTTP call to
any external API (weather, stock prices, CRM lookup, inventory check, etc.).

**How to add a real tool:**
```csharp
// In Agents/AgentTools.cs
[Description("Get the current stock price for a ticker symbol.")]
public static async Task<string> GetStockPrice(
    [Description("The stock ticker symbol, e.g. MSFT")] string ticker)
{
    // call your real API here
    var price = await _stockApiClient.GetPriceAsync(ticker);
    return $"{ticker} is currently trading at ${price:F2}.";
}
```

Then register it in `Program.cs`:
```csharp
AIFunctionFactory.Create(AgentTools.GetStockPrice)
```

The agent will call it automatically whenever a user asks about stock prices.

---

### 7.3 Document Processing Pipeline

**Pattern:** Phase 5 (Workflows)

Chain multiple processing steps where the output of each step is the input of the
next. Use this for:

- **Text extraction → summarisation → translation** pipelines.
- **Validation → enrichment → storage** data pipelines.
- **Classification → routing → response generation** agent pipelines.

**Example: Document summariser:**
```
Step 1 (CleanTextExecutor)   → strip HTML, normalise whitespace
Step 2 (SummariseExecutor)   → agent condenses the text to 3 sentences
Step 3 (TranslateExecutor)   → agent translates to the target language
```

Each executor is independently testable and the whole pipeline is run via
`POST /api/workflow/run`.

---

### 7.4 Multi-Agent Orchestration

**Pattern:** Phase 6 (Hosting) + `AgentWorkflowBuilder`

Register multiple named agents in DI and compose them into a sequential or
concurrent workflow. Use this for:

- **Triage agent** decides which specialist agent handles the request.
- **Research agent** gathers facts; **Writing agent** formats them into a report.
- **Validation agent** checks the output of another agent before returning it.

```csharp
builder.AddAIAgent("research-agent",  instructions: "You are a research specialist.");
builder.AddAIAgent("writing-agent",   instructions: "You are a technical writer.");

builder.AddWorkflow("research-and-write", (sp, key) =>
{
    var research = sp.GetRequiredKeyedService<AIAgent>("research-agent");
    var writing  = sp.GetRequiredKeyedService<AIAgent>("writing-agent");
    return AgentWorkflowBuilder.BuildSequential(key, [research, writing]);
}).AddAsAIAgent();
```

---

### 7.5 Personalised Assistant

**Pattern:** Phase 4 (Memory) with a persistent backing store

Extend `UserMemoryService` to write to a database (SQL, Redis, Cosmos DB) instead
of an in-memory dictionary. Each user gets a persistent profile that survives
application restarts. The agent greets returning users by name and tailors responses
to their stored preferences.

**Suggested extensions:**
- Store `user_language` and respond in the user's preferred language.
- Store `user_timezone` and format date/time responses correctly.
- Store `user_subscription_tier` and offer tier-appropriate answers.

---

## 8. Extending the Project

### 8.1 Adding a New Tool

1. Add a static method to `Agents/AgentTools.cs`:

```csharp
[Description("Convert currency from one unit to another.")]
public static string ConvertCurrency(
    [Description("Amount to convert")] decimal amount,
    [Description("Source currency code, e.g. USD")] string from,
    [Description("Target currency code, e.g. EUR")] string to)
{
    // real implementation would call an exchange rate API
    return $"{amount} {from} = {amount * 0.92m} {to} (approximate)";
}
```

2. Register it in `Program.cs` inside the `AddAIAgent` factory:

```csharp
var tools = new[]
{
    AIFunctionFactory.Create(AgentTools.GetWeather),
    AIFunctionFactory.Create(AgentTools.GetWeatherForecast),
    AIFunctionFactory.Create(AgentTools.ConvertCurrency),  // add here
};
```

No other changes required — the agent will call it automatically when relevant.

---

### 8.2 Adding a New Workflow Step

1. Add an executor class to `Agents/Workflows/TextWorkflow.cs` (or create a new file):

```csharp
internal sealed class TrimExecutor() : Executor<string, string>("TrimExecutor")
{
    public override ValueTask<string> HandleAsync(
        string message, IWorkflowContext ctx, CancellationToken ct)
        => ValueTask.FromResult(message.Trim());
}
```

2. Wire it into the workflow in `TextWorkflow.Build()`:

```csharp
var trim = new TrimExecutor();
builder.AddEdge(uppercase, trim);
builder.AddEdge(trim, reverse).WithOutputFrom(reverse);
```

---

### 8.3 Persisting Memory to a Database

Replace the in-memory `ConcurrentDictionary` in `UserMemoryService` with database
calls. The public interface (`ExtractAndStore`, `GetSnapshot`, `BuildContextInstruction`,
`Remove`) stays the same — only the storage backend changes.

**Example with Entity Framework Core:**
```csharp
public async Task ExtractAndStoreAsync(string sessionId, string userMessage)
{
    // parse facts (existing logic)
    // then:
    await _dbContext.UserFacts.AddOrUpdateAsync(sessionId, facts);
    await _dbContext.SaveChangesAsync();
}
```

---

### 8.4 Exposing via A2A Protocol

Install the A2A hosting package:

```bash
dotnet add package Microsoft.Agents.Hosting.A2A --prerelease
```

Then add to `Program.cs`:

```csharp
builder.Services.AddA2AServer();
// ...
var app = builder.Build();
app.MapA2AServer();
app.Run();
```

Your agent is then discoverable at `GET /.well-known/agent.json` and callable
by other A2A-compatible agents.

---

## 9. Troubleshooting

### `AzureAI:Endpoint is not configured`

**Cause:** `appsettings.Development.json` still has the placeholder value or the key is missing.

**Fix:** Edit `appsettings.Development.json` and set `AzureAI:Endpoint` to your actual
Azure OpenAI or AI Foundry endpoint URL.

---

### `AuthenticationFailedException` or `CredentialUnavailableException`

**Cause:** `DefaultAzureCredential` could not find valid credentials.

**Fix:**
```bash
az login
az account set --subscription "<your-subscription>"
```

If you are running inside a container or CI pipeline, set `AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, and `AZURE_CLIENT_SECRET` environment variables for a service
principal, or assign a Managed Identity.

---

### `404 Not Found` on `/api/agent/run`

**Cause:** The application is not running, or the port is different.

**Fix:** Check the terminal output for the actual URL. The default is
`http://localhost:5122`. If HTTPS is selected, use `https://localhost:7009`.

---

### `Session '{id}' not found`

**Cause:** The session was already deleted, the app was restarted (sessions are
in-memory and lost on restart), or the session ID was copied incorrectly.

**Fix:** Create a new session with `POST /api/sessions` and use the returned ID.

---

### Agent does not call the weather tool

**Cause:** The LLM decided no tool was needed, or the question was not clearly
about weather.

**Fix:** Rephrase the question to be explicit, e.g.:
- Before: `"Paris weather"`
- After: `"What is the current weather in Paris?"`

---

### Agent does not remember the user's name (Phase 4)

**Cause:** The message must contain the exact phrase `"my name is"` for the simple
pattern extractor to pick it up.

**Fix:** Use the exact phrase:
- Works: `"My name is Alice."` / `"Hi, my name is Bob"`
- Does not work: `"Call me Alice"` / `"I'm Alice"`

To support more patterns, extend `UserMemoryService.ExtractAndStore()`.

---

### `dotnet build` fails with assembly errors

**Cause:** NuGet packages are not restored or there is a cache issue.

**Fix:**
```bash
dotnet restore
dotnet build
```

If the issue persists:
```bash
dotnet nuget locals all --clear
dotnet restore
dotnet build
```
