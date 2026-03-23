# ASPS-352: Admin Device Alert Simulator - Design Document

## 📋 Overview
כלי admin המאפשר יצירת סימולציות של device alerts למטרות testing, demo, ו-QA.

### Goals
1. ✅ יצירה וניהול של סימולציות alert
2. ✅ הפעלה של סימולציות עם delays מוגדרים
3. ✅ תמיכה בכל סוגי ה-alerts הקיימים
4. ✅ UI ידידותי עם drag&drop
5. ✅ אינטגרציה מלאה עם CQRS architecture

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Razor Pages UI                           │
│  ┌───────────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Simulations   │  │ Create/Edit  │  │ Step Editor     │  │
│  │ List          │  │ Simulation   │  │ (Modal Dialog)  │  │
│  └───────┬───────┘  └──────┬───────┘  └────────┬────────┘  │
│          │                 │                    │           │
└──────────┼─────────────────┼────────────────────┼───────────┘
           │                 │                    │
           ▼                 ▼                    ▼
┌─────────────────────────────────────────────────────────────┐
│                      CQRS Layer                             │
│  ┌──────────────────────────┐  ┌────────────────────────┐  │
│  │ Commands                 │  │ Queries                │  │
│  │ • CreateSimulation       │  │ • GetSimulations       │  │
│  │ • UpdateSimulation       │  │ • GetSimulationById    │  │
│  │ • DeleteSimulation       │  │ • GetSimulationSteps   │  │
│  │ • RunSimulation          │  │                        │  │
│  └────────────┬─────────────┘  └────────┬───────────────┘  │
│               │                         │                  │
└───────────────┼─────────────────────────┼──────────────────┘
                │                         │
                ▼                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Business Layer                            │
│  ┌─────────────────────────────────────────────────────┐   │
│  │         Simulation Runner Service                   │   │
│  │  1. Load Simulation Steps                           │   │
│  │  2. For each step:                                  │   │
│  │     - Wait for Delay                                │   │
│  │     - Build Alert DTO                               │   │
│  │     - Send to AlertsController                      │   │
│  │  3. Track execution status                          │   │
│  └─────────────────────────────────────────────────────┘   │
│                           │                                 │
└───────────────────────────┼─────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                 Existing Alert Pipeline                     │
│     AlertsController → RealTimeAlertListener → Actors       │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 Database Schema

### SimulationEntity

```csharp
public class SimulationEntity : Entity
{
    // Basic Info
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Steps (JSON serialized)
    [Column(TypeName = "text")]
    public string StepsJson { get; set; } = "[]";
    
    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty; // Admin username
    
    // Status
    public SimulationStatus Status { get; set; } = SimulationStatus.Draft;
    public int TotalSteps { get; set; }
    
    // Computed property
    [NotMapped]
    public List<SimulationStep> Steps
    {
        get => JsonSerializer.Deserialize<List<SimulationStep>>(StepsJson) 
               ?? new List<SimulationStep>();
        set => StepsJson = JsonSerializer.Serialize(value);
    }
}

public enum SimulationStatus
{
    Draft,
    Ready,
    Running,
    Completed,
    Failed
}
```

### SimulationStep (JSON Model - not a table)

```csharp
public class SimulationStep
{
    public int Order { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty; // RemoteAccess, Url, TrackUrl
    public Dictionary<string, object> AlertData { get; set; } = new();
    public int DelaySeconds { get; set; }
    public DelayUnit DelayUnit { get; set; } = DelayUnit.Seconds;
    
    // Computed
    public int TotalDelaySeconds => DelayUnit switch
    {
        DelayUnit.Seconds => DelaySeconds,
        DelayUnit.Minutes => DelaySeconds * 60,
        DelayUnit.Hours => DelaySeconds * 3600,
        DelayUnit.Days => DelaySeconds * 86400,
        _ => DelaySeconds
    };
}

public enum DelayUnit
{
    Seconds,
    Minutes,
    Hours,
    Days
}
```

### Migration SQL

