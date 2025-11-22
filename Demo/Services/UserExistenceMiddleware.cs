using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class UserExistenceMiddleware
{
    private readonly RequestDelegate _next;

    public UserExistenceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserContext db)
    {
        if (context.User.Identity.IsAuthenticated)
        {
            var email = context.User.FindFirst(ClaimTypes.Name)?.Value
                        ?? context.User.FindFirst(ClaimTypes.Email)?.Value;

            if (email != null)
            {
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    // User deleted: sign out and redirect
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Response.Cookies.Append("Info", "You have been logged out.");
                    context.Response.Redirect("/Account/Login");
                    return; // Stop processing further
                }
            }
        }

        await _next(context);
    }
}