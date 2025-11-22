using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawfectGrooming.Models;

public class ServiceOption
{
    public int Id { get; set; }

    [Required]
    public required string Name { get; set; }

    [Required]
    public required string Description { get; set; }

    // Keep as string if DB is text. Use regex to validate numeric price format (e.g. 12.34)
    [Required]
    [Precision(4, 2)]
    [Column(TypeName = "decimal(5,2)")]

    public required decimal Price { get; set; }

    // Initialize to avoid nullable-warning
    public required List<string> Features { get; set; } = new List<string>();

    [Required]
    public required string PetType { get; set; }

    public required List<string> ImageURL { get; set; } = new List<string>();

    // NEW: stores aggregated booking count for this service (will require migration to persist)
    public int Count { get; set; } = 0;
}