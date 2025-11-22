using Demo.Migrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.VisualBasic;
using PawfectGrooming.Models;
using System.Security.Claims;
namespace PawfectGrooming.Controllers
{
    [Authorize(Roles = "Member")]
    public class PetController : Controller
    {
        private readonly UserContext db;
        private readonly Helper hp;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<Resources.Resources> Localizer;

        public PetController(UserContext db, Helper hp, IWebHostEnvironment env, IStringLocalizer<Resources.Resources> localizer)
        {
            this.db = db;
            this.hp = hp;
            _env = env;
            Localizer = localizer;
        }

        public async Task<IActionResult> ProfilePet()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
            var pets = await db.Pets
                .Where(p => p.Email == userEmail)
                .ToListAsync();
            return View(pets);
        }

        public async Task<IActionResult> PetDetails(int? id)
        {
            if (id == null) return NotFound();
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
            var pet = await db.Pets
                .FirstOrDefaultAsync(m => m.PetId == id && m.Email == userEmail);
            if (pet == null) return NotFound();
            return View(pet);
        }
        [HttpGet]
        public IActionResult CreatePet()
        {
            ViewBag.GenderOptions = hp.GetGenderOptions(Localizer);
            ViewBag.PetTypeOptions = hp.GetPetTypeOptionsForPet(Localizer);
            return View(new PetCreateVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePet(PetCreateVM vm, string CroppedPhoto)
        {
            if (ModelState.IsValid("Photo"))
            {
                var err = hp.ValidatePhoto(vm.Photo);
                if (!string.IsNullOrEmpty(err))
                    ModelState.AddModelError("Photo", err);
            }

            if (ModelState.IsValid)
            {
                string photoFileName ="";
                if (!string.IsNullOrEmpty(CroppedPhoto))
                {
                    var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
                    if (!string.IsNullOrEmpty(base64Data))
                    {
                        var bytes = Convert.FromBase64String(base64Data);
                        var uploads = Path.Combine(_env.WebRootPath, "images", "Pet_Images");
                        Directory.CreateDirectory(uploads);
                        var fileName = Guid.NewGuid().ToString() + ".jpg";
                        var filePath = Path.Combine(uploads, fileName);
                        System.IO.File.WriteAllBytes(filePath, bytes);
                        photoFileName = fileName;
                    }
                }
                else if (vm.Photo != null && vm.Photo.Length > 0)
                {
                    var uploads = Path.Combine(_env.WebRootPath, "images", "Pet_Images");
                    Directory.CreateDirectory(uploads);
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.Photo.FileName);
                    var filePath = Path.Combine(uploads, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await vm.Photo.CopyToAsync(stream);
                    }
                    photoFileName = fileName;
                }
                else
                {
                    photoFileName = "noimage.png"; // Default image
                }

                var entity = new Pets
                {
                    Name = vm.Name,
                    PetType = vm.PetType,
                    Breed = vm.Breed,
                    Gender = vm.Gender,
                    Age = vm.Age,
                    Weight = vm.Weight,
                    MedicalNotes = vm.MedicalNotes,
                    Photo = photoFileName, // Store only file name
                    Email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name,
                    User= null!
                };

                db.Pets.Add(entity);
                await db.SaveChangesAsync();
                TempData["Info"] = "Pet created successfully!";
                return RedirectToAction(nameof(ProfilePet));
            }

            ViewBag.GenderOptions = hp.GetGenderOptions(Localizer);
            ViewBag.PetTypeOptions = hp.GetPetTypeOptionsForPet(Localizer);
            return View(vm);
        }

        public async Task<IActionResult> UpdatePetInfo(int? id)
        {
            if (id == null) return NotFound();

            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
            var pet = await db.Pets.FirstOrDefaultAsync(m => m.PetId == id && m.Email == userEmail);
            if (pet == null) return NotFound();

            var vm = new PetUpdateVM
            {
                PetId = pet.PetId,
                Name = pet.Name,
                PetType = pet.PetType,
                Breed = pet.Breed,
                Gender = pet.Gender,
                Age = pet.Age,
                Weight = pet.Weight,
                MedicalNotes = pet.MedicalNotes,
                PhotoURL = pet.Photo
            };

            ViewBag.GenderOptions = hp.GetGenderOptions(Localizer);
            ViewBag.PetTypeOptions = hp.GetPetTypeOptionsForPet(Localizer);
            ViewBag.CurrentPhoto = pet.Photo;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePetInfo(PetUpdateVM vm, string? CroppedPhoto="")
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
            var dbPet = await db.Pets.FirstOrDefaultAsync(m => m.PetId == vm.PetId && m.Email == userEmail);
            if (dbPet == null) return NotFound();

            if (ModelState.IsValid)
            {
                bool hasChanges = false;
                if (vm.Name != dbPet.Name) 
                {
                    dbPet.Name = vm.Name;
                    hasChanges = true; 
                }
                if(vm.PetType != dbPet.PetType) 
                {
                    dbPet.PetType = vm.PetType;
                    hasChanges = true;
                }
                if(vm.Breed != dbPet.Breed)
                {
                    dbPet.Breed = vm.Breed;
                    hasChanges = true;
                }
                if(vm.Gender != dbPet.Gender)
                {
                    dbPet.Gender = vm.Gender;
                    hasChanges = true;
                }
                if(vm.Age != dbPet.Age)
                {
                    dbPet.Age = vm.Age;
                    hasChanges = true;
                }
                if(vm.Weight != dbPet.Weight)
                {
                    dbPet.Weight = vm.Weight;
                    hasChanges = true;
                }
                if(vm.MedicalNotes != dbPet.MedicalNotes)
                {
                    dbPet.MedicalNotes = vm.MedicalNotes;
                    hasChanges = true;
                }

                if (!string.IsNullOrEmpty(CroppedPhoto))
                {
                    var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
                    if (!string.IsNullOrEmpty(base64Data))
                    {
                        var bytes = Convert.FromBase64String(base64Data);
                        var uploads = Path.Combine(_env.WebRootPath, "images", "Pet_Images");
                        Directory.CreateDirectory(uploads);
                        var fileName = Guid.NewGuid().ToString() + ".jpg";
                        var filePath = Path.Combine(uploads, fileName);
                        System.IO.File.WriteAllBytes(filePath, bytes);

                        //Delete old images replace new one
                        if (!string.IsNullOrEmpty(dbPet.Photo))
                        {
                            hp.DeletePhoto(dbPet.Photo, Path.Combine("images", "Pet_Images"));
                        }
                        dbPet.Photo = fileName;
                        hasChanges = true;
                    }
                }
                else if (vm.Photo != null)
                {
                    if (!string.IsNullOrEmpty(dbPet.Photo))
                    {
                        hp.DeletePhoto(dbPet.Photo, Path.Combine("images", "Pet_Images"));
                    }
                    dbPet.Photo = hp.SavePhoto(vm.Photo, Path.Combine("images", "Pet_Images"));
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    db.Update(dbPet);
                    await db.SaveChangesAsync();
                    TempData["Info"] = "Pet updated successfully!";
                }
                else
                {
                    TempData["Info"] = "No changes were made.";
                }
                return RedirectToAction(nameof(ProfilePet));
            }

            ViewBag.GenderOptions = hp.GetGenderOptions(Localizer);
            ViewBag.PetTypeOptions = hp.GetPetTypeOptionsForPet(Localizer);
            ViewBag.CurrentPhoto = dbPet.Photo;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePet(int id)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity.Name;
            var pet = await db.Pets.FirstOrDefaultAsync(m => m.PetId == id && m.Email == userEmail);

            if (pet != null)
            {
                // Delete image file if not default
                if (!string.IsNullOrEmpty(pet.Photo) && !pet.Photo.EndsWith("noimage.png"))
                {
                    try
                    {
                        var filePath = Path.Combine(_env.WebRootPath, "images", "Pet_Images", pet.Photo);
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete file: {ex}");
                    }
                }

                db.Pets.Remove(pet);
                await db.SaveChangesAsync();
                TempData["Info"] = "Pet deleted successfully!";
            }

            return RedirectToAction(nameof(ProfilePet));
        }
    }
}