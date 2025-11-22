using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawfectGrooming.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; } // e.g., Credit Card, FPX, E-Wallet

        [Required]
        [Display(Name = "Payment Type")]
        public string? PaymentType { get; set; } // Deposit or Full Payment

        [Required]
        public string? Status { get; set; } = "Pending"; // Pending, Completed, Failed

        [Required]
        public int BookingId { get; set; } // Foreign Key property

        [ForeignKey("BookingId")]
        public virtual Booking? Booking { get; set; } // Navigation property

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime Date { get; set; }

        // New: explicit payment amount for this payment record
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [RegularExpression(@"^(\d{4}\s?){4}$", ErrorMessage = "Card number must be 16 digits.")]
        public string? CardNumber { get; set; }
       
        [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "Expiry date must be MM/YY.")]
        public string? ExpiryDate { get; set; }

        [RegularExpression(@"^\d{3}$", ErrorMessage = "CVV must be 3 digits.")]
        public string? CVV { get; set; }

        public string? Bank { get; set; } // FPX
        
        [Display(Name = "E-Wallet Provider")]
        public string? WalletProvider { get; set; } // E-Wallet
        
        [Display(Name = "E-Wallet ID")]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "E-Wallet ID must be between 8 and 50 characters.")]
        public string? WalletId { get; set; } // E-Wallet

        public string? StripeSessionId { get; set; } // Stripe
        public string? StripePaymentIntentId { get; set; } // Stripe
    }
}