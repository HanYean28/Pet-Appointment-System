    using Microsoft.EntityFrameworkCore;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    namespace PawfectGrooming.Models;

        public class Pets
        {
        [Key]
        public int PetId { get; set; }
        [Required]
        [StringLength(50)]
        public required string Name { get; set; }
        public required string PetType { get; set; }
        public  string Breed { get; set; }
        public required string Gender { get; set; }
        public int Age { get; set; }
        [Precision(5,2)]
        public decimal Weight { get; set; }
        public  string MedicalNotes { get; set; }
        public  string Photo { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; } //Foreign key to User
        [ForeignKey("Email")]
        public required virtual User User { get; set; }//Navigation property to User

    }

