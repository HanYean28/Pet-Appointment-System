using System.ComponentModel.DataAnnotations;

namespace PawfectGrooming.Models;

public class Service
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public required string Description { get; set; }

    [Required]
    public decimal Price { get; set; }
}