using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawfectGrooming.Models;
using System;
using System.Security.Cryptography;
using System.Text;
using static PawfectGrooming.Helper;

namespace PawfectGrooming.Controllers
{
    [Route("api/account")]
    [ApiController]
    public class AccountApiController : ControllerBase
    {
        private readonly UserContext db;

        public AccountApiController(UserContext db)
        {
            this.db = db;
        }

        // POST: api/account/temp-login
        [HttpPost("temp-login")]
        [AllowAnonymous]
        public IActionResult TemporaryLogin(
            [FromHeader(Name = "X-Temp-Login-Token")] string tempLoginToken,
            [FromHeader(Name = "X-Temp-Login-Role")] string role = "Member")
        {
            // Validate token
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (tempLoginToken != TemporaryLoginConfig.AllowedAdminToken)
                    return Unauthorized("Invalid or missing admin temp login token.");
            }
            else
            {
                if (tempLoginToken != TemporaryLoginConfig.AllowedMemberToken)
                    return Unauthorized("Invalid or missing member temp login token.");
            }

            // Generate temporary user
            var tempEmail = Guid.NewGuid().ToString() + "@temp.local";
            var token = Guid.NewGuid().ToString();
            var expiry = DateTime.UtcNow.AddMinutes(1); // 1 minute
            var randomPassword = Guid.NewGuid().ToString();
            var passwordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(randomPassword)));

            User tempUser;

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                tempUser = new Admin
                {
                    Email = tempEmail,
                    Name = "Admin_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Hash = passwordHash,
                    IsEmailVerified = false,
                    IsActive = true,
                    Gender = "Other",
                    PhoneNumber = "00000000",
                    PhotoURL = "default.png",
                    Token = token,
                    TokenExpiry = expiry,
                    IsTemporary = true
                };
            }
            else
            {
                tempUser = new Member
                {
                    Email = tempEmail,
                    Name = "Guest_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Hash = passwordHash,
                    IsEmailVerified = false,
                    IsActive = true,
                    Gender = "Other",
                    PhoneNumber = "00000000",
                    PhotoURL = "default.png",
                    Token = token,
                    TokenExpiry = expiry,
                    IsTemporary = true
                };
            }

            db.Users.Add(tempUser);
            db.SaveChanges();

            TemporaryUserStore.Add(token, tempUser, expiry);

            return Ok(new
            {
                token,
                expiry,
                role = tempUser.Role,
                user = new { tempUser.Email, tempUser.Name, tempUser.Role }
            });
        }
    }
}
