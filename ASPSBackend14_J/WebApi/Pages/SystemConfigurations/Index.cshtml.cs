using Business.Commands;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApi.Services;

namespace WebApi.Pages.SystemConfigurations
{
    public class IndexModel : PageModel
    {
        private readonly ICQRSClient _cqrsClient;

        public IndexModel(ICQRSClient cqrsClient)
        {
            _cqrsClient = cqrsClient;
        }

        [TempData]
        public string? StatusMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostInitializeASView()
        {
            try
            {
                var command = new ReInitializeASViewCommand();
                var result = await _cqrsClient.SendCommandAsync<ReInitializeASViewCommandResult>(command);

                if (result.Success)
                {
                    StatusMessage = result.Message;
                }
                else
                {
                    StatusMessage = $"Error: {result.Message}";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
