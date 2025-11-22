global using PawfectGrooming;
global using PawfectGrooming.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using PawfectGrooming.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(); builder.Services.AddSqlServer<UserContext>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\PawfectUsersDB.mdf;
");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuthCookie";
        options.LoginPath = "/Account/Login"; // Redirect to login if unauthorized
        options.AccessDeniedPath = "/Account/AccessDenied";

        // Cookie settings (security + expiration)
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.SlidingExpiration = false; 

    });

//Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];


builder.Services.AddScoped<Helper>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();
builder.Services.AddHostedService<TempAccountCleanupService>();
var app = builder.Build();
var supportedCultures = new[] { "en-US", "zh-CN", "ms-MY" };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),//Default language - english
    SupportedCultures = [.. supportedCultures.Select(c => new CultureInfo(c))],
    SupportedUICultures = [.. supportedCultures.Select(c => new CultureInfo(c))]
};
app.UseRequestLocalization(localizationOptions);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<UserExistenceMiddleware>();
app.UseAuthorization();  
app.MapDefaultControllerRoute();

app.Run();


