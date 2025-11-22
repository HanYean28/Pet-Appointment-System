using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace YourAppNamespace.Pages.Product
{
    [Authorize(Roles = "Admin,User")]
    public class StatusModel : PageModel
    {
        private readonly IWebHostEnvironment _env;
        public StatusModel(IWebHostEnvironment env) => _env = env;

        public record ReservationDto(string Id, string Name, string Email, string Room, DateTime StartDate, DateTime EndDate);

        public class RoomRow
        {
            public string RoomId { get; init; } = "";
            public List<bool> Occupied { get; init; } = new();
        }

        public int Year { get; private set; }
        public int Month { get; private set; }
        public string MonthName => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);
        public int DaysInMonth { get; private set; }

        // Define rooms here (adjust or load from DB)
        public List<string> Rooms { get; private set; } = new() { "R001 (S)", "R002 (S)", "R003 (D)", "R004 (D)" };
        public List<RoomRow> Rows { get; private set; } = new();
        public List<ReservationDto> Reservations { get; private set; } = new();

        public async Task OnGetAsync(int? year, int? month)
        {
            var now = DateTime.UtcNow;
            Year = year ?? now.Year;
            Month = month ?? now.Month;
            DaysInMonth = DateTime.DaysInMonth(Year, Month);

            // initialize rows
            Rows = Rooms.Select(r => new RoomRow { RoomId = r, Occupied = Enumerable.Repeat(false, DaysInMonth).ToList() }).ToList();

            // try read reservations from wwwroot/data/reservations.json (optional)
            var file = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "reservations.json");
            if (System.IO.File.Exists(file))
            {
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var list = JsonSerializer.Deserialize<List<ReservationDto>>(json, options);
                    if (list != null) Reservations = list;
                }
                catch
                {
                    // ignore parse errors and continue with demo data
                }
            }

            // fallback demo data when none present
            if (!Reservations.Any())
            {
                Reservations = new List<ReservationDto>
                {
                    new("1","Lalisa Manoban","m1@gmail.com","R001 (S)", new DateTime(2024,12,10), new DateTime(2024,12,15)),
                    new("2","Lalisa Manoban","m1@gmail.com","R002 (S)", new DateTime(2024,12,21), new DateTime(2024,12,26)),
                    new("3","Joe Example","joe@ex.com","R003 (D)", new DateTime(2024,12,28), new DateTime(2025,1,3)),
                };
            }

            // mark occupied days (Start inclusive, End exclusive)
            var monthStart = new DateTime(Year, Month, 1);
            var monthEnd = monthStart.AddMonths(1); // exclusive
            foreach (var r in Reservations)
            {
                var resStart = r.StartDate.Date;
                var resEnd = r.EndDate.Date;

                var overlapStart = resStart < monthStart ? monthStart : resStart;
                var overlapEnd = resEnd > monthEnd ? monthEnd : resEnd;

                if (overlapStart >= overlapEnd) continue;

                var row = Rows.FirstOrDefault(x => string.Equals(x.RoomId, r.Room, StringComparison.OrdinalIgnoreCase));
                if (row == null) continue;

                for (var d = overlapStart; d < overlapEnd; d = d.AddDays(1))
                {
                    var idx = (d - monthStart).Days;
                    if (idx >= 0 && idx < DaysInMonth) row.Occupied[idx] = true;
                }
            }
        }
    }
}