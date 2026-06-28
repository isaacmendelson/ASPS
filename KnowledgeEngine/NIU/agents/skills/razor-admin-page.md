---
name: razor-admin-page
description: Scaffold a new admin Razor Page in WebApi — .cshtml + .cshtml.cs + CQRS query wiring + (when relevant) nav-link registration. Mirrors the SCRUM-904 RiskDetail pattern.
---

# /razor-admin-page

Generates a complete admin page in `WebApi/Pages/`: view + page-model + CQRS query call + (for Index pages) the nav-menu entry. The pattern matches [Users/RiskDetail.cshtml(.cs)](c:/Jobs/ASPS/GitHub/Software/ASPSBackend14_J/WebApi/Pages/Users/RiskDetail.cshtml).

## When to invoke
- User wants to add a new admin UI page.
- User says "add admin page", "new Razor page", "expose <data> in the admin UI".

## Ask first

1. **Folder + page name?** Existing convention groups by entity: `Users/`, `Devices/`, `DeviceAlerts/`, `BankWebsites/`, etc.
2. **Page role?**
   - **Index** — list / table view, reached from the sidebar (needs nav-link).
   - **Detail** — single-record view, reached from an Index row (no nav-link, takes a key in query string).
   - **Create/Edit** — form, reached from Index, posts to a CQRS Command.
3. **Data source** — which CQRS Query/Command should it call? If it doesn't exist yet, **stop and run `/cqrs-handler` first**, then come back here.
4. **Authorization** — should it require Keycloak auth? Default: yes, inherits from layout. Anonymous pages are rare; surface that explicitly.

## Files to create / modify

### 1. `WebApi/Pages/<Folder>/<Name>.cshtml` — view

```cshtml
@page
@model WebApi.Pages.<Folder>.<Name>Model
@{
    ViewData["Title"] = "<Display Title>";
}

<h2><Display Title></h2>
<p class="text-muted">SCRUM-### — short description of what this page shows.</p>

@if (!string.IsNullOrEmpty(Model.ErrorMessage))
{
    <div class="alert alert-danger">@Model.ErrorMessage</div>
}
else if (Model.Data == null)
{
    <div class="alert alert-info">No data yet.</div>
}
else
{
    <!-- Cards, tables, etc. — match existing pages' Bootstrap 5 idioms -->
}
```

Reference for layout idioms (cards, table styling, badges): [Users/RiskDetail.cshtml](c:/Jobs/ASPS/GitHub/Software/ASPSBackend14_J/WebApi/Pages/Users/RiskDetail.cshtml). Match its visual structure unless the user asks for something else.

### 2. `WebApi/Pages/<Folder>/<Name>.cshtml.cs` — page model

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Business.Queries;
using WebApi.Services;

namespace WebApi.Pages.<Folder>
{
    public class <Name>Model : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<<Name>Model> _logger;

        public <Name>Model(ICQRSClient cqrsClient, ILogger<<Name>Model> logger)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Key { get; set; } = string.Empty;   // for Detail pages

        public <ResultType>? Data { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                var query = new <Name>Query { Key = Key };
                var result = await _cqrsClient.SendQueryAsync<<Name>QueryResult>(query);
                if (result == null || !result.Success)
                {
                    ErrorMessage = result?.Message ?? "Query failed";
                    return;
                }
                Data = result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading <Name> for {Key}", Key);
                ErrorMessage = "Could not load — see backend logs.";
            }
        }
    }
}
```

For **Create/Edit** pages, add `OnPostAsync` that builds a `<Name>Command` and calls `_cqrsClient.SendCommandAsync<...>(command)`. Use `[BindProperty]` (not `[BindProperty(SupportsGet = true)]`) on form fields.

### 3. Navigation — `WebApi/Pages/Shared/_Layout.cshtml`

**Only for Index pages.** Add a `<li>` matching the existing pattern. Pick a FontAwesome icon that's not already used in the sidebar:

```html
<li class="nav-item">
    <a class="nav-link" asp-page="/<Folder>/Index">
        <i class="fas fa-<icon>"></i> <Display Title>
    </a>
</li>
```

Place it in the section that matches the page's role — Data section (Users, Devices, Alerts), Reference data section (Phishing, Banks), Operations section (Roadmaps, Simulations), or System section. Don't append to the bottom blindly.

### 4. Link from parent — for Detail / Create / Edit pages

These don't get sidebar entries. Wire them from the parent Index page:

```cshtml
<a asp-page="/<Folder>/<Name>" asp-route-key="@item.Key" class="btn btn-sm btn-secondary">Details</a>
```

## Verification

1. `dotnet build ASPSBackend.sln -c Debug --nologo` clean (`MSB3027`/`MSB3021` = file lock, ignore; real failures are `CS####`).
2. Restart WebApi. Browse to the page.
3. Keycloak SSO flow should redirect → login → return to the page. If you get a 401 or an infinite redirect loop, check the page's `[Authorize]` attribute vs the layout default.
4. With a missing/empty CQRS result, the "no data" path should render — not a 500.

## Never

- Read from `AppDbContext` directly in the page model. The pattern is `ICQRSClient` → `SendQueryAsync` → handler in Business → repository. Skipping this breaks the WebApi ↔ ASPSBackend separation that the CQRSGateway exists to enforce.
- Add a nav-link for a Detail page. It pollutes the sidebar.
- Hard-code Keycloak claims. If the page needs identity, read from `User` (the standard ClaimsPrincipal) — its mapping is configured in `Program.cs`.
- Inline raw SQL via Pomelo extension methods. Use a repository, surfaced via a Query/Command.

## Output convention

```
Page: /<Folder>/<Name>
Role: Index | Detail | Create | Edit
Files created:
  - WebApi/Pages/<Folder>/<Name>.cshtml
  - WebApi/Pages/<Folder>/<Name>.cshtml.cs
Files modified:
  - (Index only) WebApi/Pages/Shared/_Layout.cshtml  (nav-link added)
  - (Detail/Edit) WebApi/Pages/<Folder>/Index.cshtml  (link added)
CQRS query/command used: <Name>Query | <Name>Command
Build: PASS / FAIL
```
