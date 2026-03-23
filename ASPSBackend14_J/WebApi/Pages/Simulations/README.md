# Simulations Feature - User Guide

## 📋 Overview
The Simulations feature allows administrators to create, manage, and execute device alert simulations for testing and training purposes.

---

## 🗂️ Page Structure

```
/Simulations/
├── Index.cshtml          → List all simulations
├── Create.cshtml         → Create new simulation
├── Edit.cshtml           → Edit existing simulation
└── _StepEditorDialog.cshtml → Step editor modal (partial view)
```

---

## 🚀 Features

### **1. Simulations List (Index)**
**URL:** `/Simulations`

**Capabilities:**
- View all simulations in a searchable table
- Search by name or description
- Run simulation (executes all steps)
- Edit simulation
- Delete simulation

**Columns:**
- Name
- Description
- Creator
- Steps count
- Created date
- Actions (Run, Edit, Delete)

---

### **2. Create Simulation**
**URL:** `/Simulations/Create`

**Fields:**
- **Name** (required): Descriptive name for the simulation
- **Description** (optional): Detailed purpose/notes
- **Steps**: Dynamic list of simulation steps

**Step Configuration:**
- **Sequence #**: Order of execution (1 = first)
- **Delay (ms)**: Wait time before executing step
- **Target User ID**: User's KeyField (GUID)
- **Target Device UID**: Device's unique identifier
- **Alert Type**: 
  - `UrlAlert`
  - `RemoteAccessAlert`
  - `TrackUrlAlert`
- **Alert JSON Data**: JSON representation of the alert object
- **Priority**: Low, Medium, High, Critical

**Example Alert JSON (UrlAlert):**
```json
{
  "Url": "https://phishing-example.com",
  "Title": "Suspicious Website",
  "Category": "Phishing",
  "Severity": "High"
}
```

---

### **3. Edit Simulation**
**URL:** `/Simulations/Edit?key={simulationKey}`

**Capabilities:**
- Pre-loads existing simulation data
- Modify name, description, steps
- Save changes via `UpdateSimulationCommand`

---

### **4. Run Simulation**
**Action:** POST request from Index page

**Behavior:**
- Executes all simulation steps in sequence
- Each step:
  1. Waits `DelayMs` milliseconds
  2. Deserializes `AlertJson` based on `AlertType`
  3. Sends alert to target device/user via CQRS
- Returns execution summary:
  - Total steps
  - Executed steps
  - Start/End time

**Example Flow:**
```
Step 1 (Delay: 0ms)    → Send UrlAlert to Device-A, User-X
Step 2 (Delay: 5000ms) → Wait 5 seconds
                       → Send RemoteAccessAlert to Device-B, User-Y
Step 3 (Delay: 10000ms)→ Wait 10 seconds
                       → Send TrackUrlAlert to Device-A, User-X
```

---

## 🔧 Backend Integration

### **CQRS Queries:**
- `GetSimulationsQuery` → Get all simulations (with optional search)
- `GetSimulationDetailsQuery` → Get single simulation with steps
- `GetSimulationUsersQuery` → Get available users for step configuration
- `GetSimulationUserDevicesQuery` → Get devices for a user

### **CQRS Commands:**
- `CreateSimulationCommand` → Create new simulation
- `UpdateSimulationCommand` → Update existing simulation
- `DeleteSimulationCommand` → Soft-delete simulation
- `RunSimulationCommand` → Execute simulation steps

---

## 📦 Data Model

### **Simulation Entity**
```csharp
public class Simulation : Entity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string CreatorKeyField { get; set; }
    public string SimulationStepsJson { get; set; }
    public User Creator { get; set; }
    public DateTime DateCreated { get; set; }
}
```

### **SimulationStep Model**
```csharp
public class SimulationStep
{
    public int Sequence { get; set; }
    public long DelayMs { get; set; }
    public string DeviceUid { get; set; }
    public string UserId { get; set; }
    public string AlertType { get; set; }
    public string AlertJson { get; set; }
    public Priority Priority { get; set; }
}
```

---

## ⚙️ Configuration

### **Prerequisites:**
1. **Database Migration:**
   ```bash
   dotnet ef database update
   # Applies: 20260323212600_AddSimulationsTable.cs
   ```

2. **CQRS Backend:**
   - Must be running on configured endpoint (e.g., `tcp://localhost:5000`)

3. **Authentication:**
   - Currently using hardcoded `admin-user-key`
   - **TODO:** Implement proper session/auth to get current user

---

## 🧪 Testing

### **Manual Testing Workflow:**

1. **Create Simulation:**
   - Navigate to `/Simulations`
   - Click **New Simulation**
   - Enter Name: "Test Phishing Campaign"
   - Enter Description: "Testing user response to phishing alerts"
   - Click **Add Step**:
     - Sequence: 1
     - Delay: 0
     - User ID: `{valid-user-key}`
     - Device UID: `{valid-device-uid}`
     - Alert Type: `UrlAlert`
     - Alert JSON:
       ```json
       {
         "Url": "https://fake-phishing.com",
         "Title": "Suspicious Link",
         "Severity": "High"
       }
       ```
     - Priority: High
   - Click **Save Step**
   - Click **Create Simulation**

2. **Edit Simulation:**
   - From list, click **Edit**
   - Modify name/description
   - Edit/Add/Delete steps
   - Click **Update Simulation**

3. **Run Simulation:**
   - From list, click **Run**
   - Confirm prompt
   - Check success message: "5/5 steps executed"

4. **Delete Simulation:**
   - From list, click **Delete**
   - Confirm prompt
   - Verify it's removed from list

---

## 🐛 Known Issues

1. **User Authentication:**
   - Creator key is hardcoded as `admin-user-key`
   - **Fix:** Implement session management to get current user

2. **Alert JSON Validation:**
   - No client-side JSON schema validation
   - Invalid JSON caught on server, but late feedback
   - **Fix:** Add JSON validator in Step Editor Dialog

3. **Device/User Autocomplete:**
   - Currently manual input of IDs
   - **Enhancement:** Add autocomplete dropdowns using `GetSimulationUsersQuery` and `GetSimulationUserDevicesQuery`

---

## 📚 Related Documentation

- **Backend:** `docs/ASPS-352-FRONTEND-SUMMARY.md`
- **Database:** `Business/Migrations/20260323212600_AddSimulationsTable.cs`
- **Entities:** `Common/Entities/Simulation.cs`
- **Models:** `Common/Models/SimulationStep.cs`
- **Queries:** `Business/Queries/SimulationQueries.cs`
- **Commands:** `Business/Commands/SimulationCommands.cs`
- **Handlers:** `Business/Handlers/SimulationQueryHandlers.cs`, `Business/Handlers/SimulationCommandHandlers.cs`

---

## 📞 Support

**Questions?** Contact:
- **Alex (CTO)** - Technical architecture
- **QA Team** - Testing and validation
- **Isaac** - Overall project guidance

---

**Last Updated:** March 23, 2026  
**Version:** 1.0
