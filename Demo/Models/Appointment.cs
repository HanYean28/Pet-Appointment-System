using System;
using System.ComponentModel.DataAnnotations;

namespace PawfectGrooming.Models
{
    public class AppointmentViewModel
    {
        // Step 1: Date & Time 
        [Required]
        [DataType(DataType.Date)]
        public DateTime? Date { get; set; }

        [Required]
        public string Time { get; set; }

        // Step 2: Pet Info
        [Required]
        public string PetName { get; set; }

        [Required]
        public string PetType { get; set; }

        public string PetBreed { get; set; }
        public decimal PetWeight { get; set; }
        public string SpecialInstructions { get; set; }

        // Step 3: Owner Info
        [Required]
        public string OwnerName { get; set; }

        [Required]
        [Phone]
        public string OwnerPhone { get; set; }

        [Required]
        [EmailAddress]
        public string OwnerEmail { get; set; }
    }
}
