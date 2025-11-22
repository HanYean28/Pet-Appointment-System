using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PawfectGrooming.Models;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PawfectGrooming.Controllers
{
    public class PaymentController : Controller
    {
        private readonly UserContext db;
        private readonly IStringLocalizer<Resources.Resources> Localizer;
        private readonly StripeSettings _stripeSettings;
        private readonly Helper hp;

        public PaymentController(UserContext context, IStringLocalizer<Resources.Resources> localizer, IOptions<StripeSettings> stripeSettings, Helper helper)
        {
            db = context;
            Localizer = localizer;
            _stripeSettings = stripeSettings.Value;
            hp = helper;
        }

        // Render the main "Payment" view
        [HttpGet]
        public IActionResult Create(int? bookingId, string? paymentType)
        {
            if (bookingId == null)
                return NotFound();

            var booking = db.Bookings
                            .Include(b => b.Package)
                            .Include(b => b.Service)
                            .FirstOrDefault(b => b.Id == bookingId.Value);

            if (booking == null)
                return NotFound();

            // Package/Service name
            ViewBag.PackageName = booking.Service?.Name ?? booking.Package?.Name;

            // Price (service or package)
            ViewBag.Price = booking.Service?.Price ?? booking.Package?.Price ?? 0m;

            // Appointment time
            try
            {
                var time = DateTime.ParseExact(booking.Time, "h:mm tt", CultureInfo.InvariantCulture).TimeOfDay;
                ViewBag.AppointmentTime = booking.Date.Add(time);
            }
            catch (FormatException)
            {
                ViewBag.AppointmentTime = booking.Date;
            }

            var model = new Payment
            {
                BookingId = bookingId.Value,
                PaymentType = paymentType
            };

            return View("Payment", model);
        }

        // Create a payment record (non-Stripe)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Payment payment)
        {
            var booking = db.Bookings
                .Include(b => b.Service)
                .Include(b => b.Package)
                .FirstOrDefault(b => b.Id == payment.BookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Invalid booking.";
                return RedirectToAction("History");
            }

            // ✅ Always recalc amount server-side
            decimal baseAmt = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
            var appliedNow = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
            decimal discountNow = appliedNow.VoucherId.HasValue ? appliedNow.Discount : 0m;

            if (string.Equals(payment.PaymentType, "Deposit", StringComparison.OrdinalIgnoreCase))
            {
                payment.Amount = 20; // RM20 deposit
            }
            else
            {
                payment.Amount = (int)Math.Max(0, baseAmt - discountNow);
            }

            payment.Date = DateTime.Now;
            // Set status based on payment type
            payment.Status = payment.PaymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed";

            // ✅ Clean up irrelevant fields by payment method
            switch (payment.PaymentMethod)
            {
                case "Credit Card":
                    break; // keep card details
                case "FPX":
                    payment.CardNumber = null;
                    payment.ExpiryDate = null;
                    payment.CVV = null;
                    break;
                case "E-Wallet":
                    payment.CardNumber = null;
                    payment.ExpiryDate = null;
                    payment.CVV = null;
                    payment.Bank = null;
                    break;
                case "Stripe":
                    payment.CardNumber = null;
                    payment.ExpiryDate = null;
                    payment.CVV = null;
                    payment.Bank = null;
                    payment.WalletProvider = null;
                    payment.WalletId = null;
                    break;
            }

            if (ModelState.IsValid)
            {
                db.Payment.Add(payment);
                db.SaveChanges();

                // ✅ Link payment to booking
                booking.Payment = payment;
                booking.Status = payment.PaymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed";
                db.SaveChanges();

                // ✅ Voucher & Points
                var charged = payment.PaymentType?.ToLower().Contains("deposit") == true
                    ? 20.00m
                    : Math.Max(0, baseAmt - discountNow);

                hp.ConsumeVoucherAndAwardPoints(booking.Id, booking.UserEmail, payment.PaymentType, charged);

                return RedirectToAction("Confirmation", new { id = payment.PaymentId });
            }

            return View("Payment", payment);
        }


        // Simulated credit card processing (non-Stripe)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessCreditCard(int bookingId, string paymentType, string cardNumber, string expiryDate, string cvv)
        {
            // Calculate amount
            var booking = db.Bookings
    .Include(b => b.Service)
    .Include(b => b.Package)
    .FirstOrDefault(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            var baseAmt = booking.Service?.Price ?? booking.Package?.Price ?? 0m;

            // Only apply voucher on full payments
            decimal discount = 0m;
            if (!string.IsNullOrWhiteSpace(paymentType) && !paymentType.ToLower().Contains("deposit"))
            {
                var applied = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
                if (applied.VoucherId.HasValue)
                    discount = applied.Discount;

                if (discount < 0) discount = 0;
                if (discount > baseAmt) discount = baseAmt;
            }

            decimal charged = (!string.IsNullOrWhiteSpace(paymentType) && paymentType.ToLower().Contains("deposit"))
                ? 20.00m
                : Math.Max(0, baseAmt - discount);

            // Create payment record
            var payment = new Payment
            {
                BookingId = bookingId,
                PaymentMethod = "Credit Card",
                PaymentType = paymentType,
                Status = paymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed",
                Date = DateTime.Now,
                CardNumber = cardNumber,
                ExpiryDate = expiryDate,
                CVV = cvv,
                Amount = (int)charged // <-- voucher applied here
            };


            db.Payment.Add(payment);
            db.SaveChanges();

            // Update booking
            booking.Payment = payment;
            booking.Status = "Completed";
            db.SaveChanges();

            // Consume voucher and award points (voucher only on full payment)
            hp.ConsumeVoucherAndAwardPoints(booking.Id, booking.UserEmail, paymentType, charged);

            return RedirectToAction("Confirmation", new { id = payment.PaymentId });
        }


        [HttpGet]
        public IActionResult CreditCard(int bookingId, string paymentType)
        {
            var booking = db.Bookings
                .Include(b => b.Service)
                .Include(b => b.Package)
                .FirstOrDefault(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            // NEW: compute and pass amounts
            var (baseAmt, discount, payable) = ComputePayableNow(bookingId, paymentType);
            ViewBag.BaseAmount = baseAmt;
            ViewBag.Discount = discount;
            ViewBag.PayableNow = payable;

            return View(new Payment
            {
                BookingId = bookingId,
                PaymentMethod = "Credit Card",
                PaymentType = paymentType,
                Booking = booking
            });
        }

        [HttpGet]
        public IActionResult FPX(int bookingId, string paymentType)
        {
            var booking = db.Bookings
                .Include(b => b.Service)
                .Include(b => b.Package)
                .FirstOrDefault(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            // NEW: compute and pass amounts
            var (baseAmt, discount, payable) = ComputePayableNow(bookingId, paymentType);
            ViewBag.BaseAmount = baseAmt;
            ViewBag.Discount = discount;
            ViewBag.PayableNow = payable;

            ViewBag.PublishableKey = _stripeSettings.PublishableKey;

            return View(new Payment
            {
                BookingId = bookingId,
                PaymentMethod = "FPX",
                PaymentType = paymentType,
                Booking = booking
            });
        }

        [HttpGet]
        public IActionResult EWallet(int bookingId, string paymentType)
        {
            var booking = db.Bookings
                .Include(b => b.Service)
                .Include(b => b.Package)
                .FirstOrDefault(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            // NEW: compute and pass amounts
            var (baseAmt, discount, payable) = ComputePayableNow(bookingId, paymentType);
            ViewBag.BaseAmount = baseAmt;
            ViewBag.Discount = discount;
            ViewBag.PayableNow = payable;

            return View(new Payment
            {
                BookingId = bookingId,
                PaymentMethod = "E-Wallet",
                PaymentType = paymentType,
                Booking = booking
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessEWalletPayment([FromBody] EWalletPaymentRequest request)
        {
            try
            {
                // Validate input
                if (request.BookingId <= 0) return Json(new { error = "Invalid booking ID" });
                if (string.IsNullOrEmpty(request.WalletProvider)) return Json(new { error = "Please select an e-wallet provider" });
                if (string.IsNullOrEmpty(request.WalletId) || request.WalletId.Length < 8) return Json(new { error = "Please enter a valid e-wallet ID or phone number" });
                if (string.IsNullOrEmpty(request.Email)) return Json(new { error = "Please enter your email address" });
                if (string.IsNullOrEmpty(request.Name) || request.Name.Length < 2) return Json(new { error = "Please enter your full name" });

                // Check booking
                var booking = db.Bookings
                    .Include(b => b.Service)
                    .Include(b => b.Package)
                    .FirstOrDefault(b => b.Id == request.BookingId);
                if (booking == null) return Json(new { error = "Booking not found" });

                // Calculate amount (voucher only on full payment)
                decimal amount;
                if (request.PaymentType?.ToLower().Contains("deposit") == true)
                {
                    amount = 20.00m;
                }
                else
                {
                    var baseAmt = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
                    var applied = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
                    var discount = applied.VoucherId.HasValue ? applied.Discount : 0m;
                    amount = Math.Max(0, baseAmt - discount);
                }

                // Simulate e-wallet payment
                await Task.Delay(2000);

                // Create payment record
                var payment = new Payment
                {
                    BookingId = request.BookingId,
                    PaymentMethod = "E-Wallet",
                    PaymentType = request.PaymentType,
                    Status = request.PaymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed",
                    Date = DateTime.Now,
                    WalletProvider = request.WalletProvider,
                    WalletId = request.WalletId,
                    Amount = (int)amount
                };

                db.Payment.Add(payment);
                db.SaveChanges();

                // Update booking status
                booking.Payment = payment;
                booking.Status = request.PaymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed";
                db.SaveChanges();

                // Consume voucher + award points (Helper will skip deposit)
                var baseAmtForConsume = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
                var appliedNow = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
                var discountNow = appliedNow.VoucherId.HasValue ? appliedNow.Discount : 0m;
                var charged = request.PaymentType?.ToLower().Contains("deposit") == true
                    ? 20.00m
                    : Math.Max(0, baseAmtForConsume - discountNow);
                hp.ConsumeVoucherAndAwardPoints(booking.Id, booking.UserEmail, request.PaymentType, charged);

                return Json(new
                {
                    success = true,
                    paymentId = payment.PaymentId,
                    message = $"E-wallet payment of RM {amount:F2} completed successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = $"E-wallet payment failed: {ex.Message}" });
            }
        }

        // Route selection based on chosen method
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SelectMethod(Payment payment)
        {
            if (ModelState.IsValid)
            {
                switch (payment.PaymentMethod)
                {
                    case "Credit Card":
                        return RedirectToAction("CreditCard", new { bookingId = payment.BookingId, paymentType = payment.PaymentType });
                    case "FPX":
                        return RedirectToAction("FPX", new { bookingId = payment.BookingId, paymentType = payment.PaymentType });
                    case "E-Wallet":
                        return RedirectToAction("EWallet", new { bookingId = payment.BookingId, paymentType = payment.PaymentType });
                    case "Stripe":
                        return RedirectToAction("Stripe", new { bookingId = payment.BookingId, paymentType = payment.PaymentType });
                    default:
                        return RedirectToAction("Create", new { bookingId = payment.BookingId, paymentType = payment.PaymentType });
                }
            }

            return View("Payment", payment);
        }

        [HttpGet]
        public IActionResult Confirmation(int id)
        {
            try
            {
                var payment = db.Payment
                    .Include(p => p.Booking)
                    .FirstOrDefault(p => p.PaymentId == id);

                if (payment == null)
                {
                    TempData["ErrorMessage"] = $"Payment with ID {id} not found.";
                    return RedirectToAction("History");
                }

                return View("Confirmation", payment);
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred while loading the payment confirmation.";
                return RedirectToAction("History");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Member")]
        public IActionResult History()
        {
            string userEmail = User.Identity.Name;
            var payments = db.Payment
                .Include(p => p.Booking)
                .Where(p => p.Booking.UserEmail == userEmail)
                .OrderByDescending(p => p.Date)
                .ToList();

            return View("History", payments);
        }

       

        // Stripe FPX session (Checkout)
        [HttpPost]
        public IActionResult CreateFpxSession(int bookingId, string paymentType)
        {   
            var booking = db.Bookings
                .Include(b => b.Service)
                .Include(b => b.Package)
                .FirstOrDefault(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            decimal amountToCharge;
            if (!string.IsNullOrWhiteSpace(paymentType) && paymentType.ToLower().Contains("deposit"))
            {
                amountToCharge = 20.00m;
            }
            else
            {
                var baseAmountFpx = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
                var appliedFpx = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
                var discountFpx = appliedFpx.VoucherId.HasValue ? appliedFpx.Discount : 0m;
                amountToCharge = Math.Max(0, baseAmountFpx - discountFpx);
            }

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "fpx" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(amountToCharge * 100),
                            Currency = "myr",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = booking.Service?.Name ?? booking.Package?.Name ?? "Pawfect Grooming"
                            },
                        },
                        Quantity = 1,
                    }
                },
                Mode = "payment",
                SuccessUrl = Url.Action("StripeSuccess", "Payment", new { session_id = "{CHECKOUT_SESSION_ID}" }, Request.Scheme),
                CancelUrl = Url.Action("StripeCancel", "Payment", null, Request.Scheme),
            };

            var service = new SessionService();
            var session = service.Create(options);

            var payment = new Payment
            {
                BookingId = booking.Id,
                PaymentMethod = "FPX",
                PaymentType = paymentType ?? "Full Payment",
                Status = "Pending",
                Date = DateTime.Now,
                StripeSessionId = session.Id,
                StripePaymentIntentId = session.PaymentIntentId,
                Amount = amountToCharge


            };

            db.Payment.Add(payment);
            db.SaveChanges();

            return Json(new { id = session.Id });
        }

        // Stripe Card Payment Methods
        [HttpGet]
        public IActionResult Stripe(int bookingId, string paymentType)
        {
            ViewBag.PublishableKey = _stripeSettings.PublishableKey;
            return View(new Payment { BookingId = bookingId, PaymentMethod = "Stripe", PaymentType = paymentType });
        }

        [HttpPost]
        public async Task<IActionResult> CreatePaymentIntent(int bookingId, string paymentType)
        {
            try
            {
                var booking = db.Bookings
                    .Include(b => b.Service)
                    .Include(b => b.Package)
                    .FirstOrDefault(b => b.Id == bookingId);

                if (booking == null)
                    return Json(new { error = "Booking not found" });

                // Amount for Stripe card: voucher only on full payment
                decimal amount;
                if (!string.IsNullOrWhiteSpace(paymentType) && paymentType.ToLower().Contains("deposit"))
                {
                    amount = 20.00m;
                }
                else
                {
                    var baseAmount = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
                    var applied = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
                    var discount = applied.VoucherId.HasValue ? applied.Discount : 0m;
                    amount = Math.Max(0, baseAmount - discount);
                }

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100), // MYR -> sen
                    Currency = "myr",
                    PaymentMethodTypes = new List<string> { "card" },
                    Metadata = new Dictionary<string, string>
                    {
                        { "bookingId", bookingId.ToString() },
                        { "paymentType", paymentType }
                    }
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                return Json(new { clientSecret = paymentIntent.ClientSecret });
            }
            catch (StripeException ex)
            {
                return Json(new { error = ex.Message });
            }
            catch
            {
                return Json(new { error = "An error occurred while creating payment intent" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(string paymentIntentId, int bookingId, string paymentType)
        {
            try
            {
                if (string.IsNullOrEmpty(paymentIntentId))
                    return Json(new { error = "Payment intent ID is required" });

                if (bookingId <= 0)
                    return Json(new { error = "Invalid booking ID" });

                // Fallback path - only for full payments, not deposits
                if (paymentIntentId.StartsWith("pi_fallback_"))
                {
                    // Skip FPX fallback for deposits - they should use other payment methods
                    if (!string.IsNullOrWhiteSpace(paymentType) && paymentType.ToLower().Contains("deposit"))
                    {
                        return Json(new { error = "FPX fallback not available for deposits. Please use another payment method." });
                    }

                    // Get booking to calculate amount
                    var booking = db.Bookings
                        .Include(b => b.Service)
                        .Include(b => b.Package)
                        .FirstOrDefault(b => b.Id == bookingId);
                    
                    if (booking == null)
                    {
                        return Json(new { error = "Booking not found" });
                    }

                    // Calculate amount for full payment (with voucher if applied)
                    var baseAmt = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
                    var applied = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
                    var discount = applied.VoucherId.HasValue ? applied.Discount : 0m;
                    var amountToCharge = Math.Max(0, baseAmt - discount);

                    var payment = new Payment
                    {
                        BookingId = bookingId,
                        PaymentMethod = "FPX (Fallback)",
                        PaymentType = paymentType,
                        Status = "Completed",
                        Date = DateTime.Now,
                        StripePaymentIntentId = paymentIntentId,
                        Amount = amountToCharge
                    };

                    db.Payment.Add(payment);
                    db.SaveChanges();

                    // Update booking status
                    booking.Payment = payment;
                    booking.Status = "Completed";
                    db.SaveChanges();

                    // Consume voucher and award points
                    hp.ConsumeVoucherAndAwardPoints(booking.Id, booking.UserEmail, paymentType, amountToCharge);

                    return Json(new { success = true, paymentId = payment.PaymentId });
                }

                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);

                if (paymentIntent.Status == "succeeded")
                {
                    var existingPayment = db.Payment.FirstOrDefault(p => p.StripePaymentIntentId == paymentIntent.Id);
                    if (existingPayment != null)
                    {
                        var bookedExisting = db.Bookings.FirstOrDefault(b => b.Id == bookingId);
                        if (bookedExisting != null)
                        {
                            var chargedFromStripeExisting = (decimal)paymentIntent.Amount / 100m;
                            hp.ConsumeVoucherAndAwardPoints(bookedExisting.Id, bookedExisting.UserEmail, paymentType, chargedFromStripeExisting);
                        }
                        return Json(new { success = true, paymentId = existingPayment.PaymentId });
                    }

                    var payment = new Payment
                    {
                        BookingId = bookingId,
                        PaymentMethod = "Stripe",
                        PaymentType = paymentType,
                        Status = paymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed",
                        Date = DateTime.Now,
                        StripePaymentIntentId = paymentIntent.Id
                    };

                    db.Payment.Add(payment);
                    db.SaveChanges();

                    var booking = db.Bookings.FirstOrDefault(b => b.Id == bookingId);
                    if (booking != null)
                    {
                        booking.Payment = payment;
                        booking.Status = paymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed";
                        db.SaveChanges();
                    }

                    // Consume voucher and award points using actual charged amount
                    var chargedFromStripe = (decimal)paymentIntent.Amount / 100m;
                    var booked = db.Bookings.FirstOrDefault(b => b.Id == bookingId);
                    if (booked != null)
                    {
                        hp.ConsumeVoucherAndAwardPoints(booked.Id, booked.UserEmail, paymentType, chargedFromStripe);
                    }

                    return Json(new { success = true, paymentId = payment.PaymentId, message = "Payment confirmed successfully" });
                }
                else
                {
                    return Json(new { error = $"Payment was not successful. Status: {paymentIntent.Status}" });
                }
            }
            catch (StripeException ex)
            {
                return Json(new { error = $"Stripe error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return Json(new { error = $"An error occurred while confirming payment: {ex.Message}" });
            }
        }

        // Stripe Webhook Handler
        [HttpPost]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _stripeSettings.WebhookSecret
                );

                switch (stripeEvent.Type)
                {
                    case "payment_intent.succeeded":
                        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentIntentSucceeded(paymentIntent);
                        break;
                    case "payment_intent.payment_failed":
                        var failedPaymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentIntentFailed(failedPaymentIntent);
                        break;
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                return BadRequest($"Stripe error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        private async Task HandlePaymentIntentSucceeded(PaymentIntent paymentIntent)
        {
            var bookingId = paymentIntent.Metadata.GetValueOrDefault("bookingId");
            var paymentType = paymentIntent.Metadata.GetValueOrDefault("paymentType");

            if (!string.IsNullOrEmpty(bookingId) && int.TryParse(bookingId, out int bookingIdInt))
            {
                var existingPayment = db.Payment.FirstOrDefault(p => p.StripePaymentIntentId == paymentIntent.Id);
                if (existingPayment == null)
                {
                    var payment = new Payment
                    {
                        BookingId = bookingIdInt,
                        PaymentMethod = "Stripe",
                        PaymentType = paymentType ?? "Full Payment",
                        Status = (paymentType ?? "Full Payment").ToLower().Contains("deposit") ? "Deposit" : "Completed",
                        Date = DateTime.Now,
                        StripePaymentIntentId = paymentIntent.Id
                    };

                    db.Payment.Add(payment);
                    db.SaveChanges();

                    var booking = db.Bookings.FirstOrDefault(b => b.Id == bookingIdInt);
                    if (booking != null)
                    {
                        booking.Payment = payment;
                        booking.Status = (paymentType ?? "Full Payment").ToLower().Contains("deposit") ? "Deposit" : "Completed";
                        db.SaveChanges();
                    }
                }

                // Idempotent voucher consumption and points award (use actual charged amount)
                var charged = (decimal)paymentIntent.Amount / 100m;
                var bk = db.Bookings.FirstOrDefault(b => b.Id == bookingIdInt);
                if (bk != null)
                {
                    hp.ConsumeVoucherAndAwardPoints(bk.Id, bk.UserEmail, paymentType, charged);
                }
            }
        }

        [HttpGet]
        public IActionResult StripeSuccess(string session_id)
        {
            var service = new SessionService();
            var session = service.Get(session_id);

            var payment = db.Payment.FirstOrDefault(p => p.StripeSessionId == session.Id);
            if (payment != null)
            {
                payment.Status = payment.PaymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed";
                db.SaveChanges();

                // Consume voucher and award points (FPX success) - voucher for full payment only
                var booking = db.Bookings
                    .Include(b => b.Service)
                    .Include(b => b.Package)
                    .FirstOrDefault(b => b.Id == payment.BookingId);

                if (booking != null)
                {
                    booking.Status = payment.PaymentType?.ToLower().Contains("deposit") == true ? "Deposit" : "Completed";
                    db.SaveChanges();

                    var isDeposit = !string.IsNullOrWhiteSpace(payment.PaymentType) && payment.PaymentType.ToLower().Contains("deposit");
                    decimal charged;
                    if (isDeposit)
                    {
                        charged = 20.00m;
                    }
                    else
                    {
                        var baseAmt2 = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
                        var applied2 = hp.GetAppliedVoucherFor(booking.Id, booking.UserEmail);
                        var discount2 = applied2.VoucherId.HasValue ? applied2.Discount : 0m;
                        charged = Math.Max(0, baseAmt2 - discount2);
                    }

                    hp.ConsumeVoucherAndAwardPoints(booking.Id, booking.UserEmail, payment.PaymentType, charged);
                }

                return RedirectToAction("Confirmation", new { id = payment.PaymentId });
            }

            return RedirectToAction("History");
        }

        [HttpGet]
        public IActionResult StripeCancel()
        {
            return RedirectToAction("History");
        }

        

        [HttpGet]
        public IActionResult Debug(int bookingId, string paymentType)
        {
            return View(new Payment { BookingId = bookingId, PaymentType = paymentType });
        }

        [HttpGet]
        public IActionResult DebugPayment(int paymentId)
        {
            var payment = db.Payment
                .Include(p => p.Booking)
                .FirstOrDefault(p => p.PaymentId == paymentId);

            if (payment == null)
            {
                return Json(new { error = "Payment not found", paymentId = paymentId });
            }

            return Json(new
            {
                success = true,
                payment = new
                {
                    paymentId = payment.PaymentId,
                    paymentMethod = payment.PaymentMethod,
                    paymentType = payment.PaymentType,
                    status = payment.Status,
                    amount = payment.Amount,
                    bookingId = payment.BookingId,
                    stripePaymentIntentId = payment.StripePaymentIntentId,
                    date = payment.Date
                }
            });
        }

        [HttpGet]
        public IActionResult ListRecentPayments(int count = 10)
        {
            var payments = db.Payment
                .Include(p => p.Booking)
                .OrderByDescending(p => p.Date)
                .Take(count)
                .Select(p => new
                {
                    paymentId = p.PaymentId,
                    paymentMethod = p.PaymentMethod,
                    paymentType = p.PaymentType,
                    status = p.Status,
                    amount = p.Amount,
                    bookingId = p.BookingId,
                    date = p.Date
                })
                .ToList();

            return Json(new { success = true, payments = payments });
        }

        private async Task HandlePaymentIntentFailed(PaymentIntent paymentIntent)
        {
            var bookingId = paymentIntent.Metadata.GetValueOrDefault("bookingId");

            if (!string.IsNullOrEmpty(bookingId) && int.TryParse(bookingId, out int bookingIdInt))
            {
                var payment = new Payment
                {
                    BookingId = bookingIdInt,
                    PaymentMethod = "Stripe",
                    PaymentType = paymentIntent.Metadata.GetValueOrDefault("paymentType") ?? "Full Payment",
                    Status = "Failed",
                    Date = DateTime.Now,
                    StripePaymentIntentId = paymentIntent.Id
                };

                db.Payment.Add(payment);
                db.SaveChanges();
            }
        }

        // Voucher application (Full Payment only), redirects back to Create with bookingId + paymentType
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult ApplyVoucher(int bookingId, string code, string? returnUrl, string? paymentType)
        {
            var email = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Please sign in.";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }

            // Require "Full Payment"
            if (!string.Equals(paymentType, "Full Payment", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Vouchers only applied to Full Payment.";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }

            // Validate code presence and format (AA… 6-32, digits and hyphen allowed)
            var normalized = code?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                TempData["Error"] = "Enter a voucher code.";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }
            if (!Regex.IsMatch(normalized, @"^[A-Z0-9\-]{6,32}$"))
            {
                TempData["Error"] = "Invalid voucher format.";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }

            // Validate booking ownership and payable status
            var booking = db.Bookings.FirstOrDefault(b =>
                b.Id == bookingId &&
                b.UserEmail == email &&
                b.IsActive &&
                (b.Status == "PendingPayment" || b.Status == "Pending" || b.Status == null));

            if (booking is null)
            {
                TempData["Error"] = "Booking not found";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }

            // If a voucher is already applied in this session, require remove first
            var existingVoucherId = HttpContext.Session.GetInt32("VoucherId");
            var existingVoucherBookingId = HttpContext.Session.GetInt32("VoucherBookingId");
            if (existingVoucherId.HasValue)
            {
                if (existingVoucherBookingId.HasValue && existingVoucherBookingId.Value != bookingId)
                {
                    TempData["Error"] = "Voucher already used";
                    return RedirectToAction("Create", new { bookingId, paymentType });
                }
                TempData["Error"] = "A voucher is already applied.";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }

            var now = DateTime.UtcNow;

            var voucher = db.Voucher
                .Where(v => v.Code.ToUpper() == normalized)
                .Where(v => v.Email == email) // ownership
                .Where(v => !v.IsUsed)
                .Where(v => v.ExpiryDate >= now)
                .FirstOrDefault();

            if (voucher is null)
            {
                TempData["Error"] = "Invalid Voucher.";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }

            // Discount sanity check
            if (voucher.DiscountAmount <= 0)
            {
                TempData["Error"] = "This voucher has no discount amount.";
                return RedirectToAction("Create", new { bookingId, paymentType });
            }

            // Bind voucher to this booking via Session
            HttpContext.Session.SetInt32("VoucherId", voucher.Id);
            HttpContext.Session.SetString("VoucherCode", voucher.Code);
            HttpContext.Session.SetString("VoucherAmount", voucher.DiscountAmount.ToString(CultureInfo.InvariantCulture));
            HttpContext.Session.SetInt32("VoucherBookingId", bookingId);

            TempData["Info"] = $"Voucher applied!";
            return RedirectToAction("Create", new { bookingId, paymentType });
        }

        // Voucher removal, redirects back to Create with bookingId + paymentType
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveVoucher(string? returnUrl, int? bookingId, string? paymentType)
        {
            // Optional: ensure we only remove if it pertains to the same booking
            var existingVoucherBookingId = HttpContext.Session.GetInt32("VoucherBookingId");
            if (existingVoucherBookingId.HasValue && bookingId.HasValue && existingVoucherBookingId.Value != bookingId.Value)
            {
                TempData["Error"] = "No voucher applied for this booking.";
                var targetId = bookingId ?? existingVoucherBookingId;
                return RedirectToAction("Create", new { bookingId = targetId, paymentType });
            }

            // Clear only voucher-related session keys
            HttpContext.Session.Remove("VoucherId");
            HttpContext.Session.Remove("VoucherCode");
            HttpContext.Session.Remove("VoucherAmount");
            HttpContext.Session.Remove("VoucherBookingId");

            TempData["Info"] = "Voucher removed.";

            var finalId = bookingId ?? existingVoucherBookingId;
            return finalId.HasValue
                ? RedirectToAction("Create", new { bookingId = finalId.Value, paymentType })
                : RedirectToAction("History");
        }

        // Local helper to safely redirect back or to a fallback
        private IActionResult SafeRedirect(string? url, string fallbackController, string fallbackAction, object? routeValues = null)
        {
            if (!string.IsNullOrEmpty(url) && Url.IsLocalUrl(url))
                return LocalRedirect(url);

            return routeValues is null
                ? RedirectToAction(fallbackAction, fallbackController)
                : RedirectToAction(fallbackAction, fallbackController, routeValues);
        }

        // Compute BaseAmount, Discount (voucher only on Full Payment), and PayableNow
        private (decimal BaseAmount, decimal Discount, decimal PayableNow) ComputePayableNow(int bookingId, string? paymentType)
        {
            var booking = db.Bookings
                .Include(b => b.Service)
                .Include(b => b.Package)
                .FirstOrDefault(b => b.Id == bookingId);

            if (booking == null) return (0m, 0m, 0m);

            var baseAmt = booking.Service?.Price ?? booking.Package?.Price ?? 0m;
            var isDeposit = !string.IsNullOrWhiteSpace(paymentType) &&
                            paymentType.Equals("Deposit", StringComparison.OrdinalIgnoreCase);

            decimal discount = 0m;

            // Voucher applies only to Full Payment and only if the voucher in Session is for this booking
            if (!isDeposit)
            {
                var sessionBookingId = HttpContext.Session.GetInt32("VoucherBookingId");
                var amountStr = HttpContext.Session.GetString("VoucherAmount");

                if (sessionBookingId.HasValue && sessionBookingId.Value == bookingId && !string.IsNullOrWhiteSpace(amountStr))
                {
                    if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out discount))
                        discount = 0m;

                    if (discount < 0) discount = 0;
                    if (discount > baseAmt) discount = baseAmt;
                }
            }

            var payable = isDeposit ? 20.00m : Math.Max(0, baseAmt - discount);
            return (baseAmt, discount, payable);
        }

        [HttpGet]
        public IActionResult GenerateReceipt(int id)
        {
            var payment = db.Payment
                .Include(p => p.Booking).ThenInclude(b => b.User)
                .Include(p => p.Booking).ThenInclude(b => b.Service)
                .Include(p => p.Booking).ThenInclude(b => b.Package)
                .FirstOrDefault(p => p.PaymentId == id);

            if (payment == null) return NotFound();

            var userName = payment.Booking?.User?.Name ?? "N/A";
            var serviceName = payment.Booking?.Service?.Name ?? "N/A";
            var packageName = payment.Booking?.Package?.Name ?? "N/A";

            var baseAmt = payment.Booking?.Service?.Price ?? payment.Booking?.Package?.Price ?? 0m;
            var isDeposit = payment.PaymentType != null &&
                            payment.PaymentType.Equals("Deposit", StringComparison.OrdinalIgnoreCase);

            decimal discount = 0m;
            decimal charged;

            if (payment.PaymentMethod != null && 
                (payment.PaymentMethod.Equals("FPX", StringComparison.OrdinalIgnoreCase) || 
                 payment.PaymentMethod.Equals("FPX (Fallback)", StringComparison.OrdinalIgnoreCase)))
            {
                // FPX and FPX Fallback: use the stored payment amount
                charged = payment.Amount;
                discount = baseAmt - payment.Amount;
                if (discount < 0) discount = 0;
            }
            else
            {
                if (isDeposit)
                {
                    // For deposits: show service price, no voucher discount, deposit amount as total
                    discount = 0m;
                    charged = payment.Amount; // This should be 20.00 for deposits
                }
                else
                {
                    // For full payments: only show voucher discount if there's actually a voucher applied
                    // Check if payment amount is significantly less than base amount (indicating voucher)
                    if (payment.Amount > 0 && payment.Amount < baseAmt && (baseAmt - payment.Amount) >= 1.00m)
                    {
                        discount = baseAmt - payment.Amount;
                        if (discount < 0) discount = 0;
                        if (discount > baseAmt) discount = baseAmt;
                    }
                    else
                    {
                        discount = 0m;
                    }

                    charged = payment.Amount;
                }
            }

            var stream = new MemoryStream();

            var pdfWriter = new PdfWriter(stream);
            var pdf = new PdfDocument(pdfWriter);
            var document = new Document(pdf);

            PdfFont regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            PdfFont bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            document.Add(new Paragraph("PAWFECT GROOMING").SetFontSize(20).SetTextAlignment(TextAlignment.CENTER).SetFontColor(ColorConstants.BLUE).SetFont(bold));
            document.Add(new Paragraph("E-RECEIPT").SetFontSize(16).SetTextAlignment(TextAlignment.CENTER).SetFont(bold));
            document.Add(new LineSeparator(new SolidLine(1)));
            document.Add(new Paragraph(" ").SetFontSize(10));

            Table detailsTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }));
            detailsTable.SetWidth(UnitValue.CreatePercentValue(90));
            detailsTable.SetHorizontalAlignment(HorizontalAlignment.CENTER);
            detailsTable.SetBorder(Border.NO_BORDER);

            detailsTable.AddCell(new Cell().Add(new Paragraph("Booking ID:")).SetFont(regular).SetFontSize(10).SetBorder(null));
            detailsTable.AddCell(new Cell().Add(new Paragraph(payment.BookingId.ToString())).SetTextAlignment(TextAlignment.RIGHT).SetFont(bold).SetFontSize(10).SetBorder(null));

            detailsTable.AddCell(new Cell().Add(new Paragraph("Receipt ID:")).SetFont(regular).SetFontSize(10).SetBorder(null));
            detailsTable.AddCell(new Cell().Add(new Paragraph(payment.PaymentId.ToString())).SetTextAlignment(TextAlignment.RIGHT).SetFont(bold).SetFontSize(10).SetBorder(null));

            detailsTable.AddCell(new Cell().Add(new Paragraph("Payment Date:")).SetFont(regular).SetFontSize(10).SetBorder(null));
            detailsTable.AddCell(new Cell().Add(new Paragraph(payment.Date.ToString("dd MMMM yyyy, hh:mm tt"))).SetTextAlignment(TextAlignment.RIGHT).SetFont(regular).SetFontSize(10).SetBorder(null));

            document.Add(detailsTable);
            document.Add(new Paragraph(" ").SetFontSize(10));
            document.Add(new LineSeparator(new SolidLine(1)));
            document.Add(new Paragraph(" ").SetFontSize(10));

            Table itemsTable = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1 }));
            itemsTable.SetWidth(UnitValue.CreatePercentValue(90));
            itemsTable.SetHorizontalAlignment(HorizontalAlignment.CENTER);
            itemsTable.SetBorder(Border.NO_BORDER);

            string itemDescription = serviceName != "N/A" ? "Service: " + serviceName : "Package: " + packageName;

            // Base row
            itemsTable.AddCell(new Cell().Add(new Paragraph(itemDescription)).SetFont(regular).SetFontSize(12).SetBorder(null));
            itemsTable.AddCell(new Cell().Add(new Paragraph($"RM {baseAmt:F2}")).SetTextAlignment(TextAlignment.RIGHT).SetFont(regular).SetFontSize(12).SetBorder(null));

            // Discount row (only for full payment with discount)
            if (!isDeposit && discount > 0)
            {
                itemsTable.AddCell(new Cell().Add(new Paragraph("Voucher Discount")).SetFont(regular).SetFontSize(12).SetBorder(null));
                itemsTable.AddCell(new Cell().Add(new Paragraph($"- RM {discount:F2}")).SetTextAlignment(TextAlignment.RIGHT).SetFont(regular).SetFontSize(12).SetBorder(null));
            }

            document.Add(itemsTable);

            document.Add(new Paragraph(" ").SetFontSize(10));
            document.Add(new LineSeparator(new SolidLine(1)));
            document.Add(new Paragraph(" ").SetFontSize(10));

            Table summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1 }));
            summaryTable.SetWidth(UnitValue.CreatePercentValue(90));
            summaryTable.SetHorizontalAlignment(HorizontalAlignment.CENTER);
            summaryTable.SetBorder(Border.NO_BORDER);

            summaryTable.AddCell(new Cell().Add(new Paragraph("Payment Method")).SetFont(regular).SetFontSize(12).SetBorder(null));
            summaryTable.AddCell(new Cell().Add(new Paragraph(payment.PaymentMethod)).SetTextAlignment(TextAlignment.RIGHT).SetFont(regular).SetFontSize(12).SetBorder(null));

            if (isDeposit)
            {
                summaryTable.AddCell(new Cell().Add(new Paragraph("Total Deposited")).SetFont(bold).SetFontSize(14).SetBorder(null));
                summaryTable.AddCell(new Cell().Add(new Paragraph("RM 20.00")).SetTextAlignment(TextAlignment.RIGHT).SetFont(bold).SetFontSize(14).SetBorder(null));
            }
            else
            {
                summaryTable.AddCell(new Cell().Add(new Paragraph("Total Payment")).SetFont(bold).SetFontSize(14).SetBorder(null));
                summaryTable.AddCell(new Cell().Add(new Paragraph($"RM {charged:F2}")).SetTextAlignment(TextAlignment.RIGHT).SetFont(bold).SetFontSize(14).SetBorder(null));
            }

            document.Add(summaryTable);
            document.Add(new Paragraph(" ").SetFontSize(10));
            document.Add(new LineSeparator(new SolidLine(1)));
            document.Add(new Paragraph(" ").SetFontSize(10));
            document.Add(new Paragraph("Thank you for your business!").SetFontSize(12).SetTextAlignment(TextAlignment.CENTER).SetFont(regular));
            document.Add(new Paragraph("This is a computer-generated receipt, no signature is required.").SetFontSize(8).SetTextAlignment(TextAlignment.CENTER).SetFont(regular).SetFontColor(ColorConstants.GRAY));

            document.Close();

            var bytes = stream.ToArray();
            var fileName = $"receipt_{payment.PaymentId}.pdf";
            return File(bytes, "application/pdf", fileName);
        }


    }

    public class EWalletPaymentRequest
    {
        public int BookingId { get; set; }
        public string PaymentType { get; set; }
        public string WalletProvider { get; set; }
        public string WalletId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }
}