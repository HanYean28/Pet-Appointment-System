using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PawfectGrooming.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using X.PagedList;
using X.PagedList.Extensions;


namespace PawfectGrooming.Controllers;

// [Authorize(Roles = "Admin")]
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserContext db;
    private readonly Helper hp;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly IStringLocalizer<Resources.Resources> Localizer;
    // Changed from dynamic to bool and initialized to avoid runtime binder on null
    private bool showAll = false;
    private const int MAX_IMAGES = 6;
    private const long MAX_TOTAL_UPLOAD_BYTES = 8L * 1024L * 1024L; // 8 MB aggregate upload limit

    public AdminController(UserContext db, Helper hp, IConfiguration config, IWebHostEnvironment env, IStringLocalizer<Resources.Resources> localizer)
    {
        this.db = db;
        this.hp = hp;
        _config = config;
        _env = env;
        Localizer = localizer;
    }

    // POST: /Admin/ToggleUserStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleUserStatus(string email, string returnUrl = null)
    {
        if (string.IsNullOrEmpty(email))
        {
            TempData["Error"] = "Invalid request.";
            return RedirectToAction(nameof(MemberOverview));
        }

        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(MemberOverview));
        }

        user.IsActive = !user.IsActive;

        // When deactivating, cancel (mark inactive) upcoming bookings so they cannot be used.
        if (!user.IsActive)
        {
            var upcoming = db.Bookings
                .Where(b => b.UserEmail == email && b.Date >= DateTime.Today && b.IsActive)
                .ToList();

            foreach (var b in upcoming)
            {
                b.IsActive = false;
            }
        }

        db.SaveChanges();

        if (user.IsActive)
            TempData["Info"] = "User activated.";
        else
            TempData["Info"] = "User deactivated. Upcoming appointments cancelled.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(MemberOverview));
    }
    public IActionResult PackageMaintenance(string? name, string? sort, string? dir, int page = 1)
    {
        if (db == null || db.Packages == null)
        {
            return View(new List<Package>().ToPagedList(1, 10));
        }

        ViewBag.Name = name = name?.Trim() ?? "";
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        var searched = db.Packages.AsQueryable().Where(s => s.Name.Contains(name));

        // (3) Paging guard
        if (page < 1)
        {
            return RedirectToAction(nameof(PackageMaintenance), new { name, sort, dir, page = 1 });
        }

        List<Package> list;

        // EF cannot translate complex CLR types like List<string> (Features) into SQL.
        // If user requested sort by Features, materialize and sort in-memory.
        try
        {
            if (string.Equals(sort, "Features", StringComparison.OrdinalIgnoreCase))
            {
                list = searched.ToList(); // materialize
                list = dir == "des"
                    ? list.OrderByDescending(s => string.Join(",", s.Features ?? Enumerable.Empty<string>())).ToList()
                    : list.OrderBy(s => string.Join(",", s.Features ?? Enumerable.Empty<string>())).ToList();

            }
            else
            {
                Expression<Func<Package, object>> fn = sort switch
                {
                    "Name" => s => s.Name,
                    "Description" => s => s.Description,
                    "Price" => s => s.Price,
                    "Pet Type" => s => s.PetType,
                    "Image URL" => s => s.ImageURL,
                    _ => s => s.Name
                };

                var ordered = dir == "des"
                    ? searched.OrderByDescending(fn)
                    : searched.OrderBy(fn);

                list = ordered.ToList();
            }
        }
        catch (JsonException)
        {
            list = searched.Select(s => new Package
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                PetType = s.PetType,
                ImageURL = s.ImageURL,
                Features = new List<string>()
            }).ToList();
        }

        var m = list.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(nameof(PackageMaintenance), new { name, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_PackageList", m);
        }

        return View(m);
    }

    public IActionResult ServiceMaintenance(string? name, string? sort, string? dir, int page = 1)
    {
        if (db == null || db.ServiceOptions == null)
        {
            return View(new List<ServiceOption>().ToPagedList(1, 10));
        }

        ViewBag.Name = name = name?.Trim() ?? "";
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        var searched = db.ServiceOptions.AsQueryable().Where(s => s.Name.Contains(name));

        // (3) Paging guard
        if (page < 1)
        {
            return RedirectToAction(nameof(ServiceMaintenance), new { name, sort, dir, page = 1 });
        }

        List<ServiceOption> list;

        // EF cannot translate complex CLR types like List<string> (Features) into SQL.
        // If user requested sort by Features, materialize and sort in-memory.
        try
        {
            if (string.Equals(sort, "Features", StringComparison.OrdinalIgnoreCase))
            {
                list = searched.ToList(); // materialize
                list = dir == "des"
                    ? list.OrderByDescending(s => string.Join(",", s.Features ?? Enumerable.Empty<string>())).ToList()
                    : list.OrderBy(s => string.Join(",", s.Features ?? Enumerable.Empty<string>())).ToList();

            }
            else
            {
                Expression<Func<ServiceOption, object>> fn = sort switch
                {
                    "Name" => s => s.Name,
                    "Description" => s => s.Description,
                    "Price" => s => s.Price,
                    "Pet Type" => s => s.PetType,
                    "Image URL" => s => s.ImageURL,
                    _ => s => s.Name
                };

                var ordered = dir == "des"
                    ? searched.OrderByDescending(fn)
                    : searched.OrderBy(fn);

                list = ordered.ToList();
            }
        }
        catch (JsonException)
        {
            list = searched.Select(s => new ServiceOption
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                PetType = s.PetType,
                ImageURL = s.ImageURL,
                Features = new List<string>()
            }).ToList();
        }

        var m = list.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(nameof(ServiceMaintenance), new { name, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_ServiceOptionList", m);
        }

        return View(m);
    }
    public IActionResult AdminOverview(string? name, string? sort, string? dir, int page = 1)
    {
        if (db == null || db.Users == null)
        {
            return View(new List<User>().ToPagedList(1, 10));
        }

        ViewBag.Name = name = name?.Trim() ?? "";
        // Filter only "Admin" users
        var searched = db.Users.Where(s => EF.Property<string>(s, "Discriminator") == "Admin" && s.Name.Contains(name));
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Expression<Func<User, object>> fn = sort switch
        {
            "Name" => s => s.Name,
            "Email" => s => s.Email,
            "Gender" => s => s.Gender,
            "Photo" => s => s.PhotoURL,
            _ => s => s.Name
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        if (page < 1)
        {
            return RedirectToAction(nameof(AdminOverview), new { name, sort, dir, page = 1 });
        }

        var list = sorted.ToList();
        var m = list.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(nameof(AdminOverview), new { name, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_AdminList", m);
        }

        return View(m);
    }
    public IActionResult MemberOverview(string? name, string? sort, string? dir, int page = 1)
    {
        if (db == null || db.Users == null)
        {
            return View(new List<User>().ToPagedList(1, 10));
        }

        ViewBag.Name = name = name?.Trim() ?? "";
        // Filter only "Member" users
        var searched = db.Users.Where(s => EF.Property<string>(s, "Discriminator") == "Member" && s.Name.Contains(name));
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Expression<Func<User, object>> fn = sort switch
        {
            "Name" => s => s.Name,
            "Email" => s => s.Email,
            "Gender" => s => s.Gender,
            "Photo" => s => s.PhotoURL,
            _ => s => s.Name
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        if (page < 1)
        {
            return RedirectToAction(nameof(MemberOverview), new { name, sort, dir, page = 1 });
        }

        var list = sorted.ToList();
        var m = list.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(nameof(MemberOverview), new { name, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_MemberList", m);
        }

        return View(m);
    }

    //GET: Admin/Detail/ServiceOption
    public IActionResult Detail(int? id)
    {
        var model = db.ServiceOptions.Find(id);

        if (model == null)
        {
            return RedirectToAction("Index");
        }

        return View(model);
    }
    //GET: Admin/Detail/Package
    public IActionResult Detail2(int? id)
    {
        var model = db.Packages.Find(id);

        if (model == null)
        {
            return RedirectToAction("Index");
        }

        return View(model);
    }


    /// GET: Admin/Insert (render create form for ServiceOption)
    public IActionResult Insert()
    {
        // Provide pet type options for the dropdown in the create view
        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);

        // Provide an empty features textarea fallback
        ViewBag.FeaturesText = string.Empty;

        // Pass an empty model so the view helpers (asp-for) work
        return View(new ServiceOption
        {
            Name = string.Empty,
            Description = string.Empty,
            Price = 0,
            Features = new List<string>(),
            PetType = string.Empty,
            ImageURL = new List<string>()
        });
    }

    // POST: Admin/Insert/ServiceOption
    [HttpPost]
    public IActionResult Insert(ServiceOption vm, string? FeaturesText, IFormFileCollection? Photos, string? CroppedPhoto)
    {
        // Parse features
        List<string> parsedFeatures = new List<string>();
        if (Request.HasFormContentType && Request.Form.ContainsKey("Features"))
        {
            parsedFeatures = Request.Form["Features"]
                .Select(x => x!.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(FeaturesText))
        {
            parsedFeatures = FeaturesText
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        // Server-side validations
        if (parsedFeatures.Count == 0)
            ModelState.AddModelError("Features", "Please add at least one feature.");

        if (string.IsNullOrWhiteSpace(vm.Name))
            ModelState.AddModelError("Name", "Name is required.");

        if (string.IsNullOrWhiteSpace(vm.PetType))
            ModelState.AddModelError("PetType", "Please select a pet type.");

        if (!string.IsNullOrWhiteSpace(vm.Name) && db.ServiceOptions.Any(s => s.Name == vm.Name.Trim()))
            ModelState.AddModelError("Name", "A service with this name already exists.");

        // Collect saved filenames (support multiple uploads + CroppedPhoto)
        var savedFiles = new List<string>();

        // If there's a cropped photo, save it first and mark to skip the first uploaded file (avoids duplicate)
        var skipFirstUploaded = false;
        if (!string.IsNullOrEmpty(CroppedPhoto))
        {
            var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
            if (!string.IsNullOrEmpty(base64Data))
            {
                var bytes = Convert.FromBase64String(base64Data);
                var fileName = Guid.NewGuid() + ".jpg";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Service_Images", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                System.IO.File.WriteAllBytes(savePath, bytes);
                savedFiles.Add(fileName);

                // Avoid saving the original first uploaded file again
                skipFirstUploaded = true;
            }
        }

        // Save newly uploaded files, skipping the first if we already saved a cropped replacement
        if (Photos != null && Photos.Count > 0)
        {
            for (int i = 0; i < Photos.Count; i++)
            {
                if (skipFirstUploaded && i == 0) continue; // skip original that was cropped

                var f = Photos[i];
                var err = hp.ValidatePhoto(f);
                if (!string.IsNullOrEmpty(err))
                {
                    ModelState.AddModelError("Photos", err);
                    break;
                }
                var fn = hp.SavePhoto(f, Path.Combine("images", "Service_Images"));
                if (!string.IsNullOrEmpty(fn)) savedFiles.Add(fn);
            }
        }
        else if (vm.ImageURL != null && vm.ImageURL.Any() && savedFiles.Count == 0)
        {
            // preserve any filenames already present in vm.ImageURL if no new uploads
            savedFiles.AddRange(vm.ImageURL);
        }

        if (ModelState.IsValid)
        {
            var entity = new ServiceOption
            {
                Name = vm.Name?.Trim() ?? string.Empty,
                Description = vm.Description ?? string.Empty,
                Price = vm.Price,
                Features = parsedFeatures,
                PetType = vm.PetType ?? string.Empty,
                ImageURL = savedFiles.Any() ? savedFiles : (vm.ImageURL ?? new List<string>())
            };

            db.ServiceOptions.Add(entity);
            db.SaveChanges();

            TempData["Info"] = "Service option inserted.";
            return RedirectToAction(nameof(ServiceMaintenance));
        }

        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);
        ViewBag.FeaturesText = string.Join(Environment.NewLine, parsedFeatures);
        return View(vm);
    }

    // GET: Admin/Update/ServiceOption
    public IActionResult Update(int? id)
    {
        if (id == null) return RedirectToAction("ServiceMaintenance");

        var s = db.ServiceOptions.Find(id.Value);
        if (s == null) return RedirectToAction("ServiceMaintenance");

        // Populate pet type dropdown
        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);

        // Provide features as multiline text for textarea fallback
        ViewBag.FeaturesText = (s.Features ?? Enumerable.Empty<string>()).Any()
            ? string.Join(Environment.NewLine, s.Features ?? Enumerable.Empty<string>())
            : string.Empty;

        return View(s);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(ServiceOption vm, string? FeaturesText, IFormFileCollection? Photos, string? CroppedPhoto)
    {
        if (vm == null) return RedirectToAction("ServiceMaintenance");

        var entity = db.ServiceOptions.Find(vm.Id);
        if (entity == null) return RedirectToAction("ServiceMaintenance");

        // Debug info for troubleshooting file binding / model state
        TempData["DebugFiles"] = $"Photos_param_count={Photos?.Count ?? 0}; FormFiles={Request.Form.Files.Count}; CroppedPhoto_present={!string.IsNullOrEmpty(CroppedPhoto)}; ExistingImages_posted={Request.Form["ExistingImages"].Count};";

        // Normalize / parse features
        List<string> parsedFeatures;
        if (!string.IsNullOrWhiteSpace(FeaturesText))
        {
            parsedFeatures = FeaturesText
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }
        else if (vm.Features != null && vm.Features.Any())
        {
            parsedFeatures = vm.Features
                            .Where(x => x != null)
                            .Select(x => x!.Trim())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();
        }
        else
        {
            parsedFeatures = new List<string>();
        }

        // Server-side rules
        if (parsedFeatures.Count == 0)
            ModelState.AddModelError("Features", "Please add at least one feature.");
        if (string.IsNullOrWhiteSpace(vm.PetType))
            ModelState.AddModelError("PetType", "Please select a pet type.");

        // New image handling: read kept existing images from form
        var keptExisting = Request.Form["ExistingImages"].ToArray() ?? Array.Empty<string>();

        // Delete files removed by the user (present in DB but not in keptExisting)
        entity.ImageURL ??= new List<string>();
        var toDelete = entity.ImageURL.Except(keptExisting ?? Array.Empty<string>()).ToList();
        foreach (var old in toDelete)
        {
            try { hp.DeletePhoto(old, Path.Combine("images", "Service_Images")); } catch { }
        }

        // Start final list with kept ones
        var finalList = new List<string>(keptExisting ?? Array.Empty<string>());

        // handle base64 single cropped photo (adds as new) and skip first uploaded original
        var skipFirstUploaded = false;
        if (!string.IsNullOrEmpty(CroppedPhoto))
        {
            var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
            if (!string.IsNullOrEmpty(base64Data))
            {
                var bytes = Convert.FromBase64String(base64Data);
                var fileName = Guid.NewGuid() + ".jpg";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Service_Images", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                System.IO.File.WriteAllBytes(savePath, bytes);
                finalList.Add(fileName);

                skipFirstUploaded = true;
            }
        }

        // handle newly uploaded files (Photos), skipping the first if cropped replacement was saved
        if (Photos != null && Photos.Count > 0)
        {
            for (int i = 0; i < Photos.Count; i++)
            {
                if (skipFirstUploaded && i == 0) continue;

                var f = Photos[i];
                var err = hp.ValidatePhoto(f);
                if (!string.IsNullOrEmpty(err))
                {
                    ModelState.AddModelError("Photos", err);
                    break;
                }
                var fn = hp.SavePhoto(f, Path.Combine("images", "Service_Images"));
                if (!string.IsNullOrEmpty(fn)) finalList.Add(fn);
            }
        }

        if (ModelState.IsValid)
        {
            bool hasChanges = false;

            if (!string.Equals(entity.Name, vm.Name?.Trim(), StringComparison.Ordinal))
            {
                entity.Name = vm.Name?.Trim() ?? string.Empty;
                hasChanges = true;
            }

            if (!string.Equals(entity.Description, vm.Description, StringComparison.Ordinal))
            {
                entity.Description = vm.Description ?? string.Empty;
                hasChanges = true;
            }

            if (entity.Price != vm.Price)
            {
                entity.Price = vm.Price;
                hasChanges = true;
            }

            if (!string.Equals(entity.PetType, vm.PetType, StringComparison.Ordinal))
            {
                entity.PetType = vm.PetType ?? string.Empty;
                hasChanges = true;
            }

            // Features (replace)
            if (!parsedFeatures.SequenceEqual(entity.Features ?? Enumerable.Empty<string>()))
            {
                entity.Features = parsedFeatures;
                hasChanges = true;
            }

            // assign final image list
            if (!finalList.SequenceEqual(entity.ImageURL ?? Enumerable.Empty<string>()))
            {
                entity.ImageURL = finalList;
                hasChanges = true;
            }

            if (hasChanges)
            {
                db.SaveChanges();
                TempData["Info"] = "Service option updated.";
            }
            else
            {
                TempData["Info"] = "No changes were made.";
            }

            return RedirectToAction(nameof(ServiceMaintenance));
        }

        // If validation failed, repopulate dropdown and features text and return view
        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);
        ViewBag.FeaturesText = string.Join(Environment.NewLine, parsedFeatures);
        return View(vm);
    }

    // --- Package Insert2 / Update2 (GET + POST) ---
    // GET: Admin/Insert2 (render create form for Package)
    public IActionResult Insert2()
    {
        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);
        ViewBag.FeaturesText = string.Empty;

        return View(new Package
        {
            Name = string.Empty,
            Description = string.Empty,
            Price = 0,
            Features = new List<string>(),
            PetType = string.Empty,
            ImageURL = new List<string>()
        });
    }

    // POST: Admin/Insert2/Package
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Insert2(Package vm, string? FeaturesText, IFormFileCollection? Photos, string? CroppedPhoto)
    {
        // Parse features
        List<string> parsedFeatures = new List<string>();
        if (Request.HasFormContentType && Request.Form.ContainsKey("Features"))
        {
            parsedFeatures = Request.Form["Features"]
                .Select(x => x!.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(FeaturesText))
        {
            parsedFeatures = FeaturesText
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        // Server-side validations
        if (parsedFeatures.Count == 0)
            ModelState.AddModelError("Features", "Please add at least one feature.");
        if (string.IsNullOrWhiteSpace(vm.Name))
            ModelState.AddModelError("Name", "Name is required.");
        if (string.IsNullOrWhiteSpace(vm.PetType))
            ModelState.AddModelError("PetType", "Please select a pet type.");

        if (!string.IsNullOrWhiteSpace(vm.Name) && db.Packages.Any(p => p.Name == vm.Name.Trim()))
            ModelState.AddModelError("Name", "A package with this name already exists.");

        // Collect saved filenames
        var savedFiles = new List<string>();

        var skipFirstUploaded = false;
        if (!string.IsNullOrEmpty(CroppedPhoto))
        {
            var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
            if (!string.IsNullOrEmpty(base64Data))
            {
                var bytes = Convert.FromBase64String(base64Data);
                var fileName = Guid.NewGuid() + ".jpg";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Package_Images", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                System.IO.File.WriteAllBytes(savePath, bytes);
                savedFiles.Add(fileName);

                skipFirstUploaded = true;
            }
        }

        if (Photos != null && Photos.Count > 0)
        {
            for (int i = 0; i < Photos.Count; i++)
            {
                if (skipFirstUploaded && i == 0) continue;

                var f = Photos[i];
                var err = hp.ValidatePhoto(f);
                if (!string.IsNullOrEmpty(err))
                {
                    ModelState.AddModelError("Photos", err);
                    break;
                }
                var fn = hp.SavePhoto(f, Path.Combine("images", "Package_Images"));
                if (!string.IsNullOrEmpty(fn)) savedFiles.Add(fn);
            }
        }
        else if (vm.ImageURL != null && vm.ImageURL.Any() && savedFiles.Count == 0)
        {
            savedFiles.AddRange(vm.ImageURL);
        }

        if (ModelState.IsValid)
        {
            var entity = new Package
            {
                Name = vm.Name?.Trim() ?? string.Empty,
                Description = vm.Description ?? string.Empty,
                Price = vm.Price,
                Features = parsedFeatures,
                PetType = vm.PetType ?? string.Empty,
                ImageURL = savedFiles.Any() ? savedFiles : (vm.ImageURL ?? new List<string>())
            };

            db.Packages.Add(entity);
            db.SaveChanges();

            TempData["Info"] = "Package inserted.";
            return RedirectToAction(nameof(PackageMaintenance));
        }

        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);
        ViewBag.FeaturesText = string.Join(Environment.NewLine, parsedFeatures);
        return View(vm);
    }

    // GET: Admin/Update2/Package
    public IActionResult Update2(int? id)
    {
        if (id == null) return RedirectToAction(nameof(PackageMaintenance));

        var p = db.Packages.Find(id.Value);
        if (p == null) return RedirectToAction(nameof(PackageMaintenance));

        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);
        ViewBag.FeaturesText = (p.Features ?? Enumerable.Empty<string>()).Any()
   ? string.Join(Environment.NewLine, p.Features ?? Enumerable.Empty<string>())
            : string.Empty;

        return View(p);
    }

    // POST: Admin/Update2/Package
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update2(Package vm, string? FeaturesText, IFormFileCollection? Photos, string? CroppedPhoto)
    {
        if (vm == null) return RedirectToAction(nameof(PackageMaintenance));

        var entity = db.Packages.Find(vm.Id);
        if (entity == null) return RedirectToAction(nameof(PackageMaintenance));

        // Debug info for troubleshooting file binding / model state
        TempData["DebugFiles"] = $"Photos_param_count={Photos?.Count ?? 0}; FormFiles={Request.Form.Files.Count}; CroppedPhoto_present={!string.IsNullOrEmpty(CroppedPhoto)}; ExistingImages_posted={Request.Form["ExistingImages"].Count};";

        // Normalize / parse features
        List<string> parsedFeatures;
        if (!string.IsNullOrWhiteSpace(FeaturesText))
        {
            parsedFeatures = FeaturesText
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }
        else if (vm.Features != null && vm.Features.Any())
        {
            parsedFeatures = vm.Features
                       .Where(x => x != null)
                       .Select(x => x!.Trim())
                       .Where(x => !string.IsNullOrEmpty(x))
                       .ToList();
        }
        else
        {
            parsedFeatures = new List<string>();
        }

        // Server-side rules
        if (parsedFeatures.Count == 0)
            ModelState.AddModelError("Features", "Please add at least one feature.");
        if (string.IsNullOrWhiteSpace(vm.PetType))
            ModelState.AddModelError("PetType", "Please select a pet type.");

        // Read kept existing images
        var keptExisting = Request.Form["ExistingImages"].ToArray() ?? Array.Empty<string>();

        // Delete files removed by the user (present in DB but not in keptExisting)
        entity.ImageURL ??= new List<string>();
        var toDelete = entity.ImageURL.Except(keptExisting ?? Array.Empty<string>()).ToList();
        foreach (var old in toDelete)
        {
            try { hp.DeletePhoto(old, Path.Combine("images", "Package_Images")); } catch { }
        }

        var finalList = new List<string>(keptExisting ?? Array.Empty<string>());

        // handle base64 cropped and skip first uploaded original if applicable
        var skipFirstUploaded = false;
        if (!string.IsNullOrEmpty(CroppedPhoto))
        {
            var base64Data = System.Text.RegularExpressions.Regex.Match(CroppedPhoto, @"^data:image\/[a-zA-Z]+;base64,(?<data>.+)$").Groups["data"].Value;
            if (!string.IsNullOrEmpty(base64Data))
            {
                var bytes = Convert.FromBase64String(base64Data);
                var fileName = Guid.NewGuid() + ".jpg";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Package_Images", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                System.IO.File.WriteAllBytes(savePath, bytes);
                finalList.Add(fileName);

                skipFirstUploaded = true;
            }
        }

        // handle newly uploaded files
        if (Photos != null && Photos.Count > 0)
        {
            for (int i = 0; i < Photos.Count; i++)
            {
                if (skipFirstUploaded && i == 0) continue;

                var f = Photos[i];
                var err = hp.ValidatePhoto(f);
                if (!string.IsNullOrEmpty(err))
                {
                    ModelState.AddModelError("Photos", err);
                    break;
                }
                var fn = hp.SavePhoto(f, Path.Combine("images", "Package_Images"));
                if (!string.IsNullOrEmpty(fn)) finalList.Add(fn);
            }
        }

        if (ModelState.IsValid)
        {
            bool hasChanges = false;

            if (!string.Equals(entity.Name, vm.Name?.Trim(), StringComparison.Ordinal))
            {
                entity.Name = vm.Name?.Trim() ?? string.Empty;
                hasChanges = true;
            }

            if (!string.Equals(entity.Description, vm.Description, StringComparison.Ordinal))
            {
                entity.Description = vm.Description ?? string.Empty;
                hasChanges = true;
            }

            if (entity.Price != vm.Price)
            {
                entity.Price = vm.Price;
                hasChanges = true;
            }

            if (!string.Equals(entity.PetType, vm.PetType, StringComparison.Ordinal))
            {
                entity.PetType = vm.PetType ?? string.Empty;
                hasChanges = true;
            }

            if (!parsedFeatures.SequenceEqual(entity.Features ?? Enumerable.Empty<string>()))
            {
                entity.Features = parsedFeatures;
                hasChanges = true;
            }

            if (!finalList.SequenceEqual(entity.ImageURL ?? Enumerable.Empty<string>()))
            {
                entity.ImageURL = finalList;
                hasChanges = true;
            }

            if (hasChanges)
            {
                db.SaveChanges();
                TempData["Info"] = "Package updated.";
            }
            else
            {
                TempData["Info"] = "No changes were made.";
            }

            return RedirectToAction(nameof(PackageMaintenance));
        }

        // If validation failed, repopulate view data and return view
        ViewBag.PetTypes = hp.GetPetTypeOptions(Localizer);
        ViewBag.FeaturesText = string.Join(Environment.NewLine, parsedFeatures);
        return View(vm);
    }

    // POST: Admin/Delete/ServiceOption
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int? id)
    {
        if (id == null)
        {
            return RedirectToAction(nameof(ServiceMaintenance));
        }

        var s = db.ServiceOptions.Find(id.Value);

        if (s != null)
        {
            // Delete associated image files if present
            if (s.ImageURL != null && s.ImageURL.Any())
            {
                foreach (var img in s.ImageURL.ToList())
                {
                    try
                    {
                        hp.DeletePhoto(img, Path.Combine("images", "Service_Images"));
                    }
                    catch
                    {
                        // swallow; don't prevent deletion due to file IO issues
                    }
                }
            }

            db.ServiceOptions.Remove(s);
            db.SaveChanges();

            TempData["Info"] = "Record deleted.";
        }

        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrWhiteSpace(referer))
        {
            return Redirect(referer);
        }

        return RedirectToAction(nameof(ServiceMaintenance));
    }
    // POST: Admin/Delete2/Package
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete2(int? id)
    {
        if (id == null)
        {
            return RedirectToAction(nameof(PackageMaintenance));
        }

        var p = db.Packages.Find(id.Value);

        if (p != null)
        {
            // Delete associated image files if present
            if (p.ImageURL != null && p.ImageURL.Any())
            {
                foreach (var img in p.ImageURL.ToList())
                {
                    try
                    {
                        hp.DeletePhoto(img, Path.Combine("images", "Package_Images"));
                    }
                    catch
                    {
                        // swallow; don't prevent deletion due to file IO issues
                    }
                }
            }

            db.Packages.Remove(p);
            db.SaveChanges();

            TempData["Info"] = "Record deleted.";
        }

        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrWhiteSpace(referer))
        {
            return Redirect(referer);
        }

        return RedirectToAction(nameof(PackageMaintenance));
    }
    // GET: Admin/AppointmentMaintenance
    public IActionResult AppointmentMaintenance(string email, string status, int? serviceId, int? packageId, string? sort, string? dir, int page = 1, bool? showAllParam = null)
    {
        // If caller passed explicit query parameter, use it; otherwise fall back to stored field or querystring.
        if (showAllParam.HasValue)
        {
            this.showAll = showAllParam.Value;
        }
        else if (Request.Query.ContainsKey("showAll"))
        {
            // tolerate "true"/"false" strings
            bool parsed;
            var showAllCookie = Request.Cookies["showAll"];
            if (bool.TryParse(showAllCookie, out parsed))
                this.showAll = parsed;
        }

        ViewBag.Email = email;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.ShowAll = this.showAll;

        // Status options for dropdown
        ViewBag.StatusOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "All Statuses" },
            new SelectListItem { Value = "PendingPayment", Text = "Pending Payment" },
            new SelectListItem { Value = "Completed", Text = "Completed" },
            new SelectListItem { Value = "Deposit", Text = "Deposit" },
            new SelectListItem { Text = "Cancelled", Value = "Cancelled" },
        };

        var query = db.Bookings
            .Include(b => b.Pet)
            .Include(b => b.Service)
            .Include(b => b.Package)
            .Where(b => b.IsActive || b.Status == "Cancelled") // Show active and cancelled
            .AsQueryable();

        if (!string.IsNullOrEmpty(email))
            query = query.Where(b => b.UserEmail.Contains(email));

        if (!string.IsNullOrEmpty(status))
            query = query.Where(b => b.Status == status);

        if (serviceId.HasValue)
            query = query.Where(b => b.ServiceId == serviceId.Value);

        if (packageId.HasValue)
            query = query.Where(b => b.PackageId == packageId.Value);

        // IMPORTANT: choose filter behavior here:
        // If you want only active bookings in the admin list, uncomment the next line:
        //if (!this.showAll)
        //    query = query.Where(b => b.IsActive);

        // Enhanced sorting - added Pet/Service/Package sorting so headers in the partial can toggle sort
        query = sort switch
        {
            "Date" => dir == "asc" ? query.OrderBy(b => b.Date) : query.OrderByDescending(b => b.Date),
            "Email" => dir == "asc" ? query.OrderBy(b => b.UserEmail) : query.OrderByDescending(b => b.UserEmail),
            "Status" => dir == "asc" ? query.OrderBy(b => b.Status) : query.OrderByDescending(b => b.Status),
            "Price" => dir == "asc" ? query.OrderBy(b => b.Price) : query.OrderByDescending(b => b.Price),
            "Time" => dir == "asc" ? query.OrderBy(b => b.Time) : query.OrderByDescending(b => b.Time),
            "Pet" => dir == "asc" ? query.OrderBy(b => b.Pet != null ? b.Pet.Name : "") : query.OrderByDescending(b => b.Pet != null ? b.Pet.Name : ""),
            "Service" => dir == "asc" ? query.OrderBy(b => b.Service != null ? b.Service.Name : "") : query.OrderByDescending(b => b.Service != null ? b.Service.Name : ""),
            "Package" => dir == "asc" ? query.OrderBy(b => b.Package != null ? b.Package.Name : "") : query.OrderByDescending(b => b.Package != null ? b.Package.Name : ""),
            _ => query.OrderByDescending(b => b.Date)
        };

        var paged = query.ToPagedList(page, 15);

        if (Request.IsAjax())
        {
            return PartialView("_AppointmentList", paged);
        }

        return View(paged);
    }
    // GET: Admin/Update/Appointment
    public IActionResult UpdateAppointment(int? id)
    {
        if (id == null) return RedirectToAction("AppointmentList");

        var booking = db.Bookings
            .Include(b => b.Service)
            .Include(b => b.Package)
            .Include(b => b.Pet)
            .FirstOrDefault(b => b.Id == id.Value);

        if (booking == null) return RedirectToAction("AppointmentList");

        ViewBag.ServiceOptions = db.ServiceOptions.ToList();
        ViewBag.PackageOptions = db.Packages.ToList();

        // Get all pets for the user
        var pets = db.Pets.Where(p => p.Email == booking.UserEmail).ToList();

        // Always include the current pet, even if not in the filtered list
        if (booking.Pet != null && !pets.Any(p => p.PetId == booking.PetId))
        {
            pets.Add(booking.Pet);
        }

        ViewBag.PetOptions = pets
            .Select(p => new SelectListItem
            {
                Value = p.PetId.ToString(),
                Text = p.Name,
                Selected = (booking.PetId == p.PetId)
            })
            .ToList();

        return View(booking);
    }

    // POST: Admin/Update/Appointment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateAppointment(Booking vm)
    {
        var entity = db.Bookings.FirstOrDefault(b => b.Id == vm.Id);
        if (entity == null)
        {
            TempData["Error"] = "Appointment not found.";
            return RedirectToAction(nameof(AppointmentMaintenance));
        }

        bool hasChanges = false;

        // Update IsActive
        if (entity.IsActive != vm.IsActive)
        {
            entity.IsActive = vm.IsActive;
            hasChanges = true;
        }

        // If activating and status is Cancelled, set to PendingPayment
        if (vm.IsActive && entity.Status == "Cancelled")
        {
            entity.Status = "PendingPayment";
            hasChanges = true;
        }

        // Update other fields as needed...
        if (entity.PetId != vm.PetId)
        {
            entity.PetId = vm.PetId;
            hasChanges = true;
        }
        if (entity.ServiceId != vm.ServiceId)
        {
            entity.ServiceId = vm.ServiceId;
            hasChanges = true;
        }
        if (entity.PackageId != vm.PackageId)
        {
            entity.PackageId = vm.PackageId;
            hasChanges = true;
        }
        if (!string.Equals(entity.UserEmail, vm.UserEmail?.Trim(), StringComparison.Ordinal))
        {
            entity.UserEmail = vm.UserEmail?.Trim() ?? string.Empty;
            hasChanges = true;
        }
        if (entity.Date != vm.Date)
        {
            entity.Date = vm.Date;
            hasChanges = true;
        }
        if (entity.Time != vm.Time)
        {
            entity.Time = vm.Time;
            hasChanges = true;
        }
        if (entity.Price != vm.Price)
        {
            entity.Price = vm.Price;
            hasChanges = true;
        }

        if (hasChanges)
        {
            db.SaveChanges();
            TempData["Info"] = "Appointment updated. All set!";
        }
        else
        {
            TempData["Info"] = "No changes were made.";
        }

        return RedirectToAction(nameof(UpdateAppointment), new { id = vm.Id });
    }

    // POST: Admin/UpdateAppointmentStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentStatus(int id, string status)
    {
        var appointment = db.Bookings
            .Include(b => b.User)
            .Include(b => b.Pet)
            .FirstOrDefault(b => b.Id == id);

        if (appointment == null)
        {
            return Json(new { success = false, message = "Appointment not found." });
        }

        var oldStatus = appointment.Status;
        appointment.Status = status;
        
        db.SaveChanges();

        // Send SMS notification
        await SendSMSNotification(appointment, oldStatus, status);

        return Json(new { success = true, message = "Status updated successfully." });
    }

    // GET: Admin/AppointmentDetail
    public IActionResult AppointmentDetail(int? id)
    {
        if (id == null) return RedirectToAction("AppointmentMaintenance");

        var appointment = db.Bookings
            .Include(b => b.Pet)
            .Include(b => b.Service)
            .Include(b => b.Package)
            .Include(b => b.User)
            .FirstOrDefault(b => b.Id == id.Value);

        if (appointment == null) return RedirectToAction("AppointmentMaintenance");

        return View(appointment);
    }

    // GET: Admin/SaleReports
    public IActionResult SaleReports()
    {
        var startDate = DateTime.Today.AddMonths(-6);
        var endDate = DateTime.Today;

        // totals (DB-side via helper)
        var totalBookingCompletedRevenue = GetCompletedBookingRevenue(startDate, endDate);
        var allTimeCompletedRevenue = GetCompletedBookingRevenue(null, null);

        // counts for diagnostics
        var bookingsInRangeCount = db.Bookings.Count(b => b.Date >= startDate && b.Date <= endDate);
        var completedInRangeCount = db.Bookings.Count(b => b.Date >= startDate && b.Date <= endDate
                                                      && b.Status != null && b.Status.Trim().ToLower() == "completed");
        var allTimeCompletedCount = db.Bookings.Count(b => b.Status != null && b.Status.Trim().ToLower() == "completed");

        // last-6-months projection (kept for compatibility if needed by other UI)
        var bookingsInRange = db.Bookings
            .Include(b => b.Service)
            .Include(b => b.Package)
            .Where(b => b.Date >= startDate && b.Date <= endDate);

        var projected = bookingsInRange
            .Where(b => b.Status != null && b.Status.Trim().ToLower() == "completed")
            .Select(b => new
            {
                b.Date,
                ServiceId = b.ServiceId,
                PackageId = b.PackageId,
                ServiceName = b.Service != null ? b.Service.Name : "",
                PackageName = b.Package != null ? b.Package.Name : "",
                Amount = b.Price != 0m
                    ? b.Price
                    : (b.Service != null ? b.Service.Price : (b.Package != null ? b.Package.Price : 0m)),
                Count = b.Count > 0 ? b.Count : 1
            });

        // all-time completed bookings projection
        var allTimeProjected = db.Bookings
            .Include(b => b.Service)
            .Include(b => b.Package)
            .Where(b => b.Status != null && b.Status.Trim().ToLower() == "completed")
            .Select(b => new
            {
                b.Date,
                ServiceId = b.ServiceId,
                PackageId = b.PackageId,
                ServiceName = b.Service != null ? b.Service.Name : "",
                PackageName = b.Package != null ? b.Package.Name : "",
                Amount = b.Price != 0m
                    ? b.Price
                    : (b.Service != null ? b.Service.Price : (b.Package != null ? b.Package.Price : 0m)),
                Count = b.Count > 0 ? b.Count : 1
            });

    // Assign ViewBag totals
    ViewBag.TotalBookingCompletedRevenue = totalBookingCompletedRevenue;
    ViewBag.AllTimeCompletedRevenue = allTimeCompletedRevenue;

    ViewBag.TotalBookingsInRangeCount = bookingsInRangeCount;
    ViewBag.CompletedBookingsCount = completedInRangeCount;
    ViewBag.AllTimeCompletedBookings = allTimeCompletedCount;

    // ---- MONTHLY REVENUE: now uses ALL-TIME completed bookings grouped by Year/Month ----
    ViewBag.MonthlyRevenue = allTimeProjected
        .GroupBy(x => new { x.Date.Year, x.Date.Month })
        .Select(g => new
        {
            Year = g.Key.Year,
            Month = g.Key.Month,
            Revenue = g.Sum(x => x.Amount * x.Count),
            Count = g.Sum(x => x.Count)
        })
        .OrderBy(x => x.Year).ThenBy(x => x.Month)
        .ToList<object>();

    // ---- ALL-TIME service/package revenue + counts ----
    ViewBag.AllTimeServiceRevenue = allTimeProjected.Where(x => x.ServiceId != null).Sum(x => (decimal?)(x.Amount * x.Count)) ?? 0m;
    ViewBag.AllTimePackageRevenue = allTimeProjected.Where(x => x.PackageId != null).Sum(x => (decimal?)(x.Amount * x.Count)) ?? 0m;

    ViewBag.AllTimeServiceCount = allTimeProjected.Where(x => x.ServiceId != null).Sum(x => (int?)x.Count) ?? 0;
    ViewBag.AllTimePackageCount = allTimeProjected.Where(x => x.PackageId != null).Sum(x => (int?)x.Count) ?? 0;

    // Keep last-6-months service/package revenue for backward compatibility (charts previously used these)
    ViewBag.ServiceRevenue = projected.Where(x => x.ServiceId != null).Sum(x => (decimal?)(x.Amount * x.Count)) ?? 0m;
    ViewBag.PackageRevenue = projected.Where(x => x.PackageId != null).Sum(x => (decimal?)(x.Amount * x.Count)) ?? 0m;

    // ---- TOP lists: use ALL-TIME completed bookings ----
    ViewBag.TopServices = allTimeProjected
        .Where(x => x.ServiceId != null)
        .GroupBy(x => new { x.ServiceId, x.ServiceName })
        .Select(g => new
        {
            ServiceId = g.Key.ServiceId,
            ServiceName = g.Key.ServiceName,
            Revenue = g.Sum(x => x.Amount * x.Count),
            Count = g.Sum(x => x.Count)
        })
        .OrderByDescending(x => x.Revenue)
        .Take(5)
        .ToList<object>();

    ViewBag.TopPackages = allTimeProjected
        .Where(x => x.PackageId != null)
        .GroupBy(x => new { x.PackageId, x.PackageName })
        .Select(g => new
        {
            PackageId = g.Key.PackageId,
            PackageName = g.Key.PackageName,
            Revenue = g.Sum(x => x.Amount * x.Count),
            Count = g.Sum(x => x.Count)
        })
        .OrderByDescending(x => x.Revenue)
        .Take(5)
        .ToList<object>();

    // ---- STATUS DISTRIBUTION: now uses ALL-TIME bookings (counts per status) ----
    ViewBag.StatusDistribution = db.Bookings
        .GroupBy(b => b.Status)
        .Select(g => new { Status = g.Key, Count = g.Count() })
        .ToList<object>();

    // Payments (diagnostic) - keep as-is (last 6 months)
    ViewBag.TotalPaidPaymentsCount = db.Payment?.Count(p => (p.Status == "Completed" || p.Status == "Deposit") && p.Date >= startDate && p.Date <= endDate) ?? 0;
    ViewBag.TotalPaidPaymentsRevenue = db.Payment?.Where(p => (p.Status == "Completed" || p.Status == "Deposit") && p.Date >= startDate && p.Date <= endDate).Sum(p => (decimal?)p.Amount) ?? 0m;

    return View();
}

private decimal GetCompletedBookingRevenue(DateTime? start = null, DateTime? end = null)
{
    var s = start ?? DateTime.MinValue;
    var e = end ?? DateTime.MaxValue;

    var query = db.Bookings
        .Include(b => b.Service)
        .Include(b => b.Package)
        .Where(b => b.Status != null && b.Status.Trim().ToLower() == "completed"
                    && b.Date >= s && b.Date <= e);

    var total = query
        .Select(b => new
        {
            Amount = b.Price != 0m
                ? b.Price
                : (b.Service != null ? b.Service.Price : (b.Package != null ? b.Package.Price : 0m)),
            Count = b.Count > 0 ? b.Count : 1
        })
        .Sum(x => (decimal?)(x.Amount * x.Count)) ?? 0m;

    return total;
}


private async Task SendSMSNotification(Booking appointment, string oldStatus, string newStatus)
{
    try
    {
        // This is a placeholder for SMS functionality
        // In a real implementation, you would integrate with a service like Twilio
        var message = $"Your appointment status has been updated from {oldStatus} to {newStatus}. " +
                     $"Appointment Date: {appointment.Date:yyyy-MM-dd} at {appointment.Time}";

        // For now, we'll just log the message
        // In production, replace this with actual SMS sending logic
        System.Diagnostics.Debug.WriteLine($"SMS would be sent to {appointment.User?.PhoneNumber}: {message}");
        
        // Example Twilio integration (uncomment and configure):
        /*
        var accountSid = _config["Twilio:AccountSid"];
        var authToken = _config["Twilio:AuthToken"];
        var fromNumber = _config["Twilio:FromNumber"];
        
        TwilioClient.Init(accountSid, authToken);
        
        var messageResource = await MessageResource.CreateAsync(
            body: message,
            from: new PhoneNumber(fromNumber),
            to: new PhoneNumber(appointment.User?.PhoneNumber)
        );
        */
    }
    catch (Exception ex)
    {
        // Log error but don't fail the status update
        System.Diagnostics.Debug.WriteLine($"SMS sending failed: {ex.Message}");
    }
}

// POST: Admin/DeleteAppointment/
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult DeleteAppointment(int? id)
{
    if (id == null)
    {
        return RedirectToAction(nameof(AppointmentMaintenance));
    }

    var appointment = db.Bookings.Find(id.Value);

    if (appointment != null)
    {
        var images = appointment.Service?.ImageURL ?? appointment.Package?.ImageURL ?? new List<string>();
        foreach (var img in images.ToList())
        {
            try
            {
                hp.DeletePhoto(img, Path.Combine("images", appointment.Service != null ? "Service_Images" : "Package_Images"));
            }
            catch
            {
                // swallow; don't prevent deletion due to file IO issues
            }
        }

        db.Bookings.Remove(appointment);
        db.SaveChanges();
        TempData["Info"] = "Appointment deleted.";
    }

    var referer = Request.Headers["Referer"].ToString();
    if (!string.IsNullOrWhiteSpace(referer))
    {
        return Redirect(referer);
    }

    return RedirectToAction(nameof(AppointmentMaintenance));
}

[HttpGet]
public JsonResult GetPetsForUserAndType(string email, string petType)
{
    IQueryable<Pets> query = db.Pets.Where(p => p.Email == email);

    if (!string.IsNullOrEmpty(petType) && petType != "All")
    {
        query = query.Where(p => p.PetType == petType);
    }
    else if (petType == "All")
    {
        query = query.Where(p => p.PetType == "Cat" || p.PetType == "Dog");
    }

    var pets = query
        .Select(p => new { value = p.PetId, text = p.Name })
        .ToList();

    return Json(pets);
}
}