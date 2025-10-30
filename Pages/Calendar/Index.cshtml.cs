using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Calendar
{
    public class IndexModel : PageModel
    {
        private readonly IHolidayService _holidayService;
        private readonly IUserService _userService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IHolidayService holidayService, IUserService userService, ILogger<IndexModel> logger)
        {
            _holidayService = holidayService;
            _userService = userService;
            _logger = logger;
        }

        public IEnumerable<HolidayDisplayModel> UpcomingHolidays { get; set; } = new List<HolidayDisplayModel>();
        public HolidayStatistics Statistics { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public int DaysAhead { get; set; } = 90; // Default to next 90 days
        
        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "upcoming"; // upcoming, all, sabbatical

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading calendar page - Days ahead: {DaysAhead}, View mode: {ViewMode}", DaysAhead, ViewMode);

                await LoadHolidayDataAsync();
                await LoadStatisticsAsync();

                _logger.LogInformation("Loaded {Count} holiday records", UpcomingHolidays.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading calendar data");
                UpcomingHolidays = new List<HolidayDisplayModel>();
                TempData["ErrorMessage"] = "Error loading calendar data. Please try again.";
            }
        }

        public async Task<IActionResult> OnPostRefreshCalendarAsync()
        {
            try
            {
                // Initialize/refresh the date dimension for the next 10 years
                await _holidayService.InitializeDateDimensionAsync(10);
                TempData["SuccessMessage"] = "Holiday calendar has been refreshed successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing calendar");
                TempData["ErrorMessage"] = "Failed to refresh calendar data.";
            }

            return RedirectToPage();
        }

        private async Task LoadHolidayDataAsync()
        {
            var holidays = new List<HolidayDisplayModel>();
            var startDate = DateTime.Now.Date;
            var endDate = startDate.AddDays(DaysAhead);

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dateInfo = await _holidayService.GetDateInfoAsync(date);
                var isSabbaticalHoliday = await _holidayService.IsSabbaticalHolidayAsync(date);

                // Only include special days based on view mode
                bool shouldInclude = ViewMode.ToLower() switch
                {
                    "sabbatical" => isSabbaticalHoliday,
                    "upcoming" => isSabbaticalHoliday || date.DayOfWeek == DayOfWeek.Saturday || (dateInfo?.JewishHoliday != null),
                    "all" => true,
                    _ => isSabbaticalHoliday || date.DayOfWeek == DayOfWeek.Saturday || (dateInfo?.JewishHoliday != null)
                };

                if (shouldInclude)
                {
                    holidays.Add(new HolidayDisplayModel
                    {
                        Date = date,
                        DayName = date.ToString("dddd"),
                        HebrewDate = dateInfo?.HebrewDate ?? "",
                        JewishHoliday = dateInfo?.JewishHoliday ?? (date.DayOfWeek == DayOfWeek.Saturday ? "Sabbath" : ""),
                        IsSabbaticalHoliday = isSabbaticalHoliday,
                        IsRegularHoliday = !string.IsNullOrEmpty(dateInfo?.JewishHoliday) && !isSabbaticalHoliday,
                        IsSaturday = date.DayOfWeek == DayOfWeek.Saturday && string.IsNullOrEmpty(dateInfo?.JewishHoliday)
                    });
                }
            }

            UpcomingHolidays = holidays.OrderBy(h => h.Date);
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var allUsers = await _userService.GetAllUsersAsync();
                var startDate = DateTime.Now.Date;
                var endDate = startDate.AddDays(DaysAhead);

                var sabbaticalCount = 0;
                var regularHolidayCount = 0;
                var saturdayCount = 0;

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var isSabbatical = await _holidayService.IsSabbaticalHolidayAsync(date);
                    var dateInfo = await _holidayService.GetDateInfoAsync(date);

                    if (isSabbatical)
                        sabbaticalCount++;
                    else if (!string.IsNullOrEmpty(dateInfo?.JewishHoliday))
                        regularHolidayCount++;
                    else if (date.DayOfWeek == DayOfWeek.Saturday)
                        saturdayCount++;
                }

                Statistics = new HolidayStatistics
                {
                    TotalUsers = allUsers.Count(),
                    HolidayWorkers = allUsers.Count(u => u.SpecialDaysEnabled),
                    SabbaticalDays = sabbaticalCount,
                    RegularHolidays = regularHolidayCount,
                    Saturdays = saturdayCount,
                    DaysScanned = DaysAhead
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading statistics");
                Statistics = new HolidayStatistics();
            }
        }
    }

    public class HolidayDisplayModel
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string HebrewDate { get; set; } = string.Empty;
        public string JewishHoliday { get; set; } = string.Empty;
        public bool IsSabbaticalHoliday { get; set; }
        public bool IsRegularHoliday { get; set; }
        public bool IsSaturday { get; set; }

        public string GetRestrictionType()
        {
            if (IsSabbaticalHoliday) return "Restricted";
            if (IsRegularHoliday || IsSaturday) return "Observance";
            return "Regular";
        }

        public string GetBadgeClass()
        {
            if (IsSabbaticalHoliday) return "badge-warning";
            if (IsRegularHoliday || IsSaturday) return "badge-info";
            return "badge-secondary";
        }
    }

    public class HolidayStatistics
    {
        public int TotalUsers { get; set; }
        public int HolidayWorkers { get; set; }
        public int SabbaticalDays { get; set; }
        public int RegularHolidays { get; set; }
        public int Saturdays { get; set; }
        public int DaysScanned { get; set; }

        public double HolidayWorkerPercentage => TotalUsers > 0 ? (double)HolidayWorkers / TotalUsers * 100 : 0;
    }
}