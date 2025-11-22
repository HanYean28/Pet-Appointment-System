using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PawfectGrooming.Resources;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;



namespace PawfectGrooming.Models;

// View Models ---------------------------------------------------------------s-

#nullable disable warnings

public class LoginVM
{
    //Email Validation
    [StringLength(100)]//Max 100 char
    [EmailAddress]//Validation Email format

    /*[EmailAddress(ErrorMessageResourceType = typeof(Demo.Resources.ValidationMessages),
        ErrorMessageResourceName = "InvalidEmailFormat")]//Validation Email format */
    public string Email { get; set; }

    //Password Validation
    [DataType(DataType.Password)]//input hidden with dot
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}

public class RegisterVM
{
    [StringLength(100)]
    [EmailAddress]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?.&])[A-Za-z\d@$!%*?.&]{5,100}$",
    ErrorMessage = "Password must be 5-100 characters, and include at least one uppercase, one lowercase, one digit, and one special character.")]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }

    [Required(ErrorMessage = "Please select your gender.")]
    public string Gender { get; set; }
    [Required]
    [RegularExpression(@"^\+60\d{9,10}$", ErrorMessage = "Phone number must start with +60 and be 11 to 12 digits long")]
    public string PhoneNumber { get; set; }

    [StringLength(100)]
    public string Name { get; set; }
    public IFormFile? Photo { get; set; }

    // Optional admin verification key. If provided and matches appsetting Admin:FixedPassword
    // the controller will create an Admin record instead of a Member.
    [DataType(DataType.Password)]
    [Display(Name =  "Admin Verification Password")]
    public string? AdminKey { get; set; }
}

public class UpdatePasswordVM
{
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{5,100}$",
        ErrorMessage = "New password must at least 5 characters, 1 uppercase, 1 lowercase, 1 digit, and 1 special character.")]
    [Display(Name = "New Password")]
    public string New { get; set; }

    [Compare("New")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    public string? Email { get; set; }

    [StringLength(100)]
    public string? Name { get; set; }

    [RegularExpression(@"^\+60\d{9,10}$", ErrorMessage = "Phone number must start with +60 and be 11 to 12 digits long")]
    public string? PhoneNumber { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
}
//Forgot Passowrd
public class NewPasswordVM
{
    public string Email { get; set; }
    public string Token { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?.&])[A-Za-z\d@$!%*?.&]{5,100}$",
    ErrorMessage = "Password must be 5-100 characters, and include at least one uppercase, one lowercase, one digit, and one special character.")]
    public string NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare("NewPassword")]
    public string ConfirmPassword { get; set; }
}
public class PetCreateVM
{
    [Required]
    [StringLength(50)]
    [Display(Name = "Pet Name")]
    public string Name { get; set; }

    [Required]
    [StringLength(30)]
    public string PetType { get; set; }
    [Required]
    public string Breed { get; set; }

    [Required(ErrorMessage = "Please select your gender.")]
    public string Gender { get; set; }

    [Required]
    [Range(0, 50)]
    public int Age { get; set; }

    [Required]
    [Range(0.1, 100.0)]
    public decimal Weight { get; set; }

    [StringLength(500)]
    [Display(Name = "Medical Notes")]
    public string MedicalNotes { get; set; }
    [Required]
    public IFormFile Photo { get; set; }
}
public class PetUpdateVM
{
    public int PetId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Pet Name")]
    public string Name { get; set; }

    [Required]
    [StringLength(30)]
    public string PetType { get; set; }

    [Required]
    public string Breed { get; set; }

    [Required]
    public string Gender { get; set; }

    [Required]
    [Range(0, 50)]
    public int Age { get; set; }

    [Required]
    [Range(0.1, 100.0)]
    public decimal Weight { get; set; }

    [StringLength(500)]
    [Display(Name = "Medical Notes")]
    public string MedicalNotes { get; set; }
    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }
}
public class StatusViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = "";
    public int DaysInMonth { get; set; }
    public Dictionary<int, List<string>> DaySlots { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
}

    // Simple view model to show vouchers grouped by status + current points
    public class VouchersVM
    {
    public List<Voucher> Available { get; set; } = new();
    public List<Voucher> Used { get; set; } = new();
    public List<Voucher> Expired { get; set; } = new();

    // For showing the user's current balance (e.g., "You have 123 points")
    public int CurrentPoints { get; set; }
}


