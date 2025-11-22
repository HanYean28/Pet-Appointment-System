using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PawfectGrooming.Models;
//For deletion of expired temporary accounts
namespace PawfectGrooming.Services
{
    public class TempAccountCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public TempAccountCleanupService(IServiceProvider serviceProvider)
        {
            Console.WriteLine(">>> TempAccountCleanupService CONSTRUCTOR called <<<");
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine(">>> TempAccountCleanupService ExecuteAsync started <<<");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"[TempAccountCleanupService] Tick at {DateTime.UtcNow}");
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<UserContext>();

                        var expiredUsers = db.Users
                            .Where(u => u.IsTemporary && u.TokenExpiry <= DateTime.UtcNow)
                            .ToList();

                        Console.WriteLine($"[TempAccountCleanupService] Found {expiredUsers.Count} expired users.");

                        foreach (var u in expiredUsers)
                        {
                            Console.WriteLine($"[TempAccountCleanupService] Deleting: {u.Email}, Expiry: {u.TokenExpiry}");
                        }

                        if (expiredUsers.Any())
                        {
                            db.Users.RemoveRange(expiredUsers);
                            await db.SaveChangesAsync(stoppingToken);
                            Console.WriteLine("[TempAccountCleanupService] Deleted expired users.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[TempAccountCleanupService] ERROR: " + ex);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}