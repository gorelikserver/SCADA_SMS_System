using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SCADASMSSystem.Web.Pages
{
    public class AboutModel : PageModel
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AboutModel> _logger;

        public AboutModel(IWebHostEnvironment environment, ILogger<AboutModel> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public string Environment => _environment.EnvironmentName;

        public void OnGet()
        {
            _logger.LogInformation("About page accessed");
        }
    }
}