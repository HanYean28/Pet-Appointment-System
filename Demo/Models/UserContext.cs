using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace PawfectGrooming.Models;

#nullable disable warnings

public class UserContext : DbContext
{
    public UserContext(DbContextOptions options) : base(options) { }

    //DB Tables -------------------------------------------------------------
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Pets> Pets { get; set; }
    public DbSet<Package> Packages { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<ServiceOption> ServiceOptions { get; set; }
    public DbSet<Payment> Payment { get; set; }
    public DbSet<FAQ> FAQs { get; set; }
    public DbSet<LoginHistory> LoginHistories { get; set; }
    public DbSet<Voucher> Voucher { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Converter: List<string> <-> JSON string, tolerant of legacy plain string values.
        var listToStringConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions)null),
            v => DeserializeListTolerant(v)
        );

        // Apply to entities that store lists as JSON in nvarchar columns
        modelBuilder.Entity<ServiceOption>().Property(e => e.ImageURL)
            .HasConversion(listToStringConverter)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<ServiceOption>().Property(e => e.Features)
            .HasConversion(listToStringConverter)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<Package>().Property(e => e.ImageURL)
            .HasConversion(listToStringConverter)
            .HasColumnType("nvarchar(max)");

        modelBuilder.Entity<Package>().Property(e => e.Features)
            .HasConversion(listToStringConverter)
            .HasColumnType("nvarchar(max)");

        base.OnModelCreating(modelBuilder);
    }

    // Helper used by the ValueConverter to safely read stored values that may be:
    // - a JSON array string like '["a.jpg","b.jpg"]'
    // - a legacy single filename like 'myimage.jpg'
    private static List<string> DeserializeListTolerant(string v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return new List<string>();

        var s = v.Trim();
        // If it looks like JSON array try to deserialize, otherwise treat as single item
        if (s.StartsWith("["))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(s);
                return arr ?? new List<string>();
            }
            catch
            {
                // fall through to treat as plain string
            }
        }

        // Also handle case where value is a JSON string (e.g. "\"foo.jpg\"") or plain filename
        try
        {
            // If it's a JSON string containing a single filename, this will deserialize.
            if ((s.StartsWith("\"") && s.EndsWith("\"")) || s.StartsWith("{"))
            {
                var single = JsonSerializer.Deserialize<string>(s);
                if (!string.IsNullOrEmpty(single))
                    return new List<string> { single };
            }
        }
        catch
        {
            // ignore
        }

        return new List<string> { v };
    }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }
    [MaxLength(100)]
    public string PhotoURL { get; set; }
    [MaxLength(10)]
    public string Gender { get; set; }
    [Phone]
    public string PhoneNumber { get; set; }
    //read which object is used
    public string Role => GetType().Name;
    //Emaill verification status
    public bool IsEmailVerified { get; set; } = false;//default value is false
    [MaxLength(100)]
    public string Token { get; set; } //For password reset or email verification
    public DateTime TokenExpiry { get; set; } //For password reset or email verification

    // New: whether account is active. Admin can toggle this.
    public bool IsActive { get; set; } = true;
    //Temporary account created via temp-login API
    public bool IsTemporary { get; set; } = false;
    //Points for loyalty program
    public int Points { get; set; } = 0; //Default points is 0
}

public class Admin : User { }

public class Member : User { }