using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawfectGrooming.Models;

public class Booking
{
    [Key]
    public int Id { get; set; }

    [Required, EmailAddress]
    public required string UserEmail { get; set; }

    [ForeignKey("UserEmail")]
    public virtual User? User { get; set; }

    [Required]
    public int PetId { get; set; }

    [ForeignKey("PetId")]
    public virtual Pets? Pet { get; set; }

    public int? ServiceId { get; set; }

    [ForeignKey("ServiceId")]
    public virtual ServiceOption? Service { get; set; }

    public int? PackageId { get; set; }

    [ForeignKey("PackageId")]
    public virtual Package? Package { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public required string Time { get; set; }

    public decimal Price { get; set; }

    // Ensure this is an int and publicly settable so EF can create/alter the column correctly.
    public int Count { get; set; } = 1;

    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Pending Payment";

    public virtual Payment? Payment { get; set; }
}