using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PawfectGrooming.Models;

namespace PawfectGrooming.Controllers
{
    [Authorize(Roles = "Admin,Member")]
    public class ProductController : Controller
    {
        private readonly UserContext db;
        private readonly IStringLocalizer<Resources.Resources> Localizer;

        public ProductController(UserContext context, IStringLocalizer<Resources.Resources> localizer)
        {
            db = context;
            Localizer = localizer;
        }

        // GET: /Product/Status
        public IActionResult Status(int? year, int? month)
        {
            var today = DateTime.Today;
            int y = year ?? today.Year;
            int m = month ?? today.Month;

            var monthStart = new DateTime(y, m, 1);
            var monthEnd = monthStart.AddMonths(1); // exclusive

            // base query: bookings that fall inside the month (single-day bookings in this app)
            var bookingsQuery = db.Bookings
                .Include(b => b.User)
                .Include(b => b.Pet)
                .Include(b => b.Service)
                .Include(b => b.Package)
                .Where(b => b.IsActive && b.Date >= monthStart && b.Date < monthEnd);

            // If the current user is NOT an Admin, restrict to their own bookings only.
            if (!User.IsInRole("Admin"))
            {
                var currentUserEmail = (User.Identity?.Name ?? string.Empty).ToLowerInvariant();
                bookingsQuery = bookingsQuery.Where(b => b.UserEmail != null && b.UserEmail.ToLower() == currentUserEmail);
            }

            var bookings = bookingsQuery
                .OrderBy(b => b.Date)
                .ThenBy(b => b.Time)
                .ToList();

            int daysInMonth = DateTime.DaysInMonth(y, m);

            // build day -> list of slot strings (time + short owner)
            var daySlots = new Dictionary<int, List<string>>();
            for (int d = 1; d <= daysInMonth; d++) daySlots[d] = new List<string>();

            foreach (var bk in bookings)
            {
                var day = bk.Date.Day;
                var owner = bk.User?.Name ?? bk.UserEmail;
                var display = $"{bk.Time} ({owner})";
                daySlots.TryGetValue(day, out var list);
                list ??= new List<string>();
                list.Add(display);
                daySlots[day] = list;
            }

            var vm = new StatusViewModel
            {
                Year = y,
                Month = m,
                MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m),
                DaysInMonth = daysInMonth,
                DaySlots = daySlots,
                Bookings = bookings
            };

            return View(vm);
        }

        public class StatusViewModel
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public string MonthName { get; set; } = "";
            public int DaysInMonth { get; set; }
            public Dictionary<int, List<string>> DaySlots { get; set; } = new();
            public List<Booking> Bookings { get; set; } = new();
        }
    }
}