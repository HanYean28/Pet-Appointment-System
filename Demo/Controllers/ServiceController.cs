using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PawfectGrooming.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PawfectGrooming.Controllers
{
    // Lightweight compatibility controller.
    // All service listing logic lives in HomeController.Services.
    // Requests to /Service/Services are redirected to /Home/Services.
    public class ServiceController : Controller
    {
        [HttpGet]
        public IActionResult Services(string? name, string? sort, string? dir)
        {
            // Redirect permanently to HomeController.Services with query values preserved
            return RedirectToActionPermanent("Services", "Home", new { name, sort, dir });
        }

        // If you had a ServiceCart action here, consider redirecting it as well or remove it.
    }
}