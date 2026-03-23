# ASPS-352: Admin Device Alert Simulator - Architecture Design

## Overview

כלי admin ליצירת והרצת סימולציות של device alerts למטרות בדיקה ופיתוח.

### Purpose
- יצירת סדרות alerts מתוזמנות שמדמות התנהגות אמיתית של מכשירים
- בדיקת הגיב המערכת לתרחישים שונים
- הדגמות ו-demos
- בדיקת תהליכי ניתוח והתראות

### Core Concept
**Simulation** = סדרת **SimulationSteps** מסודרת, כאשר כל step מכיל:
- User (למי ה-alert משויך)
- Device (מאיזה מכשיר ה-alert מגיע)
- AlertType (סוג ה-alert: RemoteAccess, UrlAlert, TrackUrl, etc.)
- AlertData (נתונים דינמיים לפי סוג ה-alert)
- Delay (השהייה לפני Step הבא)

---

## 1. Database Design

### 1.1 SimulationEntity

```csharp
namespace Common.Entities;

public class SimulationEntity : Entity
{
    private Tag? tag;
    
    // Basic Info
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Steps stored as JSON
    public string StepsJson { get; set; } = "[]"; // JSON array of SimulationStepDto
    
    // Metadata
    public DateTime? LastRunAt { get; set; }
    public string? LastRunByUserKeyField { get; set; }
    public SimulationStatus Status { get; set; } = SimulationStatus.Draft;
    
    [NotMapped]
    public override string TypeName => "Simulation";
    
    [NotMapped]
    public override Tag Tag
    {
        get
        {
            if (this.tag is not null)
                return this.tag;
                
            if (!string.IsNullOrEmpty(this.KeyField))
            {
                this.tag = CreateTag();
                return this.tag;
            }
            
            return CreateTag();
        }
    }
    
    protected virtual Tag CreateTag()
    {
        var name = !string.IsNullOrWhiteSpace(Name) ? Name : $"Simulation-{KeyField}";
        return new Tag(this.Key, name, this.TypeName);
    }
}

public enum SimulationStatus
{
    Draft,      // נוצר אך לא הורץ
    Running,    // בהרצה כרגע
    Completed,  // הושלם בהצלחה
    Failed,     // נכשל
    Cancelled   // בוטל
}
```

### 1.2 SimulationStepDto (JSON Model)

זה המודל שנשמר ב-`StepsJson`:

```csharp
namespace Common.Models.Simulation;

public class SimulationStepDto
{
    public int Order { get; set; }  // מיקום ב-sequence
    
    // Target
    public string UserKeyField { get; set; } = string.Empty;
    public string DeviceKeyField { get; set; } = string.Empty;
    
    // Alert Configuration
    public string AlertType { get; set; } = string.Empty; // "RemoteAccessAlert", "UrlAlert", etc.
    public string AlertDataJson { get; set; } = "{}"; // Dynamic fields per alert type
    
    // Timing
    public DelayType DelayType { get; set; } = DelayType.Seconds;
    public int DelayValue { get; set; } = 0;
    
    // Display
    public string? Label { get; set; } // תיאור אופציונלי ל-step
}

public enum DelayType
{
    Seconds,
    Minutes,
    Hours,
    Days
}
```

### 1.3 AlertDataJson Structure

כל סוג alert יש structure שונה:

#### RemoteAccessAlert
```json
{
  "remoteAccessApp": "AnyDesk",
  "connectionUrl": "ad://123456789",
  "connectionStatus": "Active",
  "runningProcesses": 5,
  "connectionsCount": 1,
  "sessionStatus": 1,
  "remoteOS": "Windows 11",
  "remoteVersion": "22H2",
  "connectionType": "Unattended",
  "fileTransferActive": true,
  "fileTransfers": 3
}
```

#### UrlAlert
```json
{
  "url": "https://suspicious-site.com",
  "userAgent": "Mozilla/5.0...",
  "tabId": "tab-12345",
  "trackerKeys": [],
  "iFrameDomains": ["evil.com", "malware.net"]
}
```

#### TrackUrlAlert
```json
{
  "url": "https://bank-fake.com",
  "fromUrl": "https://google.com",
  "duration": 120,
  "userAgent": "Mozilla/5.0...",
  "tabId": "tab-67890",
  "scamInProgressKey": "scam-123",
  "timezone": "Asia/Jerusalem"
}
```

---

## 2. CQRS Design

### 2.1 Commands

#### CreateSimulationCommand
```csharp
namespace Business.Commands;

public class CreateSimulationCommand : Command
{
    public CreateSimulationCommand()
    {
        CommandType = nameof(CreateSimulationCommand);
    }
    
    public string CommandType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<SimulationStepDto> Steps { get; set; } = new();
}

public class CreateSimulationCommandResult : CommandResult
{
    public Key? SimulationKey { get; set; }
}
```

#### UpdateSimulationCommand
```csharp
public class UpdateSimulationCommand : Command
{
    public UpdateSimulationCommand()
    {
        CommandType = nameof(UpdateSimulationCommand);
    }
    
    public string CommandType { get; set; }
    public string SimulationKeyField { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<SimulationStepDto> Steps { get; set; } = new();
}

public class UpdateSimulationCommandResult : CommandResult
{
    public bool Updated { get; set; }
}
```

#### DeleteSimulationCommand
```csharp
public class DeleteSimulationCommand : Command
{
    public DeleteSimulationCommand()
    {
        CommandType = nameof(DeleteSimulationCommand);
    }
    
    public string CommandType { get; set; }
    public string SimulationKeyField { get; set; } = string.Empty;
}

public class DeleteSimulationCommandResult : CommandResult
{
    public bool Deleted { get; set; }
}
```

#### RunSimulationCommand
```csharp
public class RunSimulationCommand : Command
{
    public RunSimulationCommand()
    {
        CommandType = nameof(RunSimulationCommand);
    }
    
    public string CommandType { get; set; }
    public string SimulationKeyField { get; set; } = string.Empty;
    public string RunByUserKeyField { get; set; } = string.Empty; // מי הריץ
}

public class RunSimulationCommandResult : CommandResult
{
    public bool Started { get; set; }
    public string? JobId { get; set; } // For async tracking
}
```

### 2.2 Queries

#### GetSimulationsQuery
```csharp
namespace Business.Queries;

public class GetSimulationsQuery : Query
{
    public GetSimulationsQuery()
    {
        QueryType = nameof(GetSimulationsQuery);
    }
    
    public string QueryType { get; set; }
    public string? Search { get; set; }
    public SimulationStatus? StatusFilter { get; set; }
    public string? SortBy { get; set; } // "Name", "LastRunAt", "DateCreated"
    public bool SortDescending { get; set; } = true;
}

public class GetSimulationsQueryResult : QueryResult
{
    public List<SimulationListItemDto> Simulations { get; set; } = new();
}

public class SimulationListItemDto
{
    public string KeyField { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StepsCount { get; set; }
    public SimulationStatus Status { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime DateCreated { get; set; }
}
```

#### GetSimulationDetailsQuery
```csharp
public class GetSimulationDetailsQuery : Query
{
    public GetSimulationDetailsQuery()
    {
        QueryType = nameof(GetSimulationDetailsQuery);
    }
    
    public string QueryType { get; set; }
    public string SimulationKeyField { get; set; } = string.Empty;
}

public class GetSimulationDetailsQueryResult : QueryResult
{
    public string KeyField { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<SimulationStepDto> Steps { get; set; } = new();
    public SimulationStatus Status { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime DateCreated { get; set; }
}
```

#### GetUsersForSimulationQuery
```csharp
// For autocomplete in step editor
public class GetUsersForSimulationQuery : Query
{
    public GetUsersForSimulationQuery()
    {
        QueryType = nameof(GetUsersForSimulationQuery);
    }
    
    public string QueryType { get; set; }
    public string? Search { get; set; }
    public int Limit { get; set; } = 10;
}

public class GetUsersForSimulationQueryResult : QueryResult
{
    public List<UserOptionDto> Users { get; set; } = new();
}

public class UserOptionDto
{
    public string KeyField { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
```

#### GetDevicesForUserQuery
```csharp
// Get devices for selected user
public class GetDevicesForUserQuery : Query
{
    public GetDevicesForUserQuery()
    {
        QueryType = nameof(GetDevicesForUserQuery);
    }
    
    public string QueryType { get; set; }
    public string UserKeyField { get; set; } = string.Empty;
}

public class GetDevicesForUserQueryResult : QueryResult
{
    public List<DeviceOptionDto> Devices { get; set; } = new();
}

public class DeviceOptionDto
{
    public string KeyField { get; set; } = string.Empty;
    public string DeviceUid { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
}
```

---

## 3. Simulation Runner Design

### 3.1 Architecture

**SimulationRunnerService** - Background service שמריץ simulations:

```csharp
namespace Business.Services;

public interface ISimulationRunnerService
{
    Task<string> StartSimulationAsync(string simulationKeyField, string runByUserKeyField);
    Task CancelSimulationAsync(string jobId);
    Task<SimulationRunStatus> GetStatusAsync(string jobId);
}

public class SimulationRunnerService : ISimulationRunnerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SimulationRunnerService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningJobs;
    
    // Implementation details below...
}
```

### 3.2 Execution Flow

```
1. RunSimulationCommand received
   ↓
2. SimulationRunnerService.StartSimulationAsync()
   - Load Simulation entity
   - Validate steps
   - Create Job ID
   - Update status to "Running"
   - Start background Task
   ↓
3. For each SimulationStep (in order):
   a. Parse AlertDataJson
   b. Create appropriate DeviceAlertEntity
   c. Call existing alert processing pipeline
   d. Wait for delay (Seconds/Minutes/Hours/Days)
   ↓
4. Update simulation status:
   - Completed (if all steps succeeded)
   - Failed (if any step failed)
   - Cancelled (if manually stopped)
```

### 3.3 Alert Injection Strategy

**Option A: Direct Entity Creation** (Recommended)
```csharp
// Create DeviceAlertEntity directly and save to DB
private async Task ExecuteStepAsync(SimulationStepDto step)
{
    DeviceAlertEntity alert = step.AlertType switch
    {
        "RemoteAccessAlert" => CreateRemoteAccessAlert(step),
        "UrlAlert" => CreateUrlAlert(step),
        "TrackUrlAlert" => CreateTrackUrlAlert(step),
        _ => throw new NotSupportedException()
    };
    
    // Save to database using repository
    await _alertRepository.CreateAsync(alert);
    
    // Trigger analysis pipeline if needed
    await _analysisService.ProcessAlertAsync(alert.KeyField);
}
```

**Option B: Command Simulation**
```csharp
// Send alert through existing SaveDeviceAlertCommand
private async Task ExecuteStepAsync(SimulationStepDto step)
{
    var command = CreateAlertCommand(step);
    var result = await _cqrsClient.SendCommandAsync(command);
}
```

**Recommendation**: Option A - direct entity creation
- פשוט יותר
- אין צורך לחשוף SaveDeviceAlertCommand ב-admin
- מאפשר שליטה מלאה על timestamp וdata

### 3.4 Delay Implementation

```csharp
private async Task WaitForDelayAsync(DelayType delayType, int delayValue, CancellationToken ct)
{
    var delay = delayType switch
    {
        DelayType.Seconds => TimeSpan.FromSeconds(delayValue),
        DelayType.Minutes => TimeSpan.FromMinutes(delayValue),
        DelayType.Hours => TimeSpan.FromHours(delayValue),
        DelayType.Days => TimeSpan.FromDays(delayValue),
        _ => TimeSpan.Zero
    };
    
    if (delay > TimeSpan.Zero)
    {
        await Task.Delay(delay, ct);
    }
}
```

### 3.5 Error Handling

- כל step מבודד - שגיאה ב-step אחד לא עוצרת את כל ה-simulation
- Logging מפורט לכל פעולה
- Status updates בזמן אמת
- Cancellation support

---

## 4. Razor Pages Structure

### 4.1 Pages Hierarchy

```
WebApi/Pages/Simulations/
├── Index.cshtml             # List of simulations
├── Index.cshtml.cs
├── Create.cshtml            # Create new simulation
├── Create.cshtml.cs
├── Edit.cshtml              # Edit existing simulation
├── Edit.cshtml.cs
└── _StepEditorModal.cshtml  # Partial view for step editor
```

### 4.2 Index Page (List)

**URL**: `/Simulations`

**Features**:
- טבלה עם כל ה-simulations
- Search box (שם)
- Filter by status (Draft/Running/Completed/Failed)
- Sort by Name/LastRunAt/DateCreated
- Actions per row:
  - Edit - עריכת simulation
  - Run - הרצה מיידית
  - Delete - מחיקה
  - View Details - הצגת כל ה-steps

**UI Structure**:
```html
<div class="container">
    <h1>Device Alert Simulations</h1>
    
    <!-- Filters & Search -->
    <div class="filters">
        <input type="text" name="search" placeholder="Search simulations..." />
        <select name="statusFilter">
            <option value="">All Statuses</option>
            <option value="Draft">Draft</option>
            <option value="Running">Running</option>
            <option value="Completed">Completed</option>
            <option value="Failed">Failed</option>
        </select>
        <button type="submit">Filter</button>
        <a href="/Simulations/Create" class="btn btn-primary">Create New</a>
    </div>
    
    <!-- Simulations Table -->
    <table class="table">
        <thead>
            <tr>
                <th>Name</th>
                <th>Description</th>
                <th>Steps</th>
                <th>Status</th>
                <th>Last Run</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var sim in Model.Simulations)
            {
                <tr>
                    <td>@sim.Name</td>
                    <td>@sim.Description</td>
                    <td>@sim.StepsCount</td>
                    <td><span class="badge status-@sim.Status">@sim.Status</span></td>
                    <td>@sim.LastRunAt?.ToString("g")</td>
                    <td>
                        <a href="/Simulations/Edit/@sim.KeyField">Edit</a>
                        <form method="post" asp-page-handler="Run" asp-route-id="@sim.KeyField" style="display:inline;">
                            <button type="submit" class="btn btn-sm btn-success">Run</button>
                        </form>
                        <form method="post" asp-page-handler="Delete" asp-route-id="@sim.KeyField" style="display:inline;">
                            <button type="submit" class="btn btn-sm btn-danger" 
                                    onclick="return confirm('Delete this simulation?')">Delete</button>
                        </form>
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>
```

### 4.3 Create/Edit Page

**URL**: `/Simulations/Create`, `/Simulations/Edit/{id}`

**Features**:
- שדות: Name, Description
- Steps list עם drag & drop reordering
- כפתורים:
  - Add Step - פותח modal
  - Remove Step
  - Reorder (drag handles)
- Save/Cancel buttons

**UI Structure**:
```html
<div class="container">
    <h1>@(Model.IsEditMode ? "Edit" : "Create") Simulation</h1>
    
    <form method="post">
        <div class="form-group">
            <label>Name</label>
            <input type="text" asp-for="Name" class="form-control" required />
        </div>
        
        <div class="form-group">
            <label>Description</label>
            <textarea asp-for="Description" class="form-control" rows="3"></textarea>
        </div>
        
        <hr />
        
        <h3>Simulation Steps</h3>
        
        <!-- Steps List -->
        <div id="steps-list" class="sortable-list">
            @for (int i = 0; i < Model.Steps.Count; i++)
            {
                <div class="step-item" data-order="@i">
                    <span class="drag-handle">⋮⋮</span>
                    <div class="step-content">
                        <strong>Step @(i+1):</strong> 
                        @Model.Steps[i].Label ?? Model.Steps[i].AlertType
                        <br/>
                        <small>
                            User: @Model.Steps[i].UserKeyField | 
                            Device: @Model.Steps[i].DeviceKeyField | 
                            Delay: @Model.Steps[i].DelayValue @Model.Steps[i].DelayType
                        </small>
                    </div>
                    <div class="step-actions">
                        <button type="button" class="btn btn-sm btn-primary" 
                                onclick="editStep(@i)">Edit</button>
                        <button type="button" class="btn btn-sm btn-danger" 
                                onclick="removeStep(@i)">Remove</button>
                    </div>
                    
                    <!-- Hidden fields for binding -->
                    <input type="hidden" name="Steps[@i].Order" value="@i" />
                    <input type="hidden" name="Steps[@i].UserKeyField" value="@Model.Steps[i].UserKeyField" />
                    <input type="hidden" name="Steps[@i].DeviceKeyField" value="@Model.Steps[i].DeviceKeyField" />
                    <input type="hidden" name="Steps[@i].AlertType" value="@Model.Steps[i].AlertType" />
                    <input type="hidden" name="Steps[@i].AlertDataJson" value="@Model.Steps[i].AlertDataJson" />
                    <input type="hidden" name="Steps[@i].DelayType" value="@Model.Steps[i].DelayType" />
                    <input type="hidden" name="Steps[@i].DelayValue" value="@Model.Steps[i].DelayValue" />
                    <input type="hidden" name="Steps[@i].Label" value="@Model.Steps[i].Label" />
                </div>
            }
        </div>
        
        <button type="button" class="btn btn-success" onclick="openStepModal()">
            + Add Step
        </button>
        
        <hr />
        
        <button type="submit" class="btn btn-primary">Save Simulation</button>
        <a href="/Simulations" class="btn btn-secondary">Cancel</a>
    </form>
</div>

<!-- Step Editor Modal -->
@await Html.PartialAsync("_StepEditorModal")

<script src="/js/simulations.js"></script>
```

### 4.4 Step Editor Modal (_StepEditorModal.cshtml)

**Partial View** - נפתח כ-modal dialog

**Features**:
1. **User Selection** - autocomplete search box
2. **Device Selection** - dropdown (מוצג אחרי בחירת user)
3. **Alert Type** - dropdown:
   - RemoteAccessAlert
   - UrlAlert
   - TrackUrlAlert
4. **Alert Data** - dynamic fields לפי alert type
5. **Delay** - input + dropdown (Seconds/Minutes/Hours/Days)
6. **Label** - optional description

**UI Structure**:
```html
<div id="stepEditorModal" class="modal">
    <div class="modal-content">
        <h2>Edit Simulation Step</h2>
        
        <div class="form-group">
            <label>User</label>
            <input type="text" id="userSearch" class="form-control autocomplete" 
                   placeholder="Search user by name or email..." />
            <input type="hidden" id="selectedUserKeyField" />
        </div>
        
        <div class="form-group">
            <label>Device</label>
            <select id="deviceSelect" class="form-control" disabled>
                <option value="">-- Select Device --</option>
            </select>
        </div>
        
        <div class="form-group">
            <label>Alert Type</label>
            <select id="alertType" class="form-control" onchange="updateAlertFields()">
                <option value="">-- Select Alert Type --</option>
                <option value="RemoteAccessAlert">Remote Access Alert</option>
                <option value="UrlAlert">URL Alert</option>
                <option value="TrackUrlAlert">Track URL Alert</option>
            </select>
        </div>
        
        <!-- Dynamic Alert Fields Container -->
        <div id="alertFieldsContainer">
            <!-- Populated by JS based on selected AlertType -->
        </div>
        
        <div class="form-group">
            <label>Delay Before Next Step</label>
            <div class="input-group">
                <input type="number" id="delayValue" class="form-control" value="0" min="0" />
                <select id="delayType" class="form-control">
                    <option value="Seconds">Seconds</option>
                    <option value="Minutes">Minutes</option>
                    <option value="Hours">Hours</option>
                    <option value="Days">Days</option>
                </select>
            </div>
        </div>
        
        <div class="form-group">
            <label>Label (optional)</label>
            <input type="text" id="stepLabel" class="form-control" 
                   placeholder="e.g., 'User opens malicious email'" />
        </div>
        
        <div class="modal-actions">
            <button type="button" class="btn btn-primary" onclick="saveStep()">Save Step</button>
            <button type="button" class="btn btn-secondary" onclick="closeStepModal()">Cancel</button>
        </div>
    </div>
</div>
```

### 4.5 Dynamic Alert Fields (JavaScript)

**RemoteAccessAlert Fields**:
```javascript
const remoteAccessFields = `
    <div class="form-group">
        <label>Remote Access App</label>
        <select id="remoteAccessApp" class="form-control">
            <option value="AnyDesk">AnyDesk</option>
            <option value="TeamViewer">TeamViewer</option>
            <option value="RustDesk">RustDesk</option>
            <option value="Chrome Remote Desktop">Chrome Remote Desktop</option>
        </select>
    </div>
    <div class="form-group">
        <label>Connection URL</label>
        <input type="text" id="connectionUrl" class="form-control" placeholder="ad://123456789" />
    </div>
    <div class="form-group">
        <label>Connection Status</label>
        <select id="connectionStatus" class="form-control">
            <option value="Active">Active</option>
            <option value="Idle">Idle</option>
            <option value="Connecting">Connecting</option>
            <option value="Disconnected">Disconnected</option>
        </select>
    </div>
    <!-- Additional fields: runningProcesses, connectionsCount, etc. -->
`;
```

**UrlAlert Fields**:
```javascript
const urlAlertFields = `
    <div class="form-group">
        <label>URL</label>
        <input type="url" id="url" class="form-control" placeholder="https://suspicious-site.com" required />
    </div>
    <div class="form-group">
        <label>User Agent</label>
        <input type="text" id="userAgent" class="form-control" 
               value="Mozilla/5.0 (Windows NT 10.0; Win64; x64)..." />
    </div>
    <div class="form-group">
        <label>Tab ID</label>
        <input type="text" id="tabId" class="form-control" placeholder="tab-12345" />
    </div>
    <div class="form-group">
        <label>IFrame Domains (comma-separated)</label>
        <input type="text" id="iFrameDomains" class="form-control" 
               placeholder="evil.com, malware.net" />
    </div>
`;
```

**TrackUrlAlert Fields**:
```javascript
const trackUrlFields = `
    <div class="form-group">
        <label>URL</label>
        <input type="url" id="url" class="form-control" placeholder="https://bank-fake.com" required />
    </div>
    <div class="form-group">
        <label>From URL (Referrer)</label>
        <input type="url" id="fromUrl" class="form-control" placeholder="https://google.com" />
    </div>
    <div class="form-group">
        <label>Duration (seconds)</label>
        <input type="number" id="duration" class="form-control" value="120" min="0" />
    </div>
    <div class="form-group">
        <label>Scam In Progress Key</label>
        <input type="text" id="scamInProgressKey" class="form-control" placeholder="scam-123" />
    </div>
    <div class="form-group">
        <label>Timezone</label>
        <input type="text" id="timezone" class="form-control" value="Asia/Jerusalem" />
    </div>
`;
```

### 4.6 JavaScript Functions

**Main Functions** (`wwwroot/js/simulations.js`):

```javascript
// User autocomplete
async function searchUsers(query) {
    const response = await fetch(`/api/simulations/search-users?q=${query}`);
    return await response.json();
}

// Load devices for selected user
async function loadDevicesForUser(userKeyField) {
    const response = await fetch(`/api/simulations/devices/${userKeyField}`);
    const data = await response.json();
    
    const deviceSelect = document.getElementById('deviceSelect');
    deviceSelect.innerHTML = '<option value="">-- Select Device --</option>';
    
    data.devices.forEach(device => {
        deviceSelect.innerHTML += `<option value="${device.keyField}">
            ${device.deviceUid} (${device.deviceType})
        </option>`;
    });
    
    deviceSelect.disabled = false;
}

// Update alert fields based on selected alert type
function updateAlertFields() {
    const alertType = document.getElementById('alertType').value;
    const container = document.getElementById('alertFieldsContainer');
    
    switch (alertType) {
        case 'RemoteAccessAlert':
            container.innerHTML = remoteAccessFields;
            break;
        case 'UrlAlert':
            container.innerHTML = urlAlertFields;
            break;
        case 'TrackUrlAlert':
            container.innerHTML = trackUrlFields;
            break;
        default:
            container.innerHTML = '';
    }
}

// Save step (add to list or update existing)
function saveStep() {
    const step = {
        userKeyField: document.getElementById('selectedUserKeyField').value,
        deviceKeyField: document.getElementById('deviceSelect').value,
        alertType: document.getElementById('alertType').value,
        alertDataJson: collectAlertData(),
        delayType: document.getElementById('delayType').value,
        delayValue: parseInt(document.getElementById('delayValue').value),
        label: document.getElementById('stepLabel').value
    };
    
    // Add to steps list in form
    addStepToList(step);
    closeStepModal();
}

// Collect alert-specific data into JSON
function collectAlertData() {
    const alertType = document.getElementById('alertType').value;
    const data = {};
    
    // Collect all input fields from alertFieldsContainer
    document.querySelectorAll('#alertFieldsContainer input, #alertFieldsContainer select').forEach(field => {
        data[field.id] = field.value;
    });
    
    return JSON.stringify(data);
}

// Drag & drop reordering using SortableJS
const sortable = new Sortable(document.getElementById('steps-list'), {
    handle: '.drag-handle',
    onEnd: function(evt) {
        updateStepOrders();
    }
});

function updateStepOrders() {
    const items = document.querySelectorAll('.step-item');
    items.forEach((item, index) => {
        item.querySelector('input[name$=".Order"]').value = index;
    });
}
```

---

## 5. API Endpoints (Supporting Services)

### 5.1 User Search API

```csharp
// WebApi/Controllers/SimulationsController.cs

[ApiController]
[Route("api/simulations")]
public class SimulationsController : ControllerBase
{
    private readonly CQRSClient _cqrsClient;
    
    [HttpGet("search-users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int limit = 10)
    {
        var query = new GetUsersForSimulationQuery
        {
            Search = q,
            Limit = limit
        };
        
        var result = await _cqrsClient.SendQueryAsync<GetUsersForSimulationQueryResult>(query);
        
        return Ok(new { users = result.Users });
    }
    
    [HttpGet("devices/{userKeyField}")]
    public async Task<IActionResult> GetDevicesForUser(string userKeyField)
    {
        var query = new GetDevicesForUserQuery
        {
            UserKeyField = userKeyField
        };
        
        var result = await _cqrsClient.SendQueryAsync<GetDevicesForUserQueryResult>(query);
        
        return Ok(new { devices = result.Devices });
    }
}
```

---

## 6. Data Flow Summary

### 6.1 Create/Edit Flow

```
User fills form
  → Steps list (with drag & drop)
  → Submit form
    → CreateSimulationCommand / UpdateSimulationCommand
      → CommandHandler:
        - Validates steps
        - Serializes Steps to JSON
        - Saves SimulationEntity
      → Redirect to Index
```

### 6.2 Run Flow

```
User clicks "Run"
  → POST /Simulations/Index?handler=Run
    → RunSimulationCommand
      → CommandHandler:
        - Calls SimulationRunnerService.StartSimulationAsync()
        - Updates status to "Running"
        - Returns JobId
      → Background Task:
        For each step:
          1. Deserialize AlertDataJson
          2. Create DeviceAlertEntity
          3. Save to DB
          4. Trigger analysis pipeline
          5. Wait for delay
        Update status to "Completed"
      → Redirect to Index (with success message)
```

---

## 7. Open Questions

### 7.1 Alert Analysis Pipeline
**Question**: האם Simulation צריך להפעיל את pipeline הניתוח האוטומטי (phishing detection, risk scoring) או רק ליצור raw alerts?

**Options**:
- A. Auto-trigger analysis (realistic simulation)
- B. Skip analysis (faster, less side effects)
- C. Make it configurable per simulation

**Recommendation**: Option C - add `TriggerAnalysis` boolean field to SimulationEntity

### 7.2 User Permissions
**Question**: מי יכול ליצור ולהריץ simulations?

**Options**:
- A. Admin role only
- B. Admin + specific permission
- C. All users (testing mode)

**Recommendation**: Option A initially, add permission system later

### 7.3 Simulation History
**Question**: האם לשמור היסטוריה של הרצות (run logs)?

**Options**:
- A. Single "LastRunAt" field (current design)
- B. Separate SimulationRunEntity with full logs
- C. Log to external system

**Recommendation**: Option A for MVP, Option B for future enhancement

### 7.4 Real-time Progress
**Question**: איך להציג progress בזמן הרצה?

**Options**:
- A. Polling (check status every N seconds)
- B. SignalR (real-time updates)
- C. WebSockets

**Recommendation**: Option A for MVP (simple), Option B for better UX

### 7.5 Concurrent Runs
**Question**: לאפשר הרצת multiple simulations בו זמנית?

**Recommendation**: Yes, use ConcurrentDictionary<JobId, CancellationTokenSource>

### 7.6 Device Token
**Question**: Simulated alerts צריכים device token תקף?

**Options**:
- A. Use special "SIMULATION" token
- B. Use real device tokens
- C. Generate temporary tokens

**Recommendation**: Option A - easier to identify simulated alerts

---

## 8. Implementation Order

### Phase 1: Core Infrastructure
1. ✅ Create SimulationEntity
2. ✅ Add DbSet to DbContext
3. ✅ Create migration
4. ✅ Define SimulationStepDto and supporting models
5. ✅ Create Commands/Queries

### Phase 2: CQRS Handlers
1. ✅ Implement CreateSimulationCommandHandler
2. ✅ Implement UpdateSimulationCommandHandler
3. ✅ Implement DeleteSimulationCommandHandler
4. ✅ Implement GetSimulationsQueryHandler
5. ✅ Implement GetSimulationDetailsQueryHandler
6. ✅ Implement GetUsersForSimulationQueryHandler
7. ✅ Implement GetDevicesForUserQueryHandler

### Phase 3: Simulation Runner
1. ✅ Create ISimulationRunnerService interface
2. ✅ Implement SimulationRunnerService
3. ✅ Implement alert entity creation logic
4. ✅ Implement delay logic
5. ✅ Add error handling and cancellation
6. ✅ Implement RunSimulationCommandHandler

### Phase 4: UI
1. ✅ Create /Simulations/Index page (list)
2. ✅ Create /Simulations/Create page
3. ✅ Create /Simulations/Edit page
4. ✅ Create _StepEditorModal partial view
5. ✅ Implement JavaScript (simulations.js)
6. ✅ Add supporting API endpoints

### Phase 5: Testing & Polish
1. ✅ Test all alert types
2. ✅ Test drag & drop reordering
3. ✅ Test run/cancel functionality
4. ✅ Add validation and error messages
5. ✅ Style and responsive design
6. ✅ Documentation

---

## 9. Technical Considerations

### 9.1 JSON Serialization
- Use `System.Text.Json` (already used in project)
- Handle null/missing fields gracefully
- Validate AlertDataJson structure before execution

### 9.2 Performance
- Simulations with long delays should not block the web server
- Use background tasks (Task.Run) for execution
- Consider Hangfire/Background Service for production

### 9.3 Security
- Validate user has permission to create alerts for selected user/device
- Sanitize all inputs
- Log all simulation runs for audit

### 9.4 Testing
- Unit tests for CommandHandlers
- Integration tests for SimulationRunner
- UI tests for drag & drop
- End-to-end test: create → run → verify alerts in DB

---

## 10. Future Enhancements

### 10.1 Templates
- Save simulations as templates
- Import/export simulations (JSON)
- Built-in templates for common scenarios

### 10.2 Advanced Scheduling
- Schedule simulations to run at specific times
- Recurring simulations (daily/weekly)

### 10.3 Scenarios
- Multi-user scenarios (coordination between devices)
- Branching logic (if X then Y)

### 10.4 Analytics
- Dashboard showing simulation run statistics
- Compare simulation results over time

---

## Conclusion

הארכיטקטורה מבוססת על הדפוסים הקיימים בפרויקט:
- ✅ Entity-based data model
- ✅ CQRS pattern for commands/queries
- ✅ Razor Pages for UI
- ✅ JSON for flexible data storage
- ✅ Background services for async operations

העיקרון המרכזי: **Simulation is just a sequence of pre-configured alerts with timing**

מוכן למימוש בשלבים!