```sql
CREATE TABLE IF NOT EXISTS `SimulationEntity` (
    `KeyField` varchar(36) NOT NULL,
    `AggregateVersionField` int NOT NULL DEFAULT 0,
    `Name` varchar(255) NOT NULL,
    `Description` text,
    `StepsJson` longtext NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `LastRunAt` datetime(6) NULL,
    `CreatedBy` varchar(100) NOT NULL,
    `Status` int NOT NULL DEFAULT 0,
    `TotalSteps` int NOT NULL DEFAULT 0,
    `Discriminator` varchar(50) NOT NULL DEFAULT 'SimulationEntity',
    PRIMARY KEY (`KeyField`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX IX_SimulationEntity_Status ON SimulationEntity(Status);
CREATE INDEX IX_SimulationEntity_CreatedAt ON SimulationEntity(CreatedAt);
```

---

## 🎯 CQRS Commands & Queries

### Commands

#### 1. CreateSimulationCommand
```csharp
public class CreateSimulationCommand : Command
{
    public string CommandType { get; set; } = nameof(CreateSimulationCommand);
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SimulationStep> Steps { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateSimulationCommandResult : CommandResult
{
    public Key? SimulationKey { get; set; }
    public string SimulationId { get; set; } = string.Empty;
}
```

#### 2. UpdateSimulationCommand
```csharp
public class UpdateSimulationCommand : Command
{
    public string CommandType { get; set; } = nameof(UpdateSimulationCommand);
    public string SimulationId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<SimulationStep>? Steps { get; set; }
}

public class UpdateSimulationCommandResult : CommandResult
{
    public bool Updated { get; set; }
}
```

#### 3. DeleteSimulationCommand
```csharp
public class DeleteSimulationCommand : Command
{
    public string CommandType { get; set; } = nameof(DeleteSimulationCommand);
    public string SimulationId { get; set; } = string.Empty;
}

public class DeleteSimulationCommandResult : CommandResult
{
    public bool Deleted { get; set; }
}
```

#### 4. RunSimulationCommand
```csharp
public class RunSimulationCommand : Command
{
    public string CommandType { get; set; } = nameof(RunSimulationCommand);
    public string SimulationId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = false; // For testing without sending alerts
}

public class RunSimulationCommandResult : CommandResult
{
    public bool Started { get; set; }
    public string ExecutionId { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public DateTime EstimatedCompletion { get; set; }
}
```

### Queries

#### 1. GetSimulationsQuery
```csharp
public class GetSimulationsQuery : Query
{
    public string QueryType { get; set; } = nameof(GetSimulationsQuery);
    public string? Search { get; set; }
    public SimulationStatus? Status { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public bool Descending { get; set; } = true;
}

public class GetSimulationsQueryResult : QueryResult
{
    public List<SimulationListItem> Simulations { get; set; } = new();
}

public class SimulationListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SimulationStatus Status { get; set; }
    public int TotalSteps { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
```

#### 2. GetSimulationByIdQuery
```csharp
public class GetSimulationByIdQuery : Query
{
    public string QueryType { get; set; } = nameof(GetSimulationByIdQuery);
    public string SimulationId { get; set; } = string.Empty;
}

public class GetSimulationByIdQueryResult : QueryResult
{
    public SimulationEntity? Simulation { get; set; }
}
```

#### 3. GetUsersForSimulationQuery
```csharp
public class GetUsersForSimulationQuery : Query
{
    public string QueryType { get; set; } = nameof(GetUsersForSimulationQuery);
    public string? Search { get; set; }
}

public class GetUsersForSimulationQueryResult : QueryResult
{
    public List<UserSimulationOption> Users { get; set; } = new();
}

public class UserSimulationOption
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int DeviceCount { get; set; }
}
```

#### 4. GetDevicesForUserQuery
```csharp
public class GetDevicesForUserQuery : Query
{
    public string QueryType { get; set; } = nameof(GetDevicesForUserQuery);
    public string UserId { get; set; } = string.Empty;
}

public class GetDevicesForUserQueryResult : QueryResult
{
    public List<DeviceSimulationOption> Devices { get; set; } = new();
}

public class DeviceSimulationOption
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceUid { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
}
```

