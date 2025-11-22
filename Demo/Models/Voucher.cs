using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawfectGrooming.Models;

    public class Voucher
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        [Required, EmailAddress]
        public required string Email { get; set; }

        [ForeignKey("Email")]
        public virtual User? User { get; set; }
        public decimal DiscountAmount { get; set; } = 5.00m; // Default discount amount
        public DateTime ExpiryDate { get; set; }
        public bool IsUsed { get; set; } = false;
    }

