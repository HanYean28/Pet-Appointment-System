using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PawfectGrooming.Services;
//using Stripe;

namespace PawfectGrooming
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddSession();

            // Replace with your actual connection string
            services.AddDbContext<UserContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            // Register background service
            services.AddHostedService<TempAccountCleanupService>();

            // Add Stripe configuration so we can inject PublishableKey later
            services.Configure<StripeSettings>(Configuration.GetSection("Stripe"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Configure Stripe with secret key
            //StripeConfiguration.ApiKey = Configuration["Stripe:SecretKey"];

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }

    // Helper class for binding Stripe settings from appsettings.json
    public class StripeSettings
    {
        public string PublishableKey { get; set; }
        public string SecretKey { get; set; }
        public string WebhookSecret { get; set; }
    }
}