---

## ⚙️ Simulation Runner Service

### SimulationRunnerService

```csharp
public class SimulationRunnerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SimulationRunnerService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningSimulations;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Listen for RunSimulationCommand events
        // Execute simulations in background
    }
    
    public async Task<RunSimulationCommandResult> RunSimulationAsync(
        string simulationId, 
        bool dryRun, 
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();
        
        // 1. Load simulation
        var simulation = await dbContext.Set<SimulationEntity>()
            .FirstOrDefaultAsync(s => s.KeyField == simulationId, cancellationToken);
            
        if (simulation == null)
            return new RunSimulationCommandResult { Success = false, Message = "Simulation not found" };
        
        // 2. Update status
        simulation.Status = SimulationStatus.Running;
        simulation.LastRunAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        
        var executionId = Guid.NewGuid().ToString();
        _runningSimulations[executionId] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        try
        {
            // 3. Execute steps
            var steps = simulation.Steps.OrderBy(s => s.Order).ToList();
            
            foreach (var step in steps)
            {
                // 3.1 Wait for delay
                if (step.TotalDelaySeconds > 0)
                {
                    _logger.LogInformation(
                        "Simulation {SimulationId} Step {Order}: Waiting {Seconds}s",
                        simulationId, step.Order, step.TotalDelaySeconds);
                        
                    await Task.Delay(
                        TimeSpan.FromSeconds(step.TotalDelaySeconds), 
                        cancellationToken);
                }
                
                // 3.2 Send alert
                if (!dryRun)
                {
                    await SendAlertAsync(step, httpClient, cancellationToken);
                }
                else
                {
                    _logger.LogInformation(
                        "Simulation {SimulationId} Step {Order}: DRY RUN - {AlertType}",
                        simulationId, step.Order, step.AlertType);
                }
            }
            
            // 4. Mark completed
            simulation.Status = SimulationStatus.Completed;
            await dbContext.SaveChangesAsync(cancellationToken);
            
            return new RunSimulationCommandResult 
            { 
                Success = true, 
                Started = true,
                ExecutionId = executionId,
                TotalSteps = steps.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation {SimulationId} failed", simulationId);
            simulation.Status = SimulationStatus.Failed;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            
            return new RunSimulationCommandResult 
            { 
                Success = false, 
                Message = ex.Message 
            };
        }
        finally
        {
            _runningSimulations.TryRemove(executionId, out _);
        }
    }
    
    private async Task SendAlertAsync(
        SimulationStep step, 
        HttpClient httpClient, 
        CancellationToken cancellationToken)
    {
        // Build alert DTO based on AlertType
        object alertDto = step.AlertType switch
        {
            "TrackUrl" => BuildTrackUrlAlertDto(step),
            "RemoteAccess" => BuildRemoteAccessAlertDto(step),
            "Url" => BuildUrlAlertDto(step),
            _ => throw new NotSupportedException($"Alert type {step.AlertType} not supported")
        };
        
        // Send to AlertsController
        var endpoint = step.AlertType.ToLower() switch
        {
            "trackurl" => "/api/alerts/trackurl",
            "remoteaccess" => "/api/alerts/remoteaccess",
            "url" => "/api/alerts/url",
            _ => throw new NotSupportedException($"No endpoint for {step.AlertType}")
        };
        
        var json = JsonSerializer.Serialize(alertDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to send alert: {Status} - {Error}", 
                response.StatusCode, error);
            throw new Exception($"Alert submission failed: {response.StatusCode}");
        }
        
        _logger.LogInformation(
            "Alert sent successfully: {AlertType} for device {DeviceId}",
            step.AlertType, step.DeviceId);
    }
    
    private TrackUrlAlertDto BuildTrackUrlAlertDto(SimulationStep step)
    {
        return new TrackUrlAlertDto
        {
            DeviceUid = GetValueOrDefault<string>(step.AlertData, "DeviceUid", ""),
            Url = GetValueOrDefault<string>(step.AlertData, "Url", ""),
            Duration = GetValueOrDefault<int>(step.AlertData, "Duration", 0),
            Priority = GetValueOrDefault<Priority>(step.AlertData, "Priority", Priority.Medium),
            Timestamp = DateTime.UtcNow
        };
    }
    
    private T GetValueOrDefault<T>(Dictionary<string, object> data, string key, T defaultValue)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }
        return defaultValue;
    }
}
```

