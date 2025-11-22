using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PawfectGrooming.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text.Json;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;
using static System.Runtime.InteropServices.JavaScript.JSType;
using X.PagedList;
using X.PagedList.Extensions;
using X.PagedList; // <-- Add this at the top with other using statements


namespace PawfectGrooming.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserContext db;
        private readonly Helper hp;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<Resources.Resources> Localizer;

        public HomeController(UserContext db, Helper hp, IConfiguration config, IWebHostEnvironment env, IStringLocalizer<Resources.Resources> localizer)
        {
            this.db = db;
            this.hp = hp;
            _config = config;
            _env = env;
            Localizer = localizer;
        }

        // Demo: static in-memory storage. Use a real DB for production!
        private static List<Booking> Appointments = new List<Booking>();

        // --- Main pages ---
        public IActionResult Index()
        {
            // Ensure counts stay in sync before building the view data
            SyncTopSellerCounts();

            var serviceQuery = db.ServiceOptions.AsQueryable();
            var packageQuery = db.Packages.AsQueryable();

            var services = serviceQuery.OrderBy(s => s.Name).ToList();
            var packages = packageQuery.OrderBy(p => p.Name).ToList();

            // Build combined top-sellers list from persisted Count + include description/features
            var sellers = new List<TopSellerVM>();

            foreach (var s in services.Where(x => x.Count > 0))
            {
                // features may be a List<string> or null; join with " + "
                string features = "";
                if (s.Features is IEnumerable<string> listFeatures)
                    features = string.Join(" + ", listFeatures.Where(f => !string.IsNullOrWhiteSpace(f)));
                else if (s.Features != null)
                    features = s.Features.ToString() ?? "";

                sellers.Add(new TopSellerVM
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.Price,
                    ImageUrl = (s.ImageURL != null && s.ImageURL.Any()) ? (s.ImageURL.First() ?? "/images/noimage.png") : "/images/noimage.png",
                    Count = s.Count,
                    ItemType = "Service",
                    Description = s.Description ?? string.Empty,
                    FeaturesFormatted = features
                });
            }

            foreach (var p in packages.Where(x => x.Count > 0))
            {
                string features = "";
                if (p.Features is IEnumerable<string> listFeatures)
                    features = string.Join(" + ", listFeatures.Where(f => !string.IsNullOrWhiteSpace(f)));
                else if (p.Features != null)
                    features = p.Features.ToString() ?? "";

                sellers.Add(new TopSellerVM
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    ImageUrl = (p.ImageURL != null && p.ImageURL.Any()) ? (p.ImageURL.First() ?? "/images/noimage.png") : "/images/noimage.png",
                    Count = p.Count,
                    ItemType = "Package",
                    Description = p.Description ?? string.Empty,
                    FeaturesFormatted = features
                });
            }

            var topAll = sellers.OrderByDescending(x => x.Count).Take(3).ToList();
            ViewBag.TopSellers = topAll;

            // Keep original Index view content (return same View)
            return View();
        }

        // Keep the single Services action (search/sort handled here).
        // GET: Home/Services
        public IActionResult Services(string? name, string? sort, string? dir, decimal? minPrice, decimal? maxPrice)
        {
            if (db == null)
                return View("~/Views/Home/Services.cshtml", new List<ServiceOption>());

            ViewBag.Name = name = name?.Trim() ?? "";
            ViewBag.Sort = sort;
            ViewBag.Dir = dir;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            minPrice ??= 0;
            maxPrice ??= 999.99m;

            if (maxPrice > 999.99m)
            {
                maxPrice = 999.99m;
                ViewBag.MaxPriceWarning = "Maximum price capped at RM 999.99.";
            }

            var serviceQuery = db.ServiceOptions
                .Where(s => s.Price >= minPrice && s.Price <= maxPrice);

            var packageQuery = db.Packages
                .Where(p => p.Price >= minPrice && p.Price <= maxPrice);


            if (!string.IsNullOrEmpty(name))
            {
                serviceQuery = serviceQuery.Where(s => s.Name.Contains(name));
                packageQuery = packageQuery.Where(p => p.Name.Contains(name));
            }

            var services = serviceQuery.OrderBy(s => s.Name).ToList();
            var packages = packageQuery.OrderBy(p => p.Name).ToList();

            // normalize pet type
            services = services.Select(s => { s.PetType = (s.PetType ?? "").Trim(); return s; }).ToList();
            packages = packages.Select(p => { p.PetType = (p.PetType ?? "").Trim(); return p; }).ToList();

            ViewBag.Packages = packages;

            // detect AJAX reliably and return partial only
            var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            if (isAjax)
            {
                return PartialView("_ServiceList", services);
            }

            return View("~/Views/Home/Services.cshtml", services);
        }

        public IActionResult Contact() => View();

        // --- Booking 3-step: Date & Time (1), Pet Info (2), Confirm (3) ---

        // GET: Home/Book
        [HttpGet]
        public IActionResult Book(int? appointmentId, int? serviceId, int? packageId, string date = null, string time = null, int step = 1, string petType = null)
        {
            Booking model = null;

            if (appointmentId.HasValue)
            {
                model = db.Bookings
                    .Include(b => b.Pet)
                    .Include(b => b.Service)
                    .Include(b => b.Package)
                    .FirstOrDefault(b => b.Id == appointmentId.Value);

                // If rescheduling, allow changing date/time if provided in query
                if (model != null)
                {
                    // Allow changing only date/time if provided
                    if (!string.IsNullOrEmpty(date))
                        model.Date = DateTime.Parse(date);
                    if (!string.IsNullOrEmpty(time))
                        model.Time = time;
                }
            }

            //if (TempData.ContainsKey("Booking"))
            //    model = TempData.Get<Booking>("Booking");

            if (model == null && TempData.ContainsKey("Booking"))
                model = TempData.Get<Booking>("Booking");

            if (model == null)
                model = new Booking
                {
                    UserEmail = string.Empty,
                    PetId = 0,
                    ServiceId = null,
                    PackageId = null,
                    Date = DateTime.Today,
                    Time = string.Empty,
                    Price = 0m
                };

            // Autofill for step 1 (service/package selection)
            if (step == 1)
            {
                if (serviceId.HasValue)
                {
                    model.ServiceId = serviceId.Value;
                    model.Service = db.ServiceOptions.FirstOrDefault(s => s.Id == serviceId.Value);
                    model.PackageId = null; // Clear package
                    model.Package = null;
                    if (model.Service != null)
                        model.Price = model.Service.Price;
                }
                if (packageId.HasValue)
                {
                    model.PackageId = packageId.Value;
                    model.Package = db.Packages.FirstOrDefault(p => p.Id == packageId.Value);
                    model.ServiceId = null; // Clear service
                    model.Service = null;
                    if (model.Package != null)
                        model.Price = model.Package.Price;
                }
            }

            // Autofill for step 2 (pet & owner info)
            if (step == 2 && User.Identity.IsAuthenticated)
            {
                var email = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.Email == email);
                var allPets = db.Pets.Where(p => p.Email == email).ToList();
                if (user != null) {
                    // Determine PetType from selected service or package
                    if (model.ServiceId.HasValue)
                    {
                        var service = db.ServiceOptions.FirstOrDefault(s => s.Id == model.ServiceId.Value);
                        petType = service?.PetType;
                    }
                    else if (model.PackageId.HasValue)
                    {
                        var package = db.Packages.FirstOrDefault(p => p.Id == model.PackageId.Value);
                        petType = package?.PetType;
                    }

                    // Filter user's pets based on the selected service/package PetType.
                    // - If PetType is "Dog", only show user's dogs.
                    // - If PetType is "Cat", only show user's cats.
                    // - If PetType is "Cat & Dog", show both dogs and cats.
                    // - Otherwise, show all pets as a fallback.
                    List<Pets> filteredPets;
                    if (string.Equals(petType, "Dog", StringComparison.OrdinalIgnoreCase))
                        filteredPets = allPets.Where(p => p.PetType == "Dog").ToList();
                    else if (string.Equals(petType, "Cat", StringComparison.OrdinalIgnoreCase))
                        filteredPets = allPets.Where(p => p.PetType == "Cat").ToList();
                    else if (string.Equals(petType, "Cat & Dog", StringComparison.OrdinalIgnoreCase))
                        filteredPets = allPets.Where(p => p.PetType == "Dog" || p.PetType == "Cat").ToList();
                    else
                        filteredPets = allPets; // fallback: show all

                    ViewBag.Pets = filteredPets;

                    if (user != null)
                    {
                        model.User = user;
                        model.UserEmail = user.Email;
                        ViewBag.OwnerName = user.Name;
                        ViewBag.OwnerPhone = user.PhoneNumber;
                        ViewBag.OwnerEmail = user.Email;
                    }
                }

                if (step == 3)
                {
                    if (model.ServiceId.HasValue)
                        model.Service = db.ServiceOptions.FirstOrDefault(s => s.Id == model.ServiceId);
                    if (model.PackageId.HasValue)
                        model.Package = db.Packages.FirstOrDefault(p => p.Id == model.PackageId.Value);
                }

                TempData.Set("Booking", model);
                ViewBag.Step = step;
                ViewBag.AppointmentId = appointmentId;
                return View($"BookStep{step}", model);
            }

            // Autofill for step 3 (confirmation)
            if (step == 3)
            {
                if (model.ServiceId.HasValue)
                    model.Service = db.ServiceOptions.FirstOrDefault(s => s.Id == model.ServiceId);
                if (model.PackageId.HasValue)
                    model.Package = db.Packages.FirstOrDefault(p => p.Id == model.PackageId.Value);
            }

            TempData.Set("Booking", model);
            ViewBag.Step = step;
            ViewBag.AppointmentId = appointmentId;
            return View($"BookStep{step}", model); // <-- Ensure this is always reached
        }

        // Step 1: Date & Time
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BookStep1(Booking model, int? appointmentId)
        {
            // Only validate date and time
            if (model.Date == null || string.IsNullOrEmpty(model.Time))
            {
                ModelState.AddModelError("", "Please select a date and time.");
                ViewBag.Step = 1;
                return View("BookStep1", model);
            }
            var tdy = DateTime.Today;
            var now = DateTime.Now;
            if (model.Date < tdy || (model.Date == tdy &&
                DateTime.Parse(model.Time) <= now.AddHours(1)))
            {
                TempData["Error"] = "Pick a time at least 1 hour ahead.";
                ViewBag.Step = 1;
                return View("BookStep1", model);
            }
            if (model.Date.DayOfWeek == DayOfWeek.Sunday)
            {
                TempData["Error"] = "We are closed on Sundays";
                ViewBag.Step = 1;
                return View("BookStep1", model);
            }

            // Check for time slot availability (prevent double booking of same time slot)
            if (!appointmentId.HasValue && User.Identity.IsAuthenticated)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Name);
                var existingBooking = db.Bookings
                    .FirstOrDefault(b => b.Date == model.Date &&
                                       b.Time == model.Time &&
                                       b.IsActive);

                if (existingBooking != null)
                {
                    TempData["Error"] = "This time slot is already booked. Please choose a different time.";
                    ViewBag.Step = 1;
                    return View("BookStep1", model);
                }
            }

            TempData.Set("Booking", model);
            //Check if user is logged in
            if (!User.Identity.IsAuthenticated)
            {
                //Go to login and redirect back to Step 2 after login
                var returnUrl = Url.Action("Book", "Home", new { appointmentId, step = 2 });
                return RedirectToAction("Login", "Account", new { returnUrl });
            }
            return RedirectToAction(nameof(Book), new { appointmentId, step = 2 });
        }

        // Step 2: Pet & Owner Info
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult BookStep2(Booking model, int? appointmentId)
        {
            var form = model; // For clarity
            model = TempData.Get<Booking>("Booking");
            model.Date = form.Date;
            model.Time = form.Time;
            model.PetId = form.PetId;
            if (form.ServiceId.HasValue && form.ServiceId.Value > 0)
            {
                model.ServiceId = form.ServiceId;
            }
            else
            {
                model.ServiceId = null;
            }
            if (form.PackageId.HasValue && form.PackageId.Value > 0)
            {
                model.PackageId = form.PackageId;
            }
            else
            {
                model.PackageId = null;
            }
            var email = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            // Prevent deactivated users from proceeding
            if (user != null && !user.IsActive)
            {
                TempData["Error"] = "Account deactivated.";
                return RedirectToAction("Login", "Account");
            }

            if (model.PetId == 0 || user == null)
            {
                ModelState.AddModelError("", "Please select your pet and make sure you are logged in.");
                ViewBag.Step = 2;
                ViewBag.Pets = db.Pets.Where(p => p.Email == email).ToList();
                return View("BookStep2", model);
            }
            model.User = user;
            model.Pet = db.Pets.FirstOrDefault(p => p.PetId == model.PetId);

            if (model.ServiceId.HasValue && form.ServiceId.Value > 0)
            {
                model.Service = db.ServiceOptions.FirstOrDefault(s => s.Id == model.ServiceId);
            }
            else
            {
                model.ServiceId = null;
            }


            if (model.PackageId.HasValue && form.PackageId.Value > 0)
            {
                model.Package = db.Packages.FirstOrDefault(p => p.Id == model.PackageId.Value);
            }
            else
            {
                model.PackageId = null;
            }

            model.Price = 0;
            if (model.Service != null) model.Price += model.Service.Price;
            if (model.Package != null) model.Price += model.Package.Price;

            TempData.Set("Booking", model);
            return RedirectToAction(nameof(Book), new { appointmentId, step = 3 });
        }

        // Step 3: Confirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BookStep3(Booking model, int? appointmentId)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Name) ?? model.UserEmail;

            // Prevents same users from booking the same pet at the same date and time
            // Check for duplicate booking (same user, pet, date, and time)
            if (!appointmentId.HasValue) // Only check for new bookings, not rescheduling
            {
                var existingBooking = db.Bookings
                    .FirstOrDefault(b => b.UserEmail == userEmail &&
                                       b.PetId == model.PetId &&
                                       b.Date == model.Date &&
                                       b.Time == model.Time &&
                                       b.IsActive);

                if (existingBooking != null)
                {
                    TempData["Error"] = "You already have an appointment for this pet at the same date and time. Please choose a different time slot.";
                    ViewBag.Step = 3;
                    ViewBag.Pets = db.Pets.Where(p => p.Email == userEmail).ToList();
                    return View("BookStep3", model);
                }
            }

            if (model.ServiceId.HasValue && model.ServiceId.Value > 0)
            {
                var svcExists = db.ServiceOptions.Any(s => s.Id == model.ServiceId.Value);
                if (!svcExists)
                {
                    TempData["Error"] = "Selected service not found.";
                    Console.WriteLine("Trying to book with ServiceId: " + model.ServiceId);
                    Console.WriteLine("Trying to book with PackageId: " + model.PackageId);
                    ViewBag.Step = 3;
                    return View("BookStep3", model);
                }
            }
            if (model.PackageId.HasValue && model.PackageId.Value > 0)
            {
                var pkgExists = db.Packages.Any(p => p.Id == model.PackageId.Value);
                if (!pkgExists)
                {
                    TempData["Error"] = "Selected package/Service not found.";
                    Console.WriteLine("Trying to book with ServiceId: " + model.ServiceId);
                    Console.WriteLine("Trying to book with PackageId: " + model.PackageId);
                    ViewBag.Step = 3;
                    return View("BookStep3", model);
                }
            }
            try
            {
                if (model.Id > 0) // Update existing
                {
                    var existing = db.Bookings.FirstOrDefault(b => b.Id == model.Id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Appointment not found.";
                        return RedirectToAction(nameof(AppointmentsList));
                    }

                    existing.Date = model.Date;
                    existing.Time = model.Time;
                    db.SaveChanges();

                    TempData["Info"] = "Appointment rescheduled! ✅";
                }
                else // New appointment
                {
                    var booking = new Booking
                    {
                        UserEmail = userEmail,
                        PetId = model.PetId,
                        ServiceId = model.ServiceId > 0 ? model.ServiceId : null,
                        PackageId = model.PackageId > 0 ? model.PackageId : null,
                        Date = model.Date,
                        Time = model.Time,
                        Price = model.Price,
                        IsActive = true,
                        Status = "PendingPayment",
                    };

                    db.Bookings.Add(booking);
                    db.SaveChanges();

                    // Sync aggregated counts immediately after adding booking
                    SyncTopSellerCounts();

                    TempData["Info"] = "Appointment booked! 🗓️ All set.";
                }

                TempData.Remove("Booking");
                return RedirectToAction(nameof(AppointmentsList));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database save failed: " + (ex.InnerException?.Message ?? ex.Message));
                TempData["Error"] = "Database save failed: " + (ex.InnerException?.Message ?? ex.Message);
                return RedirectToAction(nameof(Book), new { step = 3 });
            }
        }
        public IActionResult AppointmentSuccess() => View();

        public IActionResult AppointmentsList(int? page, string? sort = "Date", string? dir = "asc")
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Name); // current logged in user

            // Base query including related entities used by the view
            var query = db.Bookings
                .Include(b => b.Pet)
                .Include(b => b.Service)
                .Include(b => b.Package)
                .Where(b => b.UserEmail == userEmail && 
                    (b.IsActive || b.Status == "Cancelled"))
                .AsQueryable();

            // Normalize inputs
            var sortKey = (sort ?? "Date").Trim();
            var direction = (dir ?? "asc").ToLowerInvariant();

            // Apply server-side sorting by Status or Date with deterministic tiebreakers
            query = sortKey.ToLowerInvariant() switch
            {
                "status" when direction == "desc" => query.OrderByDescending(b => b.Status).ThenByDescending(b => b.Date).ThenByDescending(b => b.Time),
                "status"                            => query.OrderBy(b => b.Status).ThenBy(b => b.Date).ThenBy(b => b.Time),
                "date"  when direction == "desc"   => query.OrderByDescending(b => b.Date).ThenByDescending(b => b.Time),
                _                                   => query.OrderBy(b => b.Date).ThenBy(b => b.Time)
            };

            // Paging (match page param from view). Adjust pageSize as needed.
            int pageNumber = page ?? 1;
            const int pageSize = 10;

            ViewBag.Sort = sortKey;
            ViewBag.Dir = direction;

            var paged = query.ToPagedList(pageNumber, pageSize); // This now works with the correct using
            return View(paged);
        }


        // Payment
        [HttpGet]
        public IActionResult Payment()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Payment(Payment payment)
        {
            if (ModelState.IsValid)
            {
                payment.Date = DateTime.Now;
                payment.Status = "Pending";

                db.Payment.Add(payment);
                db.SaveChanges();

                return RedirectToAction("PaymentSuccess");
            }
            return View(payment);
        }
        public IActionResult PaymentSuccess()
        {
            return View();
        }

        // GET: /Appointment/Reschedule/
        [HttpGet]
        public IActionResult Reschedule(int id)

        {
            var booking = db.Bookings.FirstOrDefault(b => b.Id == id && b.IsActive);
            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToAction("AppointmentsList");
            }
            return View(booking);
        }

        // POST: /Appointment/Reschedule/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reschedule(Booking model)
        {
            // Only validate date and time
            if (model.Date == null || string.IsNullOrEmpty(model.Time))
            {
                ModelState.AddModelError("", "Please select a date and time.");
                ViewBag.Step = 1;
                return View(model);
            }
            var tdy = DateTime.Today;
            var now = DateTime.Now;
            DateTime selectedDateTime;
            if (!DateTime.TryParseExact(model.Time, "h:mm tt", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out selectedDateTime))
            {
                TempData["Error"] = "Invalid time format.";
                ViewBag.Step = 1;
                return View(model);
            }
            if (model.Date < tdy || (model.Date == tdy &&
                selectedDateTime <= now.AddHours(1)))
            {
                TempData["Error"] = "Pick a time at least 1 hour ahead.";
                ViewBag.Step = 1;
                return View(model);
            }
            if (model.Date.DayOfWeek == DayOfWeek.Sunday)
            {
                TempData["Error"] = "We are closed on Sundays";

                ViewBag.Step = 1;
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine("Validation errors: " + string.Join("; ", errors));
                TempData["Error"] = "Invalid input: " + string.Join("; ", errors);
                return View(model);
            }

            var booking = db.Bookings.FirstOrDefault(b => b.Id == model.Id && b.IsActive);
            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToAction("AppointmentsList");
            }

            booking.Date = model.Date;
            booking.Time = model.Time;
            db.SaveChanges();

            TempData["Info"] = "Booking rescheduled!";
            return RedirectToAction("AppointmentsList");
        }

        [HttpPost]
        public IActionResult Cancel(int id)
        {
            var booking = db.Bookings.FirstOrDefault(b => b.Id == id);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = "Cancelled"; // <-- Set status instead of removing
            booking.IsActive = false;     // Optionally mark as inactive
            db.SaveChanges();

            SyncTopSellerCounts();

            TempData["Info"] = "Appointment cancelled!";
            return RedirectToAction(nameof(AppointmentsList)); // back to the appointment list
        }

        // Sync aggregated booking counts into ServiceOptions.Count and Packages.Count
        // (counts how many bookings reference each ServiceId / PackageId)
        private void SyncTopSellerCounts()
        {
            // Aggregate by ServiceId (count occurrences)
            var svcCounts = db.Bookings
                .Where(b => b.ServiceId != null)
                .GroupBy(b => b.ServiceId)
                .Select(g => new { Id = g.Key!.Value, Count = g.Count() })
                .ToDictionary(x => x.Id, x => x.Count);

            // Aggregate by PackageId (count occurrences)
            var pkgCounts = db.Bookings
                .Where(b => b.PackageId != null)
                .GroupBy(b => b.PackageId)
                .Select(g => new { Id = g.Key!.Value, Count = g.Count() })
                .ToDictionary(x => x.Id, x => x.Count);

            // Update ServiceOptions.Count
            var services = db.ServiceOptions.ToList();
            var svcUpdated = false;
            foreach (var s in services)
            {
                var newCount = svcCounts.TryGetValue(s.Id, out var c) ? c : 0;
                if (s.Count != newCount)
                {
                    s.Count = newCount;
                    svcUpdated = true;
                }
            }

            // Update Packages.Count
            var packages = db.Packages.ToList();
            var pkgUpdated = false;
            foreach (var p in packages)
            {
                var newCount = pkgCounts.TryGetValue(p.Id, out var c) ? c : 0;
                if (p.Count != newCount)
                {
                    p.Count = newCount;
                    pkgUpdated = true;
                }
            }

            if (svcUpdated || pkgUpdated)
            {
                db.SaveChanges();
            }
        }

    }

    public static class TempDataExtensions
    {
        public static void Set<T>(this ITempDataDictionary tempData, string key, T value)
        {
            tempData[key] = System.Text.Json.JsonSerializer.Serialize(value);
        }

        public static T Get<T>(this ITempDataDictionary tempData, string key)
        {
            if (tempData.TryGetValue(key, out var o) && o is string s)
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<T>(s)!;
                }
                catch (JsonException)
                {
                    // Value in TempData wasn't valid JSON for the requested type.
                    // Return default(T) rather than throwing to avoid breaking the request pipeline.
                    return default!;
                }
            }
            return default!;
        }

        public static IActionResult Status(UserContext db, int? year, int? month)
        {
            var today = DateTime.Today;
            int y = year ?? today.Year;
            int m = month ?? today.Month;

            var monthStart = new DateTime(y, m, 1);
            var monthEnd = monthStart.AddMonths(1); // exclusive

            // load bookings that fall inside the month (single-day bookings in this app)
            var bookings = db.Bookings
                .Include(b => b.User)
                .Include(b => b.Pet)
                .Include(b => b.Service)
                .Include(b => b.Package)
                .Where(b => b.IsActive && b.Date >= monthStart && b.Date < monthEnd)
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

            // FIX: Use new ViewResult and specify the model
            return new ViewResult
            {
                ViewName = "Status",
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<StatusViewModel>(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                { Model = vm }
            };
        }
    }
}