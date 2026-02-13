# ASPSBackend Solution - With Admin Frontend

## What's New

This is your existing ASPSBackend solution with the **Admin Frontend** added!

### Added Files (19 files):

**WebApi/Pages/** - Admin Frontend (Razor Pages + HTMX)
- Shared/_Layout.cshtml - Master layout with sidebar navigation
- _ViewImports.cshtml - Razor imports
- Index.cshtml + .cs - Dashboard with 6 cards
- Users/Index.cshtml + .cs - User management (CRUD)
- Devices/Index.cshtml + .cs - Device list
- DeviceAlerts/Index.cshtml + .cs - Alerts with filters
- AnalysisResults/Index.cshtml + .cs - Results with JSON viewer
- KnownPhishingWebsites/Index.cshtml + .cs - Blacklist + CSV import

**WebApi/Controllers/Admin/**
- UsersApiController.cs - User CRUD API
- NotificationPollingController.cs - HTTP long-polling for Python

**WebApi/Hubs/**
- NotificationsHub.cs - SignalR hub for real-time notifications

---

## Setup Instructions

### 1. Update WebApi.csproj

Add these lines to enable Razor Pages:

```xml
<ItemGroup>
  <!-- Add if not present -->
  <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
</ItemGroup>
```

### 2. Update Program.cs

Add Razor Pages support:

```csharp
// After builder.Services.AddControllers();
builder.Services.AddRazorPages();

// After app.UseAuthorization();
app.MapRazorPages();
app.MapHub<NotificationsHub>("/notificationshub");
```

### 3. Run & Access

```bash
dotnet build
dotnet run --project WebApi
```

**Access Admin Portal:** http://localhost:5000 (or your configured port)

---

## Project Structure

```
ASPSBackend/
├── ASPSBackend.sln
├── ASPSBackend/          # Main project
├── Business/             # Business logic
├── Common/               # Shared entities & interfaces
├── Interface/            # Repository interfaces
└── WebApi/               # API + Admin Frontend ⭐ NEW
    ├── Controllers/
    │   ├── Admin/        # ⭐ NEW
    │   │   ├── UsersApiController.cs
    │   │   └── NotificationPollingController.cs
    │   └── (your existing controllers)
    ├── Hubs/             # ⭐ NEW
    │   └── NotificationsHub.cs
    ├── Pages/            # ⭐ NEW - Admin Frontend
    │   ├── Shared/
    │   │   └── _Layout.cshtml
    │   ├── _ViewImports.cshtml
    │   ├── Index.cshtml (Dashboard)
    │   ├── Users/
    │   ├── Devices/
    │   ├── DeviceAlerts/
    │   ├── AnalysisResults/
    │   └── KnownPhishingWebsites/
    └── (your existing files)
```

---

## Admin Frontend Features

### Dashboard
- 6 cards: Users, Devices, Alerts (24h), Analysis Results (24h), Phishing Sites, System Status
- Auto-refresh every 30 seconds (HTMX)
- Real-time updates via SignalR

### User Management
- Complete CRUD operations
- Enable/disable users
- Soft delete
- View user details with:
  - Devices list
  - Device alerts
  - Analysis results
- Searchable DataTable

### Device Alerts
- Filter by date range, alert type, operating system
- View alert details
- Link to user profile
- Real-time updates

### Analysis Results
- **JSON Viewer** - formatted display of JsonValue field
- Copy JSON to clipboard
- Filter by type and date
- Link to source alert

### Known Phishing Websites
- **CSV Import** feature with:
  - Progress indicator
  - Duplicate detection (case-insensitive)
  - Import summary (imported, duplicates, errors)
- Add/edit/delete websites
- Search and filter

---

## Technologies Used

- **Razor Pages** - Server-side rendering
- **HTMX** - Dynamic updates without page reloads
- **SignalR** - Real-time notifications
- **Bootstrap 5** - Responsive UI
- **DataTables** - Searchable/sortable tables
- **Font Awesome** - Icons

---

## API Endpoints (New)

### Users API
- `GET /api/users/{key}/details` - User with devices/alerts/results
- `POST /api/users/save` - Create/Update user
- `POST /api/users/{key}/enable` - Enable user
- `POST /api/users/{key}/disable` - Disable user
- `DELETE /api/users/{key}` - Soft delete

### Notification Polling (for Python clients)
- `POST /api/notifications/register` - Register client
- `GET /api/notifications/poll` - Long-polling (30s timeout)
- `GET /api/notifications/get-all` - Get all pending
- `POST /api/notifications/unregister` - Cleanup

### SignalR Hub
- Endpoint: `/notificationshub`
- Method: `ReceiveNotification`

---

## Integration with Your Existing Code

The admin frontend integrates seamlessly with your existing:

1. **Common/Entities/** - Uses your existing User, UserDevice, DeviceAlert, etc.
2. **Business/Data/ApplicationDbContext** - Uses your EF Core context
3. **Business/Messaging/** - Can display notifications from your messaging system
4. **WebApi/Services/** - Compatible with your existing services

No breaking changes to your existing code!

---

## Next Steps

1. Update `WebApi.csproj` (add Razor Pages packages if missing)
2. Update `Program.cs` (add MapRazorPages and MapHub)
3. Build and run
4. Access http://localhost:YOUR_PORT
5. You should see the dashboard with 6 cards!

---

## Troubleshooting

### Issue: Razor Pages not loading
**Solution:** Ensure you added `builder.Services.AddRazorPages()` and `app.MapRazorPages()` in Program.cs

### Issue: Namespace errors
**Solution:** Build the solution to generate necessary files, or update _ViewImports.cshtml with your namespace

### Issue: Database connection
**Solution:** Ensure your connection string in appsettings.json is correct

---

## Screenshots (What You'll See)

1. **Dashboard** - 6 cards with counts and recent items
2. **Users Page** - Searchable table with enable/disable/delete buttons
3. **User Details** - Modal with tabs for devices, alerts, results
4. **Alerts Page** - Filterable list with date pickers
5. **Analysis Results** - JSON viewer modal
6. **Phishing Sites** - CSV import wizard

---

## Your Original Solution Preserved

All your existing files remain unchanged:
- ✅ Business logic
- ✅ Domain events
- ✅ Messaging system  
- ✅ Real-time analysis
- ✅ Indicators
- ✅ All your existing controllers and services

Only **added** new files in WebApi for the admin frontend!

---

**Ready to use!** 🚀