---

## 🎨 Razor Pages Structure

### 1. Simulations/Index.cshtml

```
Pages/
  Simulations/
    Index.cshtml          # List view
    Index.cshtml.cs       # Page model
    CreateEdit.cshtml     # Create/Edit form
    CreateEdit.cshtml.cs  # Page model
    _StepEditorModal.cshtml  # Partial for step editing
```

#### Index.cshtml Features:
- **Table with columns:**
  - Name
  - Description
  - Status badge
  - Total Steps
  - Created At
  - Last Run
  - Actions (Edit/Delete/Run)

- **Filters:**
  - Search by name
  - Filter by status (Draft/Ready/Completed)
  - Sort by: Name, Created Date, Last Run

- **Actions:**
  - Create New Simulation button
  - Edit button → CreateEdit page
  - Delete button with confirmation
  - Run button → shows progress modal

### 2. Simulations/CreateEdit.cshtml

```cshtml
@page "{id?}"
@model WebApi.Pages.Simulations.CreateEditModel

<div class="container-fluid">
    <h2>@(Model.IsEdit ? "Edit" : "Create") Simulation</h2>
    
    <form method="post">
        <!-- Basic Info -->
        <div class="card mb-3">
            <div class="card-body">
                <div class="mb-3">
                    <label>Name</label>
                    <input asp-for="Name" class="form-control" required />
                </div>
                <div class="mb-3">
                    <label>Description</label>
                    <textarea asp-for="Description" class="form-control" rows="3"></textarea>
                </div>
            </div>
        </div>
        
        <!-- Steps List -->
        <div class="card mb-3">
            <div class="card-header d-flex justify-content-between">
                <h5>Simulation Steps</h5>
                <button type="button" class="btn btn-sm btn-success" 
                        onclick="openStepEditor(-1)">
                    <i class="fas fa-plus"></i> Add Step
                </button>
            </div>
            <div class="card-body">
                <div id="steps-container" class="sortable-list">
                    <!-- Steps will be rendered here with drag handles -->
                </div>
            </div>
        </div>
        
        <button type="submit" class="btn btn-primary">Save Simulation</button>
        <a asp-page="Index" class="btn btn-secondary">Cancel</a>
    </form>
</div>

<!-- Step Editor Modal -->
@await Html.PartialAsync("_StepEditorModal")

@section Scripts {
    <script src="~/js/simulation-editor.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/sortablejs@latest/Sortable.min.js"></script>
}
```

### 3. _StepEditorModal.cshtml (Partial)

```cshtml
<div class="modal fade" id="stepEditorModal" tabindex="-1">
    <div class="modal-dialog modal-lg">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Edit Step</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <input type="hidden" id="step-index" />
                
                <!-- User Selection -->
                <div class="mb-3">
                    <label>User</label>
                    <select id="step-user" class="form-select" onchange="loadDevices()">
                        <option value="">Select User...</option>
                    </select>
                </div>
                
                <!-- Device Selection -->
                <div class="mb-3">
                    <label>Device</label>
                    <select id="step-device" class="form-select" onchange="loadDeviceInfo()">
                        <option value="">Select Device...</option>
                    </select>
                </div>
                
                <!-- Alert Type -->
                <div class="mb-3">
                    <label>Alert Type</label>
                    <select id="step-alert-type" class="form-select" onchange="loadAlertFields()">
                        <option value="">Select Alert Type...</option>
                        <option value="TrackUrl">Track URL</option>
                        <option value="RemoteAccess">Remote Access</option>
                        <option value="Url">URL Alert</option>
                    </select>
                </div>
                
                <!-- Dynamic Alert Fields -->
                <div id="alert-fields-container">
                    <!-- Dynamic fields based on alert type -->
                </div>
                
                <!-- Delay -->
                <div class="row">
                    <div class="col-md-6">
                        <label>Delay</label>
                        <input type="number" id="step-delay" class="form-control" min="0" value="0" />
                    </div>
                    <div class="col-md-6">
                        <label>Unit</label>
                        <select id="step-delay-unit" class="form-select">
                            <option value="Seconds">Seconds</option>
                            <option value="Minutes">Minutes</option>
                            <option value="Hours">Hours</option>
                            <option value="Days">Days</option>
                        </select>
                    </div>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                <button type="button" class="btn btn-primary" onclick="saveStep()">Save Step</button>
            </div>
        </div>
    </div>
</div>
```

### 4. JavaScript - simulation-editor.js

```javascript
let steps = [];

// Load users for autocomplete
async function loadUsers(search = '') {
    const response = await fetch(`/api/simulations/users?search=${search}`);
    const data = await response.json();
    
    const select = document.getElementById('step-user');
    select.innerHTML = '<option value="">Select User...</option>';
    
    data.users.forEach(user => {
        const option = document.createElement('option');
        option.value = user.userId;
        option.textContent = `${user.fullName} (${user.deviceCount} devices)`;
        select.appendChild(option);
    });
}

// Load devices for selected user
async function loadDevices() {
    const userId = document.getElementById('step-user').value;
    if (!userId) return;
    
    const response = await fetch(`/api/simulations/devices?userId=${userId}`);
    const data = await response.json();
    
    const select = document.getElementById('step-device');
    select.innerHTML = '<option value="">Select Device...</option>';
    
    data.devices.forEach(device => {
        const option = document.createElement('option');
        option.value = device.deviceId;
        option.textContent = `${device.deviceType} - ${device.operatingSystem}`;
        select.appendChild(option);
    });
}

// Load dynamic fields based on alert type
function loadAlertFields() {
    const alertType = document.getElementById('step-alert-type').value;
    const container = document.getElementById('alert-fields-container');
    
    if (alertType === 'TrackUrl') {
        container.innerHTML = `
            <div class="mb-3">
                <label>URL</label>
                <input type="url" id="alert-url" class="form-control" required />
            </div>
            <div class="mb-3">
                <label>Duration (seconds)</label>
                <input type="number" id="alert-duration" class="form-control" value="0" />
            </div>
            <div class="mb-3">
                <label>Priority</label>
                <select id="alert-priority" class="form-select">
                    <option value="Low">Low</option>
                    <option value="Medium" selected>Medium</option>
                    <option value="High">High</option>
                    <option value="Critical">Critical</option>
                </select>
            </div>
        `;
    } else if (alertType === 'RemoteAccess') {
        container.innerHTML = `
            <div class="mb-3">
                <label>Remote Access App</label>
                <select id="alert-app" class="form-select">
                    <option value="TeamViewer">TeamViewer</option>
                    <option value="AnyDesk">AnyDesk</option>
                    <option value="Chrome Remote Desktop">Chrome Remote Desktop</option>
                </select>
            </div>
            <div class="mb-3">
                <label>Connection URL</label>
                <input type="text" id="alert-connection-url" class="form-control" />
            </div>
        `;
    }
}

// Save step
function saveStep() {
    const stepIndex = document.getElementById('step-index').value;
    const step = {
        order: stepIndex === '-1' ? steps.length : parseInt(stepIndex),
        userId: document.getElementById('step-user').value,
        deviceId: document.getElementById('step-device').value,
        alertType: document.getElementById('step-alert-type').value,
        delaySeconds: parseInt(document.getElementById('step-delay').value),
        delayUnit: document.getElementById('step-delay-unit').value,
        alertData: collectAlertData()
    };
    
    if (stepIndex === '-1') {
        steps.push(step);
    } else {
        steps[stepIndex] = step;
    }
    
    renderSteps();
    bootstrap.Modal.getInstance(document.getElementById('stepEditorModal')).hide();
}

// Collect alert-specific data
function collectAlertData() {
    const alertType = document.getElementById('step-alert-type').value;
    const data = {};
    
    if (alertType === 'TrackUrl') {
        data.Url = document.getElementById('alert-url').value;
        data.Duration = parseInt(document.getElementById('alert-duration').value);
        data.Priority = document.getElementById('alert-priority').value;
    } else if (alertType === 'RemoteAccess') {
        data.RemoteAccessApp = document.getElementById('alert-app').value;
        data.ConnectionUrl = document.getElementById('alert-connection-url').value;
    }
    
    return data;
}

// Render steps list with drag handles
function renderSteps() {
    const container = document.getElementById('steps-container');
    container.innerHTML = '';
    
    steps.forEach((step, index) => {
        const stepDiv = document.createElement('div');
        stepDiv.className = 'step-item card mb-2';
        stepDiv.innerHTML = `
            <div class="card-body d-flex justify-content-between align-items-center">
                <div class="d-flex align-items-center">
                    <i class="fas fa-grip-vertical drag-handle me-3"></i>
                    <div>
                        <strong>Step ${index + 1}:</strong> ${step.alertType}
                        <br>
                        <small class="text-muted">
                            Delay: ${step.delaySeconds} ${step.delayUnit}
                        </small>
                    </div>
                </div>
                <div>
                    <button type="button" class="btn btn-sm btn-info" 
                            onclick="editStep(${index})">Edit</button>
                    <button type="button" class="btn btn-sm btn-danger" 
                            onclick="deleteStep(${index})">Delete</button>
                </div>
            </div>
        `;
        container.appendChild(stepDiv);
    });
    
    // Enable drag & drop
    new Sortable(container, {
        handle: '.drag-handle',
        animation: 150,
        onEnd: function(evt) {
            const item = steps.splice(evt.oldIndex, 1)[0];
            steps.splice(evt.newIndex, 0, item);
            steps.forEach((s, i) => s.order = i);
            renderSteps();
        }
    });
}

// Initialize
document.addEventListener('DOMContentLoaded', function() {
    loadUsers();
});
```

---

## 🔄 Integration Points

### 1. AlertsController
- התוסף של `/api/simulations/*` endpoints לא נדרש
- נשתמש ב-endpoints הקיימים:
  - `POST /api/alerts/trackurl`
  - `POST /api/alerts/remoteaccess` (if exists)
  - `POST /api/alerts/url` (if exists)

### 2. Admin Navigation
הוסף ל-`_Layout.cshtml` (Admin section):
```html
<li class="nav-item">
    <a class="nav-link" asp-page="/Simulations/Index">
        <i class="fas fa-flask"></i> Alert Simulator
    </a>
</li>
```

### 3. Permissions
- רק admin users יכולים לגשת ל-Simulations pages
- הוסף authorize attribute:
```csharp
[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
```

---

## 📝 Implementation Tasks

### Phase 1: Database & Models
- [ ] Create `SimulationEntity.cs` in `Common/Entities/`
- [ ] Create `SimulationStep.cs` model in `Common/Models/`
- [ ] Create migration SQL script
- [ ] Add DbSet to ApplicationDbContext
- [ ] Run migration

### Phase 2: CQRS Layer
- [ ] Create `SimulationCommands.cs` in `Business/Commands/`
- [ ] Create `SimulationQueries.cs` in `Business/Queries/`
- [ ] Create `SimulationCommandHandlers.cs` in `Business/Handlers/`
- [ ] Create `SimulationQueryHandlers.cs` in `Business/Handlers/`
- [ ] Register handlers in DI

### Phase 3: Simulation Runner
- [ ] Create `SimulationRunnerService.cs` in `Business/Services/`
- [ ] Implement alert DTO builders for each type
- [ ] Add background service registration
- [ ] Add logging and error handling

### Phase 4: UI - Razor Pages
- [ ] Create `Pages/Simulations/` folder
- [ ] Create `Index.cshtml` + `Index.cshtml.cs`
- [ ] Create `CreateEdit.cshtml` + `CreateEdit.cshtml.cs`
- [ ] Create `_StepEditorModal.cshtml` partial
- [ ] Create `wwwroot/js/simulation-editor.js`
- [ ] Add navigation link to admin layout

### Phase 5: API Endpoints (Support)
- [ ] Create `SimulationsController.cs` for AJAX calls:
  - `GET /api/simulations/users`
  - `GET /api/simulations/devices?userId={id}`
  - `GET /api/simulations/{id}/status` (for live progress)

### Phase 6: Testing
- [ ] Unit tests for SimulationRunnerService
- [ ] Unit tests for command/query handlers
- [ ] Integration test: Create → Run → Verify alerts
- [ ] UI testing with Selenium/Playwright

### Phase 7: Documentation
- [ ] Update admin documentation
- [ ] Create user guide with screenshots
- [ ] Add API documentation
- [ ] Create demo video

---

## 🧪 Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task SimulationRunner_ExecutesStepsInOrder()
{
    // Arrange
    var simulation = CreateTestSimulation();
    var runner = new SimulationRunnerService(...);
    
    // Act
    var result = await runner.RunSimulationAsync(simulation.KeyField, false, CancellationToken.None);
    
    // Assert
    Assert.True(result.Success);
    Assert.Equal(3, result.TotalSteps);
}

[Fact]
public async Task SimulationRunner_RespectsDelays()
{
    // Test that delays are applied correctly
}

[Fact]
public async Task SimulationRunner_HandlesFailures()
{
    // Test error handling when alert submission fails
}
```

### Integration Tests
```csharp
[Fact]
public async Task EndToEnd_CreateSimulation_RunIt_VerifyAlerts()
{
    // 1. Create simulation via command
    // 2. Run simulation
    // 3. Wait for completion
    // 4. Verify alerts were created in DB
}
```

---

## 🚀 Future Enhancements

### V2 Features:
1. **Scheduling** - Run simulations at specific times
2. **Templates** - Save common simulation patterns
3. **Variables** - Use placeholders like `{randomUrl}`, `{timestamp}`
4. **Bulk Operations** - Create multiple simulations from CSV
5. **Analytics** - Track which simulations are most used
6. **Webhooks** - Notify external systems when simulation completes
7. **API Mode** - Run simulations programmatically
8. **Live Progress** - Real-time updates via SignalR

---

## ⚠️ Security Considerations

1. **Authorization**
   - Only admin users can access simulation features
   - Log all simulation executions for audit

2. **Rate Limiting**
   - Prevent abuse by limiting concurrent simulations
   - Max delay between steps: 7 days

3. **Validation**
   - Validate all alert data before sending
   - Prevent injection attacks in alert fields

4. **Resource Management**
   - Cancel long-running simulations if needed
   - Clean up old simulations (> 90 days)

---

## 📊 Success Metrics

- ✅ Simulations can be created in < 2 minutes
- ✅ 100% of alert types supported
- ✅ Zero failed alert submissions in testing
- ✅ Drag & drop works smoothly (< 50ms response)
- ✅ Can run 5 concurrent simulations without issues

---

## 🎯 Timeline Estimate

- **Phase 1-2** (DB + CQRS): 1 day
- **Phase 3** (Runner Service): 1 day
- **Phase 4** (UI): 2 days
- **Phase 5** (API Support): 0.5 day
- **Phase 6** (Testing): 1 day
- **Phase 7** (Docs): 0.5 day

**Total: ~6 days** (with one developer)

---

## 📚 References

- CQRS Architecture: `/ASPSBackend14_J/CQRS_ARCHITECTURE.md`
- Existing Alerts: `/ASPSBackend14_J/Common/Entities/DeviceAlerts.cs`
- Admin Pages: `/ASPSBackend14_J/WebApi/Pages/Users/`
- Background Services: [ASP.NET Core Hosted Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

---

**Document Version:** 1.0
**Created:** 2026-03-23
**Author:** Alex (CTO) 🧠
**Status:** Ready for Development
