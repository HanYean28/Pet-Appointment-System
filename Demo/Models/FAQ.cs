using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PawfectGrooming.Models;

namespace PawfectGrooming.Models;

public class FAQ
{
    [Key]
    [Required]
    public int Id { get; set; }
    [Required]
    [MaxLength(2000)]
    public string Question { get; set; }

    [Required]
    [MaxLength(2000)]

    public string Answer { get; set; }

    [Required]
    [MaxLength(200)]
    public string Keyword { get; set; } 
}