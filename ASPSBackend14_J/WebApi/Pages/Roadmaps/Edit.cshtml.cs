using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;
using Business.Commands;
using Business.Queries;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WebApi.Pages.Roadmaps
{
    public class EditModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;
        private readonly ILogger<EditModel> _logger;
        private readonly IWebHostEnvironment _env;

        public EditModel(ICQRSClient cqrsClient, ILogger<EditModel> logger, IWebHostEnvironment env)
        {
            _cqrsClient = cqrsClient;
            _logger = logger;
            _env = env;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public GetRoadmapByIdQueryResult? Roadmap { get; set; }
        public string? ErrorMessage { get; set; }

        private string? CurrentUser =>
            User.FindFirst("preferred_username")?.Value ?? User.Identity?.Name;

        public async Task<IActionResult> OnGetAsync()
        {
            if (Id <= 0) return RedirectToPage("Index");

            try
            {
                var query = new GetRoadmapByIdQuery { Id = Id };
                var result = await _cqrsClient.SendQueryAsync<GetRoadmapByIdQueryResult>(query);
                if (!result.Success)
                {
                    ErrorMessage = result.Message ?? "Roadmap not found";
                    return Page();
                }
                Roadmap = result;
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roadmap {Id}", Id);
                ErrorMessage = $"שגיאה בטעינה: {ex.Message}";
                return Page();
            }
        }

        // ---------- AJAX endpoints used by the SPA frontend (Phase 4) ----------

        public class SaveDto
        {
            [Required]
            public int Id { get; set; }
            [Required]
            public int ExpectedVersion { get; set; }
            [Required]
            public string Data { get; set; } = "{}";
        }

        public async Task<IActionResult> OnPostSaveAsync([FromBody] SaveDto body)
        {
            if (body is null || body.Id != Id)
            {
                return new JsonResult(new { success = false, message = "Invalid request" })
                    { StatusCode = 400 };
            }
            try
            {
                var cmd = new SaveRoadmapCommand
                {
                    Id = body.Id,
                    ExpectedVersion = body.ExpectedVersion,
                    Data = body.Data,
                    UpdatedBy = CurrentUser,
                };
                var result = await _cqrsClient.SendCommandAsync<SaveRoadmapCommandResult>(cmd);
                return new JsonResult(new
                {
                    success = result.Success,
                    message = result.Message,
                    newVersion = result.NewVersion,
                    lastUpdatedAt = result.LastUpdatedAt,
                    lastUpdatedBy = result.Success ? CurrentUser : null,
                    concurrencyConflict = result.ConcurrencyConflict,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving roadmap {Id}", Id);
                return new JsonResult(new { success = false, message = ex.Message })
                    { StatusCode = 500 };
            }
        }

        public class MetadataDto
        {
            [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
            [StringLength(500)] public string? Description { get; set; }
        }

        public async Task<IActionResult> OnPostMetadataAsync([FromBody] MetadataDto body)
        {
            if (body is null || string.IsNullOrWhiteSpace(body.Name))
            {
                return new JsonResult(new { success = false, message = "Name required" })
                    { StatusCode = 400 };
            }
            try
            {
                var cmd = new UpdateRoadmapMetadataCommand
                {
                    Id = Id,
                    Name = body.Name,
                    Description = body.Description,
                    UpdatedBy = CurrentUser,
                };
                var result = await _cqrsClient.SendCommandAsync<UpdateRoadmapMetadataCommandResult>(cmd);
                return new JsonResult(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metadata for roadmap {Id}", Id);
                return new JsonResult(new { success = false, message = ex.Message })
                    { StatusCode = 500 };
            }
        }

        // ---------------------------------------------------------------------
        // Export Viewer: render a self-contained HTML file (CSS+JS+Data inline)
        // that anyone can open offline. The Heebo font still loads from Google's
        // CDN — everything else is embedded.
        // ---------------------------------------------------------------------
        public async Task<IActionResult> OnGetViewerAsync()
        {
            if (Id <= 0) return RedirectToPage("Index");

            var query = new GetRoadmapByIdQuery { Id = Id };
            var result = await _cqrsClient.SendQueryAsync<GetRoadmapByIdQueryResult>(query);
            if (!result.Success)
            {
                return NotFound(result.Message ?? "Roadmap not found");
            }

            string css, js, body;
            try
            {
                var cssPath  = Path.Combine(_env.WebRootPath,     "css", "roadmap-spa.css");
                var jsPath   = Path.Combine(_env.WebRootPath,     "js",  "roadmap-spa.js");
                var bodyPath = Path.Combine(_env.ContentRootPath, "Pages", "Roadmaps", "_RoadmapSpaBody.cshtml");

                css  = await System.IO.File.ReadAllTextAsync(cssPath);
                js   = await System.IO.File.ReadAllTextAsync(jsPath);
                body = await System.IO.File.ReadAllTextAsync(bodyPath);

                // Strip the Razor comment header at the top of the partial so we get pure HTML.
                body = Regex.Replace(body, @"^@\*[\s\S]*?\*@\s*", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SPA assets for viewer export {Id}", Id);
                return StatusCode(500, "Failed to read SPA assets");
            }

            // Embed initial state — the SPA's loadState() reads window.RoadmapAdmin.initial.data.
            var initialPayload = JsonSerializer.Serialize(new
            {
                id = result.Id,
                name = result.Name,
                description = result.Description,
                version = result.Version,
                data = result.Data,
                lastUpdatedAt = result.LastUpdatedAt,
                lastUpdatedBy = result.LastUpdatedBy,
                createdBy = result.CreatedBy,
                dateCreated = result.DateCreated,
            }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            // Make any literal "</script>" inside the inlined JS safe (so it can't close our wrapper tag).
            var safeJs = js.Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);

            var encodedName        = HtmlEncoder.Default.Encode(result.Name ?? "Roadmap");
            var encodedDescription = HtmlEncoder.Default.Encode(result.Description ?? "");
            var lastUpdatedText    = (result.LastUpdatedAt ?? result.DateCreated).ToString("yyyy-MM-dd HH:mm");
            var lastUpdatedBy      = HtmlEncoder.Default.Encode(result.LastUpdatedBy ?? result.CreatedBy ?? "—");

            var sb = new StringBuilder(css.Length + js.Length + body.Length + 4096);
            sb.Append("<!DOCTYPE html>\n");
            sb.Append("<html lang=\"he\" dir=\"rtl\"><head>\n");
            sb.Append("<meta charset=\"UTF-8\">\n");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n");
            sb.Append($"<title>{encodedName} — ASPS Roadmap (Viewer)</title>\n");
            sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">\n");
            sb.Append("<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>\n");
            sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Heebo:wght@300;400;500;600;700;800;900&display=swap\" rel=\"stylesheet\">\n");
            sb.Append("<style>\n").Append(css).Append("\n</style>\n");
            sb.Append(@"<style>
  /* Viewer-only banner pinned to the top of the page */
  .rm-viewer-banner{
    position:fixed;top:0;inset-inline:0;z-index:60;
    background:linear-gradient(90deg,#0f172a,#1e3a5f);
    color:#fff;padding:8px 18px;
    font-family:'Heebo',sans-serif;font-size:.85rem;
    display:flex;justify-content:space-between;align-items:center;gap:14px;flex-wrap:wrap;
    box-shadow:0 2px 12px rgba(0,0,0,.35);
  }
  .rm-viewer-banner .meta-name{font-weight:700}
  .rm-viewer-banner .meta-name small{opacity:.7;font-weight:400;margin-inline-start:6px}
  .rm-viewer-banner .meta-when{opacity:.85}
  .rm-viewer-banner .meta-when strong{color:#fcd34d;font-weight:700}
  .rm-viewer-banner .meta-tag{background:rgba(255,255,255,.12);padding:2px 9px;border-radius:10px;font-size:.72rem}
  body{padding-top:42px}
</style>
");
            sb.Append("</head><body>\n");
            sb.Append("<div class=\"rm-viewer-banner\">");
            sb.Append($"<div class=\"meta-name\">📊 {encodedName}");
            if (!string.IsNullOrWhiteSpace(result.Description))
            {
                sb.Append($" <small>· {encodedDescription}</small>");
            }
            sb.Append("</div>");
            sb.Append($"<div class=\"meta-when\">📅 עודכן: <strong>{lastUpdatedText}</strong> · ע\"י <strong>{lastUpdatedBy}</strong> <span class=\"meta-tag\">v{result.Version}</span> <span class=\"meta-tag\">Viewer (offline)</span></div>");
            sb.Append("</div>\n");
            sb.Append(body);
            sb.Append("\n<script>\n");
            sb.Append("// Hydrate the SPA from embedded data — no server, no localStorage required.\n");
            sb.Append("window.RoadmapAdmin = {\n");
            sb.Append("  initial: ").Append(initialPayload).Append(",\n");
            // Saving is a no-op in the viewer — show a one-shot warning, then stay quiet.
            sb.Append("  save: function(){ if(!window.__rmViewerWarn){window.__rmViewerWarn=true;alert('זוהי גרסת Viewer offline — שינויים לא יישמרו. כדי לערוך, פתח את ה-Roadmap ב-Admin.');} return Promise.resolve(true); },\n");
            sb.Append("  markDirty: function(){},\n");
            sb.Append("  getVersion: function(){ return ").Append(result.Version).Append("; },\n");
            sb.Append("};\n");
            sb.Append("</script>\n");
            sb.Append("<script>\n").Append(safeJs).Append("\n</script>\n");
            sb.Append("</body></html>\n");

            var fileBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var safeFileName = SanitizeFileName(result.Name ?? "roadmap");
            var fileName = $"roadmap-{safeFileName}-{(result.LastUpdatedAt ?? DateTime.UtcNow):yyyy-MM-dd}.html";

            return File(fileBytes, "text/html; charset=utf-8", fileName);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
