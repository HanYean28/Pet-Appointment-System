using iText.StyledXmlParser.Jsoup.Parser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using UAParser;
using static System.Net.WebRequestMethods;
using PawfectGrooming.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;



namespace PawfectGrooming;

public class Helper
{
    private readonly IWebHostEnvironment en;
    private readonly IHttpContextAccessor ct;
    private readonly IConfiguration _config;
    private readonly UserContext db;

    public Helper(IWebHostEnvironment en, IHttpContextAccessor ct, IConfiguration config, UserContext db)
    {
        this.en = en;
        this.ct = ct;
        this._config = config;
        this.db = db;
    }

    // ------------------------------------------------------------------------
    // Photo Upload
    // ------------------------------------------------------------------------

    public string ValidatePhoto(IFormFile f)
    {
        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG and PNG photo is allowed.";
        }
        else if (f.Length > 1 * 1024 * 1024)
        {
            return "Photo size cannot more than 1MB.";
        }

        return "";
    }

    public string SavePhoto(IFormFile f, string folder)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, folder, file);

        var options = new ResizeOptions
        {
            Size = new(200, 200),
            Mode = ResizeMode.Crop,
        };

        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.Resize(options));
        img.Save(path);

        return file;
    }

    public void DeletePhoto(string file, string folder)
    {
        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, folder, file);
        System.IO.File.Delete(path);
    }



    // ------------------------------------------------------------------------
    // Security Helper Functions
    // ------------------------------------------------------------------------

    // TODO
    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        // TODO

        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        // TODO
        return ph.VerifyHashedPassword(0, hash, password) ==
            PasswordVerificationResult.Success;
    }

    public async Task SignIn(string email, string role, bool rememberMe)
    {
        // (1) Claim, identity and principal
        List<Claim> claims = [
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Role, role)
            ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        // (2) Remember me (authentication properties)
        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(1) : null
        };

        // (3) Sign in
        await ct.HttpContext!.SignInAsync("Cookies", principal, properties);
    }

    public void SignOut()
    {
        // Sign out
        ct.HttpContext!.SignOutAsync();
    }

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string password = "";

        Random r = new();

        for (int i = 1; i <= 10; i++)
        {
            password += s[r.Next(s.Length)];
        }
        ;

        return password;
    }
    // User Agent Parser

    public static string ParseUserAgent(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "Unknown Device";

        var uaParser = UAParser.Parser.GetDefault(); ;
        ClientInfo client = uaParser.Parse(userAgent);

        string browser = client.UA.Family + " " + client.UA.Major;
        string os = client.OS.Family + " " + client.OS.Major;

        // Optionally add Device Family (for mobile/tablet)
        string device = client.Device.Family;
        string deviceStr = !string.IsNullOrEmpty(device) && device != "Other"
            ? $"{device}, "
            : "";

        return $"{browser} on {deviceStr}{os}";
    }
    //Gender Options
    public IEnumerable<SelectListItem> GetGenderOptions(IStringLocalizer Localizer)
    {
        return new List<SelectListItem>
    {
        new SelectListItem { Value = "Male", Text = Localizer["Male"] },
        new SelectListItem { Value = "Female", Text = Localizer["Female"] }
    };
    }
    //PetType Options(Service and Package)
    public IEnumerable<SelectListItem> GetPetTypeOptions(IStringLocalizer Localizer)
    {
        return new List<SelectListItem>
    {
        new SelectListItem { Value = "Dog", Text = Localizer["Dog"] },
        new SelectListItem { Value = "Cat", Text = Localizer["Cat"] },
        new SelectListItem { Value = "Cat & Dog", Text = Localizer["Cat & Dog"] }
    };
    }
    //PetType Options(Pet)
    public IEnumerable<SelectListItem> GetPetTypeOptionsForPet(IStringLocalizer Localizer)
    {
        return new List<SelectListItem>
    {
        new SelectListItem { Value = "Dog", Text = Localizer["Dog"] },
        new SelectListItem { Value = "Cat", Text = Localizer["Cat"] },
        new SelectListItem { Value = "Other", Text = Localizer["Other"] }
    };
    }
    // SMS Features 
    public static void SendSms(string toPhoneNumber, string messageBody)
    {
        // Twilio credentials
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        string accountSid = config["Twilio:AccountSid"];
        string authToken = config["Twilio:AuthToken"];
        string fromPhoneNumber = config["Twilio:FromPhoneNumber"];

        TwilioClient.Init(accountSid, authToken);

        var message = MessageResource.Create(
            to: new PhoneNumber(toPhoneNumber),
            from: new PhoneNumber(fromPhoneNumber),
            body: messageBody);

        Console.WriteLine($"SMS sent! Message SID: {message.Sid}");
    }

    // Temporary User Store 
    public static class TemporaryLoginConfig
    {
        public static string AllowedMemberToken =>
            new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["TempLogin:MemberKey"];

        public static string AllowedAdminToken =>
            new ConfigurationBuilder().AddJsonFile("appsettings.json").Build()["TempLogin:AdminKey"];
    }
    // In-memory store for temporary users
    public static class TemporaryUserStore
    {
        private static ConcurrentDictionary<string, (User user, DateTime expiry)> _users = new();

        public static void Add(string token, User user, DateTime expiry)
        {
            _users[token] = (user, expiry);
        }

        public static User? Get(string token)
        {
            if (_users.TryGetValue(token, out var value))
            {
                if (DateTime.UtcNow < value.expiry)
                    return value.user;
                else
                    _users.TryRemove(token, out _); // Remove expired user
            }
            return null;
        }
        public static void Remove(string token, UserContext db)
        {
            if (_users.TryRemove(token, out var value))
            {
                if (value.user.IsTemporary)
                {
                    var dbUser = db.Users.FirstOrDefault(u => u.Email == value.user.Email);
                    if (dbUser != null)
                    {
                        db.Users.Remove(dbUser);
                        db.SaveChanges();
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------
    // Vouchers & Points
    // ------------------------------------------------------------------------

    // Generate Voucher Code
    public static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = Random.Shared;
        return new string(Enumerable.Range(0, 10).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
    }

    // Try to apply a voucher to a given booking for a specific owner email; stores it in Session if valid
    public (bool Ok, string Message) TryApplyVoucherToBooking(int bookingId, string code, string ownerEmail)
    {
        var ctx = ct.HttpContext;
        if (ctx is null) return (false, "No HTTP context.");

        if (string.IsNullOrWhiteSpace(code)) return (false, "Enter a voucher code.");

        var normalized = code.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var voucher = db.Voucher
            .Where(v => v.Code.ToUpper() == normalized)
            .Where(v => v.Email == ownerEmail)
            .Where(v => !v.IsUsed)
            .Where(v => v.ExpiryDate >= now)
            .FirstOrDefault();

        if (voucher is null)
            return (false, "Invalid, expired, used, or not your voucher.");

        // Store with invariant formatting (safer across locales)
        ctx.Session.SetInt32("VoucherId", voucher.Id);
        ctx.Session.SetString("VoucherCode", voucher.Code);
        ctx.Session.SetString("VoucherAmount", voucher.DiscountAmount.ToString(CultureInfo.InvariantCulture));
        ctx.Session.SetInt32("VoucherBookingId", bookingId);

        return (true, $"Voucher applied to booking #{bookingId}: -RM{voucher.DiscountAmount:0.00}.");
    }

    // Redeem points for a new voucher
    public (bool Ok, string Message, Voucher? Voucher) RedeemVoucher(string ownerEmail, int costPoints = 100, decimal discountAmount = 5.00m, int expiryMonths = 6)
    {
        var user = db.Users.Find(ownerEmail);
        if (user is null) return (false, "User not found.", null);

        if (user.Points < costPoints)
            return (false, $"Not enough points. Need {costPoints}.", null);

        user.Points -= costPoints;

        // Ensure code uniqueness with a short loop
        string code;
        int attempts = 0;
        do
        {
            code = GenerateCode();
            attempts++;
            if (attempts > 20) return (false, "Failed to generate code. Try again.", null);
        } while (db.Voucher.Any(v => v.Code == code));

        var voucher = new Voucher
        {
            Code = code,
            Email = ownerEmail,
            DiscountAmount = discountAmount,
            ExpiryDate = DateTime.UtcNow.AddMonths(expiryMonths),
            IsUsed = false
        };

        db.Voucher.Add(voucher);
        db.SaveChanges();

        return (true, $"Redeemed voucher {voucher.Code} (RM{voucher.DiscountAmount:0.00}). Expires {voucher.ExpiryDate:yyyy-MM-dd}.", voucher);
    }

    // Read an applied voucher from Session and revalidate against DB
    public (int? VoucherId, decimal Discount, string Code) GetAppliedVoucherFor(int bookingId, string ownerEmail)
    {
        var ctx = ct.HttpContext;
        if (ctx is null) return (null, 0m, "");

        var sessBookingId = ctx.Session.GetInt32("VoucherBookingId");
        if (sessBookingId != bookingId) return (null, 0m, "");

        var code = ctx.Session.GetString("VoucherCode");
        var amountStr = ctx.Session.GetString("VoucherAmount");
        var voucherId = ctx.Session.GetInt32("VoucherId");
        if (voucherId is null || string.IsNullOrWhiteSpace(code)) return (null, 0m, "");

        // Parse using invariant culture (in case you need the parsed value)
        if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var discount))
            discount = 0m;

        // Validate ownership and state in DB
        var v = db.Voucher.FirstOrDefault(x => x.Id == voucherId.Value && x.Email == ownerEmail && !x.IsUsed && x.ExpiryDate >= DateTime.UtcNow);
        if (v is null) return (null, 0m, "");

        // Return canonical discount from DB
        return (voucherId, v.DiscountAmount, v.Code);
    }

    // Consume the session voucher bound to this booking and award points
    public void ConsumeVoucherAndAwardPoints(int bookingId, string ownerEmail, string? paymentType, decimal amountCharged)
    {
        var ctx = ct.HttpContext;
        if (ctx is null) return;

        // If deposit, keep voucher for the final payment; no points awarded now.
        if (!string.IsNullOrWhiteSpace(paymentType) && paymentType.ToLower().Contains("deposit"))
            return;

        // Consume voucher if present for this booking
        var sessBookingId = ctx.Session.GetInt32("VoucherBookingId");
        if (sessBookingId == bookingId && ctx.Session.GetInt32("VoucherId") is int voucherId)
        {
            var v = db.Voucher.FirstOrDefault(x => x.Id == voucherId && x.Email == ownerEmail && !x.IsUsed);
            if (v != null)
            {
                v.IsUsed = true;
            }

            ctx.Session.Remove("VoucherId");
            ctx.Session.Remove("VoucherCode");
            ctx.Session.Remove("VoucherAmount");
            ctx.Session.Remove("VoucherBookingId");
        }

        // Award points on full payment
        var user = db.Users.Find(ownerEmail);
        if (user != null && amountCharged > 0)
        {
            user.Points += (int)Math.Floor(amountCharged);
        }

        db.SaveChanges();
    }
}
