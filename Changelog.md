# Changelog — Microsoft Agent Framework API

All changes are listed in reverse chronological order, grouped by implementation phase.

---

## [Phase 6] — Hosting via DI Builder

**Date:** 2026-05-08
**Reference:** [Step 6: Host Your Agent](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting)

### Added

- **`Microsoft.Agents.AI.Hosting` (1.5.0-preview.260507.1)** — new NuGet package for
  `IHostApplicationBuilder` agent registration extensions.

### Changed

- **`Program.cs`**
  - Removed manual `builder.Services.AddSingleton<AIAgent>(...)`.
  - Added `builder.AddAIAgent("main-agent", factory)` using the hosting library.
  - Added `.WithInMemorySessionStore()` to enable managed session persistence at the hosting layer.
  - Added `builder.AddWorkflow("text-pipeline", ...)` to register the Phase 5 workflow.
  - Added `.AddAsAIAgent()` to expose the workflow as a resolvable named `AIAgent`.
  - Added `using Microsoft.Agents.AI.Hosting` and `using MicrosoftAgentFrameworkAPI.Agents.Workflows`.

- **`Controllers/AgentController.cs`**
  - Constructor parameter changed from `(AIAgent agent)` to
    `([FromKeyedServices("main-agent")] AIAgent agent)`.

- **`Controllers/SessionController.cs`**
  - Constructor parameter changed from `(AIAgent agent, ...)` to
    `([FromKeyedServices("main-agent")] AIAgent agent, ...)`.

- **`MicrosoftAgentFrameworkAPI.http`**
  - Added Phase 6 test requests:
    - `POST /api/agent/run` (keyed DI verification)
    - `POST /api/sessions` (keyed DI verification)
    - `GET /openapi/v1.json` (OpenAPI spec check)

---

## [Phase 5] — Workflows

**Date:** 2026-05-08
**Reference:** [Step 5: Workflows](https://learn.microsoft.com/en-us/agent-framework/get-started/workflows)

### Added

- **`Microsoft.Agents.AI.Workflows` (1.5.0)** — new NuGet package providing `WorkflowBuilder`,
  `Executor<TIn,TOut>`, `InProcessExecution`, `ExecutorCompletedEvent`, and related types.

- **`Agents/Workflows/TextWorkflow.cs`** — new file.
  - `TextWorkflow.Build()` — static factory that builds a two-step `Workflow`.
  - `UppercaseExecutor` — converts input text to uppercase using `BindAsExecutor` on a `Func<string,string>`.
  - `ReverseTextExecutor : Executor<string, string>` — reverses the uppercase string;
    overrides `HandleAsync(string, IWorkflowContext, CancellationToken)`.

- **`Controllers/WorkflowController.cs`** — new file.
  - `POST /api/workflow/run` — accepts `{ "text": "..." }`, runs `TextWorkflow`, returns
    per-step results from `ExecutorCompletedEvent` and `WorkflowOutputEvent`.

- **`MicrosoftAgentFrameworkAPI.http`**
  - Added Phase 5 test request: `POST /api/workflow/run`.

---

## [Phase 4] — Memory and Persistence

**Date:** 2026-05-08
**Reference:** [Step 4: Memory & Persistence](https://learn.microsoft.com/en-us/agent-framework/get-started/memory)

### Added

- **`Services/UserMemoryService.cs`** — new file.
  - `UserMemoryService` — singleton; stores `UserMemory` (a `Dictionary<string, string>`) per session ID.
  - `ExtractAndStore(sessionId, message)` — parses simple natural-language patterns
    ("my name is X", "I love Y") and saves the values.
  - `BuildContextInstruction(sessionId)` — returns a system instruction string
    built from stored facts (e.g., `"The user's name is Alice. Always address them by name."`).
  - `GetSnapshot(sessionId)` — returns a read-only copy of stored facts for the state endpoint.
  - `Remove(sessionId)` — clears memory when a session is deleted.

- **`UserMemory`** class (in `UserMemoryService.cs`) — holds `Dictionary<string, string> Facts`.

### Changed

- **`Controllers/SessionController.cs`**
  - Constructor now accepts `UserMemoryService memoryService` via DI.
  - `POST /api/sessions/{id}/run` — before calling `agent.RunAsync`:
    1. Calls `_memoryService.ExtractAndStore(sessionId, message)`.
    2. Calls `_memoryService.BuildContextInstruction(sessionId)`.
    3. If instruction is non-empty, prepends a `ChatMessage(ChatRole.System, ...)` to the
       message list before passing to the agent.
  - `DELETE /api/sessions/{id}` — now also calls `_memoryService.Remove(sessionId)`.
  - Added `GET /api/sessions/{id}/memory` endpoint — returns stored facts as JSON.

- **`Program.cs`**
  - Added `builder.Services.AddSingleton<UserMemoryService>()`.
  - Added `using MicrosoftAgentFrameworkAPI.Services`.

- **`MicrosoftAgentFrameworkAPI.http`**
  - Added Phase 4 test requests:
    - `POST /api/sessions/{id}/run` (introduce name + hobby)
    - `POST /api/sessions/{id}/run` (unrelated question — agent should address by name)
    - `GET /api/sessions/{id}/memory` (inspect stored facts)

---

## [Phase 3] — Multi-Turn Sessions

**Date:** 2026-05-08
**Reference:** [Step 3: Multi-Turn Conversations](https://learn.microsoft.com/en-us/agent-framework/get-started/multi-turn)

### Added

- **`Services/SessionService.cs`** — new file.
  - `SessionService` — singleton; maintains a `ConcurrentDictionary<string, AgentSession>`.
  - `CreateSessionAsync(agent)` — calls `agent.CreateSessionAsync()`, stores result, returns
    a new GUID-based session ID.
  - `GetSession(sessionId)` — returns the `AgentSession` or `null`.
  - `RemoveSession(sessionId)` — removes the entry.
  - `GetActiveSessionIds()` — returns all live session keys.

- **`Controllers/SessionController.cs`** — new file.
  - `POST /api/sessions` — create session.
  - `POST /api/sessions/{id}/run` — run agent within an existing session.
  - `GET /api/sessions` — list active session IDs.
  - `DELETE /api/sessions/{id}` — end session.

- **`MicrosoftAgentFrameworkAPI.http`**
  - Added Phase 3 test requests (create → turn 1 → turn 2 → list → delete).

### Changed

- **`Program.cs`**
  - Added `builder.Services.AddSingleton<SessionService>()`.
  - Moved `AIAgent` construction out of `AgentController` and into a DI factory
    (`builder.Services.AddSingleton<AIAgent>(...)`), so both controllers share one agent instance.

- **`Controllers/AgentController.cs`**
  - Constructor changed from self-constructing the agent to injecting `AIAgent` from DI.

---

## [Phase 2] — Add Tools

**Date:** 2026-05-08
**Reference:** [Step 2: Add Tools](https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools)

### Added

- **`Agents/AgentTools.cs`** — new file.
  - `AgentTools.GetWeather(string location)` — returns a simulated current weather string.
    Decorated with `[Description("Get the current weather for a given location.")]`.
  - `AgentTools.GetWeatherForecast(string location)` — returns a simulated 5-day forecast
    string. Decorated with `[Description("Get a 5-day weather forecast for a given location.")]`.
  - Both methods use `System.ComponentModel.Description` for automatic schema generation.
  - Weather data (random temperature, conditions) adapted from existing `WeatherForecastController` logic.

- **`MicrosoftAgentFrameworkAPI.http`**
  - Added Phase 2 test requests:
    - `POST /api/agent/run` → "What is the weather like in Amsterdam?"
    - `POST /api/agent/run` → "Give me a 5-day forecast for Tokyo."

### Changed

- **`Controllers/AgentController.cs`**
  - Agent construction updated to include `tools`:
    ```csharp
    var tools = new[]
    {
        AIFunctionFactory.Create(AgentTools.GetWeather),
        AIFunctionFactory.Create(AgentTools.GetWeatherForecast),
    };
    ```
  - Added `using Microsoft.Extensions.AI` for `AIFunctionFactory`.
  - Added `using MicrosoftAgentFrameworkAPI.Agents` for `AgentTools`.
  - Agent renamed from `"HelloAgent"` to `"WeatherAgent"`.
  - Agent instructions updated to mention tool usage.

---

## [Phase 1] — Basic Agent

**Date:** 2026-05-08
**Reference:** [Step 1: Your First Agent](https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent)

### Added

- **`Controllers/AgentController.cs`** — new file.
  - `POST /api/agent/run` — accepts `{ "message": "..." }`, returns `{ "response": "..." }`.
  - `POST /api/agent/run-stream` — streams the agent response using Server-Sent Events (`text/event-stream`).
  - `AgentRunRequest` record — request DTO.
  - Agent created using `AIProjectClient.AsAIAgent(model, instructions, name)`.

- **`MicrosoftAgentFrameworkAPI.http`**
  - Added Phase 1 test requests:
    - `POST /api/agent/run` → "What is the largest city in France?"
    - `POST /api/agent/run-stream` → "Tell me a one-sentence fun fact about the Eiffel Tower."

### Changed

- **`appsettings.json`**
  - Added `"AzureAI"` section:
    ```json
    "AzureAI": {
      "Endpoint": "",
      "DeploymentName": "gpt-4o-mini"
    }
    ```

- **`appsettings.Development.json`**
  - Added `"AzureAI"` section with placeholder endpoint:
    ```json
    "AzureAI": {
      "Endpoint": "https://<your-sandbox>.openai.azure.com/",
      "DeploymentName": "gpt-4o-mini"
    }
    ```

---

## [Baseline] — Project Template

**Date:** Pre-existing
**Description:** Standard ASP.NET Core 10.0 Web API template.

### Files (unchanged throughout implementation)

- `WeatherForecast.cs` — data model with `Date`, `TemperatureC`, `TemperatureF`, `Summary`.
- `Controllers/WeatherForecastController.cs` — `GET /weatherforecast` returns 5 random forecasts.
- `Properties/launchSettings.json` — HTTP (`5122`) and HTTPS (`7009`) launch profiles.
- `MicrosoftAgentFrameworkAPI.slnx` — solution file.
- `MicrosoftAgentFrameworkAPI.csproj.user` — user-level project settings.

### Pre-existing NuGet packages

These were present in the template before any implementation began:

```xml
<PackageReference Include="Azure.AI.Projects" Version="2.1.0-beta.1" />
<PackageReference Include="Azure.Identity" Version="1.21.0" />
<PackageReference Include="Microsoft.Agents.AI.Foundry" Version="1.5.0" />
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.2" />
```
