---
name: cqrs-handler
description: Scaffold a new CQRS Query or Command end-to-end — DTO, handler, DI registration, CQRSGateway routing. Generates the boilerplate that recurs in every feature.
---

# /cqrs-handler

Generates a complete CQRS slice in the Business layer plus the wiring needed for WebApi to dispatch it. The boilerplate is the same every time; this skill makes it one step instead of seven.

## When to invoke
- User wants to add a Query (read) or Command (write) callable from WebApi via `ICQRSClient.SendQueryAsync` / `SendCommandAsync`.
- User says "add query", "add command", "new CQRS handler", "expose <X> to the admin UI".

## Ask the user, then scaffold

Before writing code, confirm:

1. **Query or Command?** (Query = read-only; Command = mutation.)
2. **Name** in PascalCase, ending in `Query` or `Command` — e.g. `GetUserRiskHistoryQuery`, `UpdateConsentLevelCommand`.
3. **Inputs** — what fields does the caller pass? (e.g. `string UserKey`)
4. **Output** — what fields come back? (e.g. `IReadOnlyList<UserRiskScore> History`)
5. **Which existing handler class** to add this to, or create new?
   - Per-domain handlers exist: `UserQueryHandlers`, `AdminCommandHandlers`, `SimulationQueryHandlers`, `UserRiskScoreQueryHandlers`, etc. Add to the most relevant; create new only if the domain is new.

## Files to create / modify

### 1. DTO — `Business/Queries/<Domain>Queries.cs` or `Business/Commands/<Domain>Commands.cs`

```csharp
public class GetUserRiskHistoryQuery
{
    public string MessageType { get; set; } = "Query";
    public string QueryType { get; set; } = nameof(GetUserRiskHistoryQuery);
    public string UserKey { get; set; } = string.Empty;
}

public class GetUserRiskHistoryQueryResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public IReadOnlyList<UserRiskScore>? History { get; set; }
}
```

For Commands, use `MessageType = "Command"` and `CommandType = nameof(...)`.

### 2. Handler — `Business/Handlers/<Domain>QueryHandlers.cs` (or `CommandHandlers.cs`)

Add a method to an existing handler class when possible:

```csharp
public virtual async Task<GetUserRiskHistoryQueryResult> HandleAsync(GetUserRiskHistoryQuery query)
{
    try
    {
        if (string.IsNullOrWhiteSpace(query.UserKey))
            return new() { Success = false, Message = "UserKey is required" };

        var history = await _service.GetHistoryAsync(query.UserKey);
        return new() { Success = true, History = history };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in GetUserRiskHistoryQuery for {UserKey}", query.UserKey);
        return new() { Success = false, Message = $"Error: {ex.Message}" };
    }
}
```

Keep handlers `virtual` if any test will mock them.

### 3. CQRSGateway routing — `Business/Messaging/CQRSGateway.cs`

Add to the `ProcessQueryAsync` or `ProcessCommandAsync` switch (string-based, not `nameof`):

```csharp
"GetUserRiskHistoryQuery" => await HandleGetUserRiskHistoryQuery(messageJson, scope),
```

Place under a section comment matching the domain (e.g. `// SCRUM-904 — User Risk Score`).

Then add the private handler method:

```csharp
private async Task<string> HandleGetUserRiskHistoryQuery(string messageJson, IServiceScope scope)
{
    var query = JsonConvert.DeserializeObject<GetUserRiskHistoryQuery>(messageJson)!;
    var handler = scope.ServiceProvider.GetRequiredService<UserRiskScoreQueryHandlers>();
    var result = await handler.HandleAsync(query);
    return JsonConvert.SerializeObject(result);
}
```

### 4. DI registration — `Business/Services/BusinessServiceRegistration.cs`

Only if creating a **new** handler class. Add:

```csharp
services.AddScoped<UserRiskScoreQueryHandlers>();
```

If extending an existing handler class, skip this step — it's already registered.

### 5. Caller — typically Razor page in `WebApi/Pages/...`

```csharp
var query = new GetUserRiskHistoryQuery { UserKey = userKey };
var result = await _cqrsClient.SendQueryAsync<GetUserRiskHistoryQueryResult>(query);
if (result?.Success == true) { /* use result.History */ }
```

## Verification

1. `dotnet build ASPSBackend.sln -c Debug --nologo` — must be clean.
   - Ignore `MSB3027` / `MSB3021` (file lock; compilation succeeded). Real errors are `error CS####`.
2. Start ASPSBackend → it should log `Handling query: <NewQueryType>` when called.
3. If you added a new handler class, confirm `BusinessServiceRegistration` resolves it (no DI runtime errors at startup).

## Never

- Add to the switch without adding the `HandleXxx` method (causes a runtime null-route).
- Forget the `nameof(X)` on `QueryType` / `CommandType` — the switch matches against the string in the JSON envelope, so a mismatch silently routes to the unknown-query branch.
- Mix Query and Command semantics. Queries don't mutate. If you find yourself writing `await _repo.AddAsync(...)` in a handler whose name ends in `Query`, that's a Command.

## Output convention

```
Created/modified files:
  - Business/Queries/<Domain>Queries.cs  (added GetXQuery + result)
  - Business/Handlers/<Domain>QueryHandlers.cs  (added HandleAsync method)
  - Business/Messaging/CQRSGateway.cs  (added routing + HandleX method)
  - Business/Services/BusinessServiceRegistration.cs  (if new handler class)

Build: PASS / FAIL <details>
Next: caller code in WebApi/Pages/...
```
