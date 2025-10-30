using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public class HolidayService : IHolidayService
    {
        private readonly SCADADbContext _context;
        private readonly ILogger<HolidayService> _logger;

        public HolidayService(SCADADbContext context, ILogger<HolidayService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> IsSabbaticalHolidayAsync(DateTime? date = null)
        {
            try
            {
                var checkDate = date ?? DateTime.Now;
                var dateOnly = checkDate.Date;

                var dateInfo = await _context.DateDimensions
                    .FirstOrDefaultAsync(dd => dd.FullDate.Date == dateOnly);

                if (dateInfo != null)
                {
                    bool isSabbatical = dateInfo.IsSabbaticalHoliday;
                    _logger.LogInformation("Date {Date} sabbatical status: {IsSabbatical}", dateOnly, isSabbatical);
                    return isSabbatical;
                }

                // If no data found, check if it's Saturday (Sabbath)
                bool isSaturday = checkDate.DayOfWeek == DayOfWeek.Saturday;
                _logger.LogInformation("No date dimension data for {Date}, checking Saturday: {IsSaturday}", dateOnly, isSaturday);
                return isSaturday;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if date {Date} is sabbatical holiday", date);
                // Default to false to avoid blocking SMS notifications
                return false;
            }
        }

        public async Task<DateDimension?> GetDateInfoAsync(DateTime date)
        {
            try
            {
                return await _context.DateDimensions
                    .FirstOrDefaultAsync(dd => dd.FullDate.Date == date.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting date info for {Date}", date);
                return null;
            }
        }

        public async Task InitializeDateDimensionAsync(int yearsAhead = 10)
        {
            try
            {
                // Check if date dimension already has data
                var existingCount = await _context.DateDimensions.CountAsync();
                if (existingCount > 0)
                {
                    _logger.LogInformation("Date dimension already has {Count} records, skipping initialization", existingCount);
                    return;
                }

                _logger.LogInformation("Initializing date dimension for {Years} years ahead", yearsAhead);
                
                var startDate = new DateTime(DateTime.Now.Year, 1, 1);
                var endDate = startDate.AddYears(yearsAhead);
                var dateRecords = new List<DateDimension>();

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var dateRecord = new DateDimension
                    {
                        FullDate = date,
                        DayOfWeek = (byte)((int)date.DayOfWeek + 1), // SQL Server: 1=Sunday, 7=Saturday
                        DayName = date.ToString("dddd"),
                        DayOfMonth = (byte)date.Day,
                        DayOfYear = (short)date.DayOfYear,
                        WeekOfYear = (byte)GetWeekOfYear(date),
                        Month = (byte)date.Month,
                        MonthName = date.ToString("MMMM"),
                        Quarter = (byte)((date.Month - 1) / 3 + 1),
                        Year = (short)date.Year,
                        IsWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday,
                        IsJewishHoliday = IsJewishHoliday(date),
                        IsSabbaticalHoliday = IsSabbaticalHoliday(date)
                    };

                    // Set Hebrew date and holiday name if it's a Jewish holiday
                    if (dateRecord.IsJewishHoliday)
                    {
                        dateRecord.JewishHoliday = GetJewishHolidayName(date);
                        dateRecord.HebrewDate = GetHebrewDate(date);
                    }

                    dateRecords.Add(dateRecord);
                }

                // Batch insert for performance
                await _context.DateDimensions.AddRangeAsync(dateRecords);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully initialized {Count} date dimension records", dateRecords.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing date dimension");
                throw;
            }
        }

        private static int GetWeekOfYear(DateTime date)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            return culture.Calendar.GetWeekOfYear(date, culture.DateTimeFormat.CalendarWeekRule, culture.DateTimeFormat.FirstDayOfWeek);
        }

        private static bool IsJewishHoliday(DateTime date)
        {
            // Basic implementation - in production, you would use a proper Hebrew calendar library
            // This is a simplified version that includes major Jewish holidays
            
            var year = date.Year;
            var holidayDates = GetJewishHolidays(year);
            
            return holidayDates.Any(hd => hd.Date.Date == date.Date);
        }

        private static bool IsSabbaticalHoliday(DateTime date)
        {
            // Saturday is always sabbatical
            if (date.DayOfWeek == DayOfWeek.Saturday)
                return true;

            // Major Jewish holidays with sabbatical restrictions
            var year = date.Year;
            var sabbaticalHolidays = GetSabbaticalJewishHolidays(year);
            
            return sabbaticalHolidays.Any(hd => hd.Date.Date == date.Date);
        }

        private static string? GetJewishHolidayName(DateTime date)
        {
            var year = date.Year;
            var holidayDates = GetJewishHolidays(year);
            
            var holiday = holidayDates.FirstOrDefault(hd => hd.Date.Date == date.Date);
            return holiday.Name;
        }

        private static string? GetHebrewDate(DateTime date)
        {
            // Basic implementation - in production, you would use a proper Hebrew calendar conversion
            // This is a placeholder that returns a formatted string
            try
            {
                // Simple approximation - this should be replaced with proper Hebrew calendar calculation
                var hebrewYear = date.Year + 3760; // Rough approximation
                return $"{date.Day} {GetHebrewMonth(date.Month)} {hebrewYear}";
            }
            catch
            {
                return null;
            }
        }

        private static string GetHebrewMonth(int gregorianMonth)
        {
            // Approximate Hebrew month names - this is a simplified mapping
            var hebrewMonths = new[]
            {
                "Tevet", "Shevat", "Adar", "Nisan", "Iyar", "Sivan",
                "Tammuz", "Av", "Elul", "Tishrei", "Cheshvan", "Kislev"
            };

            return hebrewMonths[(gregorianMonth - 1) % 12];
        }

        private static IEnumerable<(DateTime Date, string Name)> GetJewishHolidays(int year)
        {
            // Basic approximation of major Jewish holidays
            // In production, use a proper Hebrew calendar library like Zmanim.NET
            
            var holidays = new List<(DateTime, string)>();

            try
            {
                // Approximate dates - these should be calculated using proper Hebrew calendar
                // These are rough approximations and should be replaced with accurate calculations
                
                // Rosh Hashanah (varies each year)
                holidays.Add((new DateTime(year, 9, 15), "Rosh Hashanah")); // Approximate
                holidays.Add((new DateTime(year, 9, 16), "Rosh Hashanah"));
                
                // Yom Kippur (10 days after Rosh Hashanah)
                holidays.Add((new DateTime(year, 9, 24), "Yom Kippur")); // Approximate
                
                // Sukkot
                holidays.Add((new DateTime(year, 9, 29), "Sukkot")); // Approximate
                holidays.Add((new DateTime(year, 10, 5), "Hoshana Rabbah"));
                holidays.Add((new DateTime(year, 10, 6), "Shemini Atzeret"));
                holidays.Add((new DateTime(year, 10, 7), "Simchat Torah"));
                
                // Passover (Spring)
                holidays.Add((new DateTime(year, 4, 15), "Passover")); // Approximate
                holidays.Add((new DateTime(year, 4, 16), "Passover"));
                holidays.Add((new DateTime(year, 4, 21), "Passover"));
                holidays.Add((new DateTime(year, 4, 22), "Passover"));
                
                // Shavut
                holidays.Add((new DateTime(year, 6, 4), "Shavut")); // Approximate
                holidays.Add((new DateTime(year, 6, 5), "Shavut"));
            }
            catch (Exception)
            {
                // If date calculation fails, return empty list
            }

            return holidays;
        }

        private static IEnumerable<(DateTime Date, string Name)> GetSabbaticalJewishHolidays(int year)
        {
            // Return only holidays with sabbatical restrictions (no work allowed)
            var allHolidays = GetJewishHolidays(year);
            
            var sabbaticalHolidays = new[]
            {
                "Rosh Hashanah", "Yom Kippur", "Passover", "Shavut", 
                "Shemini Atzeret", "Simchat Torah"
            };

            return allHolidays.Where(h => sabbaticalHolidays.Contains(h.Name));
        }
    }
}