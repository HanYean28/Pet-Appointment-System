using System.ComponentModel.DataAnnotations;

namespace PawfectGrooming.Models;

public class Package
{
    public int Id { get; set; } // Or whatever your PK is
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; } // Use string if not always a number
    public required List<string> Features { get; set; } // If JSON array
    public required string PetType { get; set; }
    public required List<string> ImageURL { get; set; } = new List<string>();

    // NEW: stores aggregated booking count for this package (will require migration to persist)
    public int Count { get; set; } = 0;
}