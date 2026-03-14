using Business.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebApi.Pages.SystemConfigurations
{
    public class IndexModel : PageModel
    {
        private readonly ASView _asView;

        public IndexModel(ASView asView)
        {
            _asView = asView;
        }

        [TempData]
        public string? StatusMessage { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPostInitializeASView()
        {
            try
            {
                _asView.ReInitialize();
                StatusMessage = "ASView re-initialized successfully!";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
