// (modified Register POST: validate AdminKey and create Admin when key matches config.Admin:FixedPassword)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PawfectGrooming.Models;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using X.PagedList;
using X.PagedList.Extensions;
using static PawfectGrooming.Helper;


namespace PawfectGrooming.Controllers;

public class AccountController : Controller
{
    private readonly UserContext db;
    private readonly Helper hp;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly IStringLocalizer<Resources.Resources> Localizer;



    public AccountController(UserContext db, Helper hp, IConfiguration config, IWebHostEnvironment env, IStringLocalizer<Resources.Resources> localizer)
    {
        this.db = db;
        this.hp = hp;
        _config = config;
        _env = env;
        Localizer = localizer;

    }

    // GET: Account/Login
    [AllowAnonymous]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity.IsAuthenticated)
        {
            TempData["Info"] = "You are already logged in.";
            return RedirectToAction("Index", "Home");
        }
        if (!User.Identity.IsAuthenticated && !string.IsNullOrEmpty(returnUrl)) //returnUrl means user want to access protected page
        {
            TempData["Error"] = "Please login to continue.";
        }
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM vm, string? returnUrl)
    {

        string? lastAttemptStr = HttpContext.Session.GetString("LastLoginAttempt");//Check LastLoginAttempt Time
        if (lastAttemptStr == null || DateTime.Now - DateTime.Parse(lastAttemptStr) > TimeSpan.FromMinutes(5))
        {//more than 5 minutes reset attempts to 0
            HttpContext.Session.SetInt32("LoginAttempts", 0);
        }
        int attempts = HttpContext.Session.GetInt32("LoginAttempts") ?? 0;//get the number of attempt if no default=0
        if (attempts >= 3)
        {
            TempData["Error"] = "Login Paused. Try Again Later";
            return RedirectToAction("Login");
        }

        // (1) Get user (admin or member) record based on email (PK)

        var user = db.Users.Find(vm.Email);

        // Block login immediately if account deactivated
        if (user != null && !user.IsActive)
        {
            TempData["Error"] = "Account deactivated.";
            return RedirectToAction("Login");
        }

        // (2) Custom validation -> verify password

        if (user == null || !hp.VerifyPassword(user.Hash, vm.Password))
        {
            HttpContext.Session.SetInt32("LoginAttempts", attempts + 1);
            HttpContext.Session.SetString("LastLoginAttempt", DateTime.Now.ToString());
            TempData["Error"] = $"Login failed. Attempt {attempts + 1}/3.";
            return RedirectToAction("Login");
        }
        if (!user.IsEmailVerified)
        {
            TempData["Error"] = "Please Verify your email first.";
            await ResendVerificationEmail(user.Email);
            return View(vm);
        }

        if (ModelState.IsValid)
        {
            TempData["Info"] = "Login successfully.";
            await hp.SignIn(user!.Email, user.Role, vm.RememberMe);

            // Clear tokens after login
            user.Token = string.Empty;
            user.TokenExpiry = DateTime.MinValue;
            HttpContext.Session.Remove("LoginAttempts");
            HttpContext.Session.Remove("LastLoginAttempt");
            db.SaveChanges();
            var loginHistory = new PawfectGrooming.Models.LoginHistory
            {
                Email = user.Email,
                LoginTime = DateTime.Now,
                Devices = HttpContext.Request.Headers["User-Agent"].ToString()
            };

            db.LoginHistories.Add(loginHistory);
            db.SaveChanges();

            // (4) Handle return URL
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
        ViewBag.ReturnUrl = returnUrl;
        return View(vm);
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnUrl)
    {
        TempData["Info"] = "Logout successfully.";

        // Sign out
        hp.SignOut();

        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnUrl)
    {
        TempData["Info"] = "Access denied!";
        return View();
    }



    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------
    // GET: Account/LoginHistory
    [Authorize]
    public IActionResult LoginHistory()
    {
        var userEmail = User.Identity.Name; // Assuming email is the username
        var history = db.LoginHistories
            .Where(h => h.Email == userEmail)
            .OrderByDescending(h => h.LoginTime)
            .ToList();
        return View(history);
    }

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        if (User.Identity.IsAuthenticated)
        {
            TempData["Info"] = "You are already logged in.";
            return RedirectToAction("Index", "Home");
        }
        ViewBag.GenderOptions = hp.GetGenderOptions(Localizer);
        return View();
    }



    // POST: Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVM vm, string CroppedPhoto)
    {
        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "This email is already registered.");
        }

        // Photo validation: must have either CroppedPhoto (base64) OR uploaded file
        if (string.IsNullOrEmpty(CroppedPhoto) && (vm.Photo == null || vm.Photo.Length == 0))
        {
            ModelState.AddModelError("Photo", "Please upload or capture a photo.");
        }
        else if (!string.IsNullOrEmpty(CroppedPhoto))
        {
            // Validate base64 format
            if (!Regex.IsMatch(CroppedPhoto, @"^data:image\/(png|jpeg);base64,"))
            {
                ModelState.AddModelError("Photo", "Invalid photo format.");
            }
        }
        else if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (!string.IsNullOrEmpty(err))
                ModelState.AddModelError("Photo", err);
        }

        // If AdminKey provided but incorrect -> validation error
        var fixedAdminKey = _config["Admin:FixedPassword"];
        if (!string.IsNullOrEmpty(vm.AdminKey) && vm.AdminKey != fixedAdminKey)
        {
            ModelState.AddModelError("AdminKey", "Invalid admin verification password.");
        }

        if (ModelState.IsValid)
        {
            var token = Guid.NewGuid().ToString(); //Generate a unique token
            var tokenexpiry = DateTime.UtcNow.AddMinutes(10);
            //image handling function
            string photoUrl = "";
            if (!string.IsNullOrEmpty(CroppedPhoto))
            {
                var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
                if (!string.IsNullOrEmpty(base64Data))
                {
                    var bytes = Convert.FromBase64String(base64Data);
                    var fileName = Guid.NewGuid() + ".jpg";
                    var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "User_Images", fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                    System.IO.File.WriteAllBytes(savePath, bytes);
                    photoUrl = fileName;
                }
            }
            else if (vm.Photo != null)
            {
                // Save original file
                photoUrl = hp.SavePhoto(vm.Photo, Path.Combine("images", "User_Images"));
            }

            // Determine whether to create Admin or Member
            bool createAdmin = !string.IsNullOrEmpty(vm.AdminKey) && vm.AdminKey == fixedAdminKey;

            if (createAdmin)
            {
                db.Admins.Add(new()
                {
                    Email = vm.Email,
                    Hash = hp.HashPassword(vm.Password),
                    Name = vm.Name,
                    PhotoURL = photoUrl,
                    Gender = vm.Gender,
                    PhoneNumber = vm.PhoneNumber,
                    IsEmailVerified = false,
                    Token = token,
                    TokenExpiry = tokenexpiry

                });
            }
            else
            {
                db.Members.Add(new()
                {
                    Email = vm.Email,
                    Hash = hp.HashPassword(vm.Password),
                    Name = vm.Name,
                    PhotoURL = photoUrl,
                    Gender = vm.Gender,
                    PhoneNumber = vm.PhoneNumber,
                    IsEmailVerified = false,
                    Token = token,
                    TokenExpiry = tokenexpiry
                });
            }

            db.SaveChanges();

            // Generate verification link with token
            var verificationLink = Url.Action(
                "VerifyEmail", "Account",
                new { email = vm.Email, Token = token },
                Request.Scheme
            );
            await SendVerificationEmailAsync(vm.Email, verificationLink);

            TempData["Info"] = "✅ Registered! Please verify email";
            return RedirectToAction("Login");
        }
        ViewBag.GenderOptions = hp.GetGenderOptions(Localizer);
        return View(vm);
    }

    //After user click link in the email
    [HttpGet]
    public IActionResult VerifyEmail(string email, string token)
    {
        var user = db.Users.FirstOrDefault(u => u.Email == email && u.Token == token && u.TokenExpiry > DateTime.UtcNow);
        if (user == null)
        {
            TempData["Error"] = "Invalid or expired verification link.";
            return RedirectToAction("Login");
        }
        //Mark email as verified and clear token
        user.IsEmailVerified = true;
        user.Token = string.Empty;
        user.TokenExpiry = DateTime.MinValue;
        db.SaveChanges();
        Helper.SendSms(user.PhoneNumber, "Your email has been verified! You can now login.");
        TempData["Info"] = "Email verified! You can now login.";
        return RedirectToAction("Login");
    }
    // View Profile
    [Authorize]
    public IActionResult ViewProfile()
    {
        // Get user (admin or member) record based on email (PK)
        var user = db.Users.Find(User.Identity!.Name);
        if (user == null) return RedirectToAction("Index", "Home");
        dynamic profile = null;
        if (user.Role == "Member")
        {
            profile = db.Members.Find(User.Identity!.Name);
        }
        if (user.Role == "Admin")
        {
            profile = db.Admins.Find(User.Identity!.Name);
        }
        return View(profile);
    }

    // GET: Account/UpdatePassword
    [Authorize]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        // Get user (admin or member) record based on email (PK)
        var u = db.Users.Find(User.Identity!.Name);
        if (u == null) return RedirectToAction("Index", "Home");

        // If current password not matched
        if (!hp.VerifyPassword(u.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            // Update user password (hash)
            u.Hash = hp.HashPassword(vm.New);
            db.SaveChanges();
            TempData["Info"] = "Password updated.";
            return RedirectToAction();
        }

        return View();
    }

    // GET: Account/UpdateProfile
    //[Authorize(Roles ="Member")]
    //[Authorize(Roles ="Admin")]
    [Authorize]
    public IActionResult UpdateProfile()
    {
        // Get member record based on email (PK)
        var m = db.Set<User>().FirstOrDefault(u => u.Email == User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        var vm = new UpdateProfileVM
        {
            Email = m.Email,
            Name = m.Name,
            PhoneNumber = m.PhoneNumber,
            PhotoURL = m.PhotoURL,
        };

        return View(vm);
    }

    // POST: Account/UpdateProfile
    //[Authorize(Roles ="Member")]
    //[Authorize(Roles ="Admin")]
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateProfile(UpdateProfileVM vm, string? CroppedPhoto = "")
    {
        // Get member record based on email (PK)
        var user = db.Set<User>().FirstOrDefault(u => u.Email == User.Identity.Name);
        if (user == null) return RedirectToAction("Index", "Home");
        dynamic m = null;
        if (user.Role == "Member")
        {
            m = db.Members.Find(User.Identity!.Name);

        }
        if (user.Role == "Admin")
        {
            m = db.Admins.Find(User.Identity!.Name);

        }

        string photoUrl = m.PhotoURL;
        bool isNewPhoto = false;
        if (!string.IsNullOrEmpty(CroppedPhoto))
        {
            var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
            if (!string.IsNullOrEmpty(base64Data))
            {
                var bytes = Convert.FromBase64String(base64Data);
                var fileName = Guid.NewGuid() + ".jpg";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "User_Images", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                System.IO.File.WriteAllBytes(savePath, bytes);
                photoUrl = fileName;
                isNewPhoto = true;
            }
        }
        else if (vm.Photo != null)
        {
            photoUrl = hp.SavePhoto(vm.Photo, Path.Combine("images", "User_Images"));
        }

        if (ModelState.IsValid)
        {
            bool hasChanges = false;

            if (m.Name != vm.Name)
            {
                m.Name = vm.Name;
                hasChanges = true;
            }
            //Delete old images replace new one
            if (isNewPhoto)
            {
                if (!string.IsNullOrEmpty(m.PhotoURL))
                {
                    hp.DeletePhoto(m.PhotoURL, Path.Combine("images", "User_Images"));
                }
                m.PhotoURL = photoUrl;
                hasChanges = true;
            }
            if (m.PhoneNumber != vm.PhoneNumber)
            {
                m.PhoneNumber = vm.PhoneNumber;
                hasChanges = true;
            }

            if (hasChanges)
            {
                db.SaveChanges();
                TempData["Info"] = "Profile updated.";
            }
            else
            {
                TempData["Info"] = "No Changes were made ";
            }

            return RedirectToAction();
        }

        vm.Email = m.Email;
        vm.PhotoURL = m.PhotoURL;
        vm.PhoneNumber = m.PhoneNumber;
        return View(vm);
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        return View();
    }

    // POST: Account/ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
    {
        var user = db.Users.Find(vm.Email);
        if (User.Identity.IsAuthenticated)
        {
            TempData["Info"] = "You are already logged in.";
            return RedirectToAction("Index", "Home");
        }

        if (user == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
        }

        if (ModelState.IsValid)
        {
            //1.Generate random password
            //string password = hp.RandomPassword();
            string token = Guid.NewGuid().ToString(); //Generate a unique token

            //2.Update user (admin or member) record
            //user!.Hash = hp.HashPassword(password);
            user.Token = token;
            user.TokenExpiry = DateTime.UtcNow.AddMinutes(10); //Set token expiry time
            db.SaveChanges();

            //3.Generate reset password link 
            var resetLink = Url.Action("NewPassword", "Account", new { email = user.Email, token }, Request.Scheme);

            //4.Send email with reset password link and temporary password
            await SendResetPasswordEmailAsync(
                vm.Email,
                resetLink
            );
            TempData["Info"] = "Password reset link sent to your email.";
            //TempData["Info"] = $"Password reset to <b>{password}</b>.";
            return RedirectToAction("Login");
        }

        return View();
    }
    [HttpGet]
    public IActionResult NewPassword(string email, string token)
    {
        var user = db.Users.FirstOrDefault(u =>
            u.Email == email &&
            u.Token == token &&
            u.TokenExpiry > DateTime.UtcNow);

        if (user == null)
        {
            TempData["Error"] = "Invalid or expired link.";
            return RedirectToAction("Login");
        }

        return View(new NewPasswordVM { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult NewPassword(NewPasswordVM vm)
    {
        var user = db.Users.FirstOrDefault(u =>
            u.Email == vm.Email &&
            u.Token == vm.Token &&
            u.TokenExpiry > DateTime.UtcNow);

        if (user == null)
        {
            TempData["Error"] = "Invalid or expired link.";
            return RedirectToAction("Login");
        }

        if (ModelState.IsValid)
        {
            user.Hash = hp.HashPassword(vm.NewPassword);
            user.Token = string.Empty;
            user.TokenExpiry = DateTime.MinValue;
            db.SaveChanges();

            TempData["Info"] = "Password updated successfully!";
            return RedirectToAction("Login");
        }

        return View(vm);
    }
    //Registration email verification 
    private async Task SendVerificationEmailAsync(string email, string verificationLink)
    {
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        var mail = new MailMessage();
        mail.To.Add(email);
        mail.Subject = "Pawfect Grooming – Email Verification";
        mail.IsBodyHtml = true;
        //images embeddded
        var imagePath = Path.Combine(_env.WebRootPath, "images", "User_Images", Path.GetFileName(user.PhotoURL));
        var att = new Attachment(imagePath);
        mail.Attachments.Add(att);
        att.ContentId = "profilephoto";

        var htmlContent = $"""
<table style='font-family:Segoe UI, sans-serif; max-width:600px; margin:auto; padding:20px; border:1px solid #e0e0e0; border-radius:8px;'>
    <tr>
        <td>
            <p style='text-align:center;'>
                <img src='cid:profilephoto' alt='Profile Photo' width='80' style='border-radius:50%;' />
                <div style='font-size:1.5em; font-weight:bold;text-align:center'>{user.Name}</div>
            </p>
            <h2 style='color:#2a2a2a;'>Verify Your Email</h2>
            <p>Dear, {user.Name}</p>
            <p>Click the link below to verify your email address for Pawfect Grooming:</p>
            <p style='text-align:center;'>
                <a href='{verificationLink}' style='display:inline-block; padding:10px 20px; background-color:#007bff; color:white; text-decoration:none; border-radius:4px;'>
                    Verify Email
                </a>
            </p>
            <p style='font-size:14px; color:#555;'>This link will expire in <strong>10 minutes</strong>.</p>
            <p>If you didn’t sign up for Pawfect Grooming, feel free to ignore this email.</p>
            <hr style='margin:30px 0; border:none; border-top:1px solid #ccc;'/>
            <p style='font-size:12px; color:gray;'>
                Sent by <strong>Pawfect Grooming</strong>. Please don’t reply to this email.<br/>
                For support, contact <a href='mailto:innotech2502@gmail.com'>innotech2502@gmail.com</a>.
            </p>
        </td>
    </tr>
</table>
""";
        mail.Body = htmlContent;
        await SendEmailAsync(mail);
    }

    // Password reset email
    private async Task SendResetPasswordEmailAsync(string email, string resetLink)
    {
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        var mail = new MailMessage();
        mail.To.Add(email);
        mail.Subject = "Pawfect Grooming – Reset Your Password";
        mail.IsBodyHtml = true;
        //images embeddded
        var imagePath = Path.Combine(_env.WebRootPath, "images", "User_Images", Path.GetFileName(user.PhotoURL));
        var att = new Attachment(imagePath);
        mail.Attachments.Add(att);
        att.ContentId = "profilephoto";

        var htmlContent = $"""
        <table style='font-family:Segoe UI, sans-serif; max-width:600px; margin:auto; padding:20px; border:1px solid #e0e0e0; border-radius:8px;'>
            <tr>
                <td>
                    <p style='text-align:center;'>
                    <img src='cid:profilephoto' alt='Profile Photo' width='80' style='border-radius:50%;' />
                    <div style='font-size:1.5em; font-weight:bold;text-align:center'>{user.Name}</div>
                    </p>

 
                    <h2 style='color:#2a2a2a;'>Reset Your Password</h2>
                    <p>Dear, {user.Name}</p>
                    <p>We received a request to reset your Pawfect Grooming password.</p>
                    <p style='text-align:center;'>
                        <a href='{resetLink}' style='display:inline-block; padding:10px 20px; background-color:#007bff; color:white; text-decoration:none; border-radius:4px;'>
                            Set New Password
                        </a>
                    </p>
                    <p style='font-size:14px; color:#555;'>This link will expire in <strong>10 minutes</strong>.</p>
                    <p>If you didn’t request this, feel free to ignore 
                    <hr style='margin:30px 0; border:none; border-top:1px solid #ccc;'/>
                    <p style='font-size:12px; color:gray;'>
                        Sent by <strong>Pawfect Grooming</strong>. Please don’t reply to this email.<br/>
                        For support, contact <a href='mailto:innotech2502@gmail.com'>innotech2502@gmail.com</a>.
                    </p>
                </td>
            </tr>
        </table>
        """;
        mail.Body = htmlContent;
        await SendEmailAsync(mail);
    }
    //Set Up sender email
    private async Task SendEmailAsync(MailMessage mail)
    {
        //1. Get SMTP settings from configuration
        var smtpHost = _config["SmtpSetting:Host"];
        var smtpPort = int.Parse(_config["SmtpSetting:Port"]);
        var smtpUser = _config["SmtpSetting:SenderEmail"];
        var smtpPass = _config["SmtpSetting:Password"];
        var SenderName = _config["SmtpSetting:SenderName"];
        mail.From = new MailAddress(smtpUser, SenderName);
        //2.Configure SMTP client
        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true, // Required for Gmail
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtpUser, smtpPass)
        };
        //3. Send the email asynchronously
        await client.SendMailAsync(mail);
        // 5.Clean up attachments 
        foreach (var attachment in mail.Attachments)
        {
            attachment.Dispose();
        }

    }
    private async Task ResendVerificationEmail(string email)
    {
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user == null) return;

        // Generate new token with longer expiry
        user.Token = Guid.NewGuid().ToString();
        user.TokenExpiry = DateTime.UtcNow.AddMinutes(10);
        db.SaveChanges();

        var verificationLink = Url.Action(
            "VerifyEmail",
            "Account",
            new { email, Token = user.Token },
            Request.Scheme
        );

        await SendVerificationEmailAsync(email, verificationLink);
        //TempData["Info"] = "Verification link sent to your email.";
    }

    // GET: /Account/TemporaryLogin
    [HttpGet]
    [AllowAnonymous]
    public IActionResult TemporaryLogin()
    {
        if (User.Identity.IsAuthenticated)
        {
            TempData["Info"] = "You are already logged in.";
            return RedirectToAction("Index", "Home");
        }
        return View(); // Show the temp login page/form
    }

    // GET: /Account/TempToken
    [HttpGet]
    [AllowAnonymous]
    public IActionResult TempToken()
    {
        if (User.Identity.IsAuthenticated)
        {
            TempData["Info"] = "You are already logged in.";
            return RedirectToAction("Index", "Home");
        }
        return View("TempToken");
    }

    // POST: /Account/TempToken
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TempToken([FromForm] string tempToken)
    {
        // Debug logging (optional)
        foreach (var key in Request.Form.Keys)
            Console.WriteLine($"Form key: {key} = {Request.Form[key]}");

        if (string.IsNullOrWhiteSpace(tempToken))
        {
            TempData["Error"] = "Token is required.";
            return View("TempToken");
        }

        // 1) Try in-memory store first (if you use it)
        User? tempUser = TemporaryUserStore.Get(tempToken);

        // 2) If not in-memory, try DB Members
        if (tempUser == null)
        {
            tempUser = db.Members
                        .FirstOrDefault(m => m.Token == tempToken && m.TokenExpiry > DateTime.UtcNow);
        }

        // 3) If still not found, try DB Admins
        if (tempUser == null)
        {
            tempUser = db.Admins
                        .FirstOrDefault(a => a.Token == tempToken && a.TokenExpiry > DateTime.UtcNow);
        }

        // 4) Not found or expired
        if (tempUser == null)
        {
            TempData["Error"] = "Invalid or expired token.";
            return View("TempToken");
        }

        // Sign in with the user's derived role (GetType().Name -> "Admin" or "Member")
        var role = tempUser.Role ?? "Member";
        TempData["Info"] = $"Temporary login successful. Role: {role}";

        await hp.SignIn(tempUser.Email, role, false);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true
            }
        );

        return Redirect(Request.Headers["Referer"].ToString());
    }

    [HttpGet]
    [Authorize]
    public IActionResult MyVouchers()
    {
        var email = User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Please sign in.";
            return RedirectToAction("Login", "Account");
        }

        var now = DateTime.UtcNow;
        var user = db.Users.AsNoTracking().FirstOrDefault(u => u.Email == email);
        var all = db.Voucher.AsNoTracking()
            .Where(v => v.Email == email)
            .OrderByDescending(v => v.ExpiryDate)
            .ThenBy(v => v.IsUsed)
            .ToList();

        var vm = new VouchersVM
        {
            CurrentPoints = user?.Points ?? 0,
            Available = all.Where(v => !v.IsUsed && v.ExpiryDate >= now).ToList(),
            Used = all.Where(v => v.IsUsed).ToList(),
            Expired = all.Where(v => !v.IsUsed && v.ExpiryDate < now).ToList()
        };

        return View(vm);
    }
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult RedeemVoucher()
    {
        try
        {
            var email = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Please sign in.";
                return RedirectToAction("Login", "Account");
            }

            var user = db.Users.Find(email);
            Console.WriteLine($"RedeemVoucher: User={user?.Email}, Points={user?.Points}, IsActive={user?.IsActive}");
            if (user is null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("MyVouchers");
            }

            if (user is { IsActive: false })
            {
                TempData["Error"] = "Account deactivated.";
                return RedirectToAction("MyVouchers");
            }

            const int costPoints = 100;
            if (user.Points < 100)
            {
                TempData["Error"] = "Not enough points.";
                return RedirectToAction("MyVouchers");
            }

            // Deduct points
            user.Points -= costPoints;

            // Generate unique code with retry
            string code;
            int attempts = 0;
            do
            {
                code = Helper.GenerateCode();
                attempts++;
                if (attempts > 20)
                {
                    TempData["Error"] = "Failed to redeem voucher code.";
                    return RedirectToAction("MyVouchers");
                }
            } while (db.Voucher.Any(v => v.Code == code));

            var voucher = new Voucher
            {
                Code = code,
                Email = email,
                DiscountAmount = 5.00m,
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                IsUsed = false
            };

            db.Voucher.Add(voucher);
            db.SaveChanges();

            TempData["Info"] = $"Voucher redeemed";
            return RedirectToAction("MyVouchers");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to redeem voucher";
            return RedirectToAction("MyVouchers");
        }
    }

}