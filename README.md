![.Net](http://img.shields.io/badge/-v10.0-008999?style=flat&logo=.net&logoColor=ffffff) ![Azure](http://img.shields.io/badge/Azure-Functions-008999?style=flat&logo=azure&logoColor=ffffff) [![build](https://github.com/anthueeccel/ticket-dfunctions-azure/actions/workflows/dotnet.yml/badge.svg)](https://github.com/anthueeccel/ticket-dfunctions-azure/actions/workflows/dotnet.yml) ![last_commit](https://img.shields.io/github/last-commit/anthueeccel/ticket-dfunctions-azure) ![license](https://img.shields.io/github/license/anthueeccel/ticket-dfunctions-azure)

## Overview

This project simulates a **support-ticket routing system** built on **Azure Durable Functions**
(.NET isolated worker, C#). It is purely a **study exercise** — there is no real email
integration. Analyst notifications and replies are simulated through HTTP endpoints, and all
state is persisted to **Azurite** (the local Azure Storage emulator) at zero cost. No real secrets are exposed.

**Project name:** TicketDurableFunctions

The orchestration exercises three core Durable Functions patterns:

| Pattern                                        | Where                          | How                                                                         |
| ---------------------------------------------- | ------------------------------ | --------------------------------------------------------------------------- |
| **Fan-out / fan-in**                           | `TicketOrchestrator` step 3a   | `Task.WhenAll` calls `NotifyAnalystActivity` in parallel for all 5 analysts |
| **Human interaction (external event + timer)** | `TicketOrchestrator` step 3c   | `CreateTimer` races against `WaitForExternalEvent<T>` via `Task.WhenAny`    |
| **Activity chaining**                          | `TicketOrchestrator` steps 1-5 | Linear chain of activities persist each state transition to Table Storage   |

## Architecture

```
POST /api/tickets                        →  CreateTicket (client / HTTP trigger)
   │                                      │
   │  ScheduleNewOrchestrationInstanceAsync
   │                                      ↓
   └────────────────────►  TicketOrchestrator (orchestrator trigger)
                           │
                           ├─► SaveTicketActivity         (Activity — Table Storage)
                           │
                           ├─► [ROUND 1]  Fan-out: NotifyAnalystActivity × 5
                           │             (parallel via Task.WhenAll)
                           ├─► Timer (10 min)
                           ├─► WaitForExternalEvent<bool> × 5  (one per analyst)
                           │             (race with timer via Task.WhenAny)
                           ├─► NotifyManagerActivity (if round fails)
                           │
                           ├─► [ROUND 2]  (same pattern, 5 min timer)
                           │
                           ├─► AssignOwnerActivity (if analyst accepts)
                           ├─► CalculateFinalPriceActivity
                           │
                           └─► CloseTicketActivity  (or end — skip pricing)

POST /api/tickets/{id}/reply/{analystId} →  AnalystReply (client / HTTP trigger)
   (raises AnalystReply-{analystId} event) ─────────────────────────────────┘

GET /api/tickets… (read path)  →  see "Reading ticket data" chart below
```

### Reading ticket data (GET endpoints)

The read path is separate from the orchestration: plain HTTP triggers query the `Tickets`
table directly (no orchestration involved).

```
GET /api/tickets[?status=&closureReason=&skip=&top=]      GET /api/tickets/{ticketId}
        │                                                          │
        ▼                                                          ▼
  GetTickets (HTTP trigger)                                GetTicketById (HTTP trigger)
        │                                                          │
        │  parse + validate query params                           │  no filters
        ▼                                                          ▼
        └──────────────►  TableStorageService  ◄───────────────────┘
                                 │
         ┌───────────────────────┴────────────────────────┐
         ▼                                                ▼
 QueryAsync<TableEntity>                        GetEntityAsync(PartitionKey, RowKey)
 filter: PartitionKey == "Ticket"               (PartitionKey = "Ticket",
         │                                       RowKey = {ticketId})
         ▼                                                │
 in-memory post-filtering:                                │
   status, closureReason (case-insensitive)               │
   then Skip(skip).Take(top)                              │
         │                                                │
         └────────────────►  Tickets table (Azurite)  ◄───┘

 200 ← JSON array of tickets                    200 ← one ticket as JSON
 400 ← invalid status / closureReason /         404 ← ticket not found
        skip / top
```

| Endpoint                      | Query params                                                                                                      | Responses                                                             |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `GET /api/tickets`            | `status` (case-insensitive), `closureReason` (case-insensitive), `skip` (default 0), `top` (default 100, max 200) | `200` JSON array (possibly empty) · `400` invalid filter/paging value |
| `GET /api/tickets/{ticketId}` | —                                                                                                                 | `200` single ticket JSON · `404` not found                            |

Example calls:

```bash
# All tickets (first page)
curl "http://localhost:7071/api/tickets"

# Closed tickets that ended without an owner
curl "http://localhost:7071/api/tickets?status=Closed&closureReason=Unassigned"

# Paging: second page of 25
curl "http://localhost:7071/api/tickets?skip=25&top=25"

# A single ticket by its TicketId (the one stored in the Tickets table, not
# the orchestration instance ID from the create response)
curl "http://localhost:7071/api/tickets/<ticketId>"
```

## Project structure

```
TicketDurableFunctions/
  Functions/
    CreateTicket.cs          HTTP trigger — starts orchestration (client pattern)
    TicketOrchestrator.cs    Orchestrator trigger — routing workflow
    AnalystReply.cs          HTTP trigger — simulates analyst reply via external event
    Activities/
      SaveTicketActivity.cs         Persists a ticket row to Table Storage
      NotifyAnalystActivity.cs      Simulates sending a notification to one analyst
      NotifyManagerActivity.cs      Simulates escalating to a manager (sets status Escalated)
      SetTicketStatusActivity.cs    Sets a ticket's status (used to mark WaitingForAnalyst each round)
      AssignOwnerActivity.cs        Assigns an analyst; simulates HoursSpent (random 1-8)
      CalculateFinalPriceActivity.cs Computes FinalPrice per PricingTable rules
      CloseTicketActivity.cs        Marks a ticket Closed (with a ClosureReason: Completed or Unassigned)
  Models/
    Ticket.cs, Analyst.cs, TicketInput.cs, AnalystReplyRequest.cs
    Enums: TicketCategory, TicketPriority, TicketStatus
  Data/
    SampleAnalysts.cs  Static list of 5 fake analysts (A1-A5)
    PricingTable.cs    Base price / priority multiplier / hourly rate rules
  Services/
    TableStorageService.cs  Wraps Azure.Data.Tables for the Tickets table
  Program.cs
  host.json
  local.settings.json
  run-functions.bat      One-click local launcher (build + start host on :7071)
  TicketDurableFunctions.csproj
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (v4+)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)

No Azure subscription or resources needed for local development.

## Running locally

### 1. Start Azurite (local storage emulator)

**Option A — VS Code Azurite extension** (recommended):

1. Install the **Azurite** extension in VS Code.
2. Open the Command Palette (`Ctrl+Shift+P`) → **Azurite: Start**.

**Option B — CLI**:

```bash
azurite
```

Azurite listens on:

- Table: `http://127.0.0.1:10002`
- Queue: `http://127.0.0.1:10001`
- Blob: `http://127.0.0.1:10000`

### 2. Start the Function App

**Option A — `run-functions.bat` (easiest on Windows):**

Double-click `run-functions.bat` (or run it from a terminal in the project root). It:

1. Kills any stale `func.exe` host from a previous run
2. Builds the project (`dotnet build`) and aborts with a clear message if the build fails
3. Starts the Functions host on `http://localhost:7071`

Live logs stream to the console window; press `Ctrl+C` (or close the window) to stop.

> **Note:** Azurite must already be running before you launch the script (see step 1 above).

**Option B — VS Code Azure Functions extension:**

1. Open the project folder in VS Code.
2. Open the Command Palette → **Azure Functions: Start and Attach** (this also attaches the debugger).

**Option C — CLI:**

```bash
func start
```

> Tip: for .NET isolated projects, `dotnet run` is also supported and builds first — handy when `func start` is run from the repo root.

The app listens on `http://localhost:7071`. On startup you should see
`Worker process started and initialized.` followed by `9 functions found (Worker)` and
`Job host started`.

### Troubleshooting: "Failed to start language worker" / "0 functions found"

The .NET isolated worker **requires** an explicit `HostBuilder` in `Program.cs`:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
```

If `Main()` is empty (or `host.Run()` is missing), the worker process starts and exits
immediately. The host retries, gives up, and reports:

```
Exceeded language worker restart retry count for runtime:dotnet-isolated
Failed to start language worker process for runtime: dotnet-isolated
0 functions found (Custom)
```

If you see these messages, make sure `Program.cs` still boots the host as shown above.

### 3. Create a ticket

```bash
curl -X POST http://localhost:7071/api/tickets \
  -H "Content-Type: application/json" \
  -d '{"title":"Laptop won'\''t boot","description":"Black screen on power","category":"Hardware","priority":"High"}'
```

**Response** (HTTP 202) includes the `id` (orchestration instance ID) and `statusQueryGetUri`:

```json
{
  "id": "...",
  "statusQueryGetUri": "http://localhost:7071/runtime/webhooks/durabletask/orchestrators/.../instances/...",
  "sendEventPostUri": "...",
  ...
}
```

### 4. Poll orchestration status

```bash
curl "<statusQueryGetUri-from-the-response-above>"
```

You'll see the orchestration go through replay cycles as it waits for external events.

### 5. Simulate an analyst accepting the ticket

```bash
curl -X POST "http://localhost:7071/api/tickets/{instanceId}/reply/A2" \
  -H "Content-Type: application/json" \
  -d '{"accepted":true}'
```

The orchestration wakes up, calls `AssignOwnerActivity`, calculates the final price, and closes the ticket.

### 6. Simulate all analysts declining (escalation flow)

```bash
# Round 1: all decline → manager escalation → round 2 (5 min timer)
curl -X POST "http://localhost:7071/api/tickets/{instanceId}/reply/A1" -H "Content-Type: application/json" -d '{"accepted":false}'
curl -X POST "http://localhost:7071/api/tickets/{instanceId}/reply/A2" -H "Content-Type: application/json" -d '{"accepted":false}'
curl -X POST "http://localhost:7071/api/tickets/{instanceId}/reply/A3" -H "Content-Type: application/json" -d '{"accepted":false}'
curl -X POST "http://localhost:7071/api/tickets/{instanceId}/reply/A4" -H "Content-Type: application/json" -d '{"accepted":false}'
curl -X POST "http://localhost:7071/api/tickets/{instanceId}/reply/A5" -H "Content-Type: application/json" -d '{"accepted":false}'
```

If no analyst accepts in round 2, the ticket closes with status `Closed` and `ClosureReason = Unassigned`
(pricing skipped). A normally completed ticket closes with `ClosureReason = Completed`, so the two
terminal outcomes are distinguishable in Table Storage even though both share the `Closed` status.

## Pricing formula

```
FinalPrice = (basePrice × priorityMultiplier) + (hoursSpent × hourlyRate)
```

| Category | Base Price | Priority | Multiplier |
| -------- | ---------- | -------- | ---------- |
| Hardware | 50         | Low      | 1.0        |
| Software | 30         | Medium   | 1.3        |
| Network  | 70         | High     | 1.6        |
| Access   | 20         | Critical | 2.0        |
| Other    | 40         |          |            |

- **Hourly rate**: 25 / hour
- **HoursSpent**: simulated randomly (1–8, 1 decimal place) by `AssignOwnerActivity` — this stands in for a real "log hours" step.

## Deployment (Consumption plan)

This section is a **guide only** — deployment has not been executed. After the local version works and you're ready to deploy to Azure:

1. **Create a Storage Account** — required by Durable Functions regardless of plan. Choose a small/standard one; the cost is near-zero for this study project.

2. **Create a Function App on the Consumption (Y1) plan** — the cheapest option:
   - Pay-per-execution (billed by the second, only when code runs).
   - Includes a generous free monthly grant.
   - Scales to zero when idle (no always-on cost).
   - **Avoid the Premium plan** — it has a minimum per-hour cost even when idle.

3. **Deploy**:

   ```bash
   func azure functionapp publish <app-name>
   ```

   Or via the VS Code Azure Functions extension.

4. Once deployed, the same HTTP endpoints will be live at `https://<app-name>.azurewebsites.net/api/tickets`.

## Technology stack

- .NET 10, Azure Functions **isolated worker** model
- `Microsoft.Azure.Functions.Worker` 2.52.0
- `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` 1.19.0
- `Azure.Data.Tables` 12.12.0
- Azurite for local storage emulation
