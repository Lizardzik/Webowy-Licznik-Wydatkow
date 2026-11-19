using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PinSecurityService _pinSecurity;

        public AuthController(ApplicationDbContext context, PinSecurityService pinSecurity)
        {
            _context = context;
            _pinSecurity = pinSecurity;
        }

        [HttpGet]
        public async Task<IActionResult> SetupPin()
        {
            User user = await _context.GetOrCreateDefaultUserAsync();
            if (!string.IsNullOrWhiteSpace(user.PinHash))
            {
                return RedirectToAction(nameof(Unlock));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupPin(string newPin, string confirmPin)
        {
            User user = await _context.GetOrCreateDefaultUserAsync();
            if (!string.IsNullOrWhiteSpace(user.PinHash))
            {
                return RedirectToAction(nameof(Unlock));
            }

            if (!_pinSecurity.IsValidPinFormat(newPin))
            {
                ViewBag.Error = "Kod PIN musi składać się z 4 do 6 cyfr.";
                return View();
            }

            if (!string.Equals(newPin, confirmPin, StringComparison.Ordinal))
            {
                ViewBag.Error = "Podane kody PIN nie są identyczne.";
                return View();
            }

            user.PinHash = _pinSecurity.HashPin(newPin);
            await _context.SaveChangesAsync();

            _pinSecurity.SetAuthCookie(HttpContext, user);
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> Unlock()
        {
            User user = await _context.GetOrCreateDefaultUserAsync();
            if (string.IsNullOrWhiteSpace(user.PinHash))
            {
                return RedirectToAction(nameof(SetupPin));
            }

            string? cookieToken = Request.Cookies[PinSecurityService.AuthCookieName];
            if (string.Equals(cookieToken, _pinSecurity.GenerateSessionToken(user), StringComparison.Ordinal))
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string pin)
        {
            User user = await _context.GetOrCreateDefaultUserAsync();
            if (string.IsNullOrWhiteSpace(user.PinHash))
            {
                return RedirectToAction(nameof(SetupPin));
            }

            if (!_pinSecurity.VerifyPin(pin, user.PinHash))
            {
                ViewBag.Error = "Nieprawidłowy kod PIN.";
                return View();
            }

            _pinSecurity.SetAuthCookie(HttpContext, user);
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePin(string currentPin, string newPin, string confirmPin)
        {
            User user = await _context.GetOrCreateDefaultUserAsync();

            if (!string.IsNullOrWhiteSpace(user.PinHash) && !_pinSecurity.VerifyPin(currentPin, user.PinHash))
            {
                TempData["PinErrorMessage"] = "Obecny kod PIN jest nieprawidłowy.";
                return RedirectToAction("User", "Home");
            }

            if (!_pinSecurity.IsValidPinFormat(newPin))
            {
                TempData["PinErrorMessage"] = "Nowy kod PIN musi składać się z 4 do 6 cyfr.";
                return RedirectToAction("User", "Home");
            }

            if (!string.Equals(newPin, confirmPin, StringComparison.Ordinal))
            {
                TempData["PinErrorMessage"] = "Nowy kod PIN i jego potwierdzenie nie są identyczne.";
                return RedirectToAction("User", "Home");
            }

            user.PinHash = _pinSecurity.HashPin(newPin);
            await _context.SaveChangesAsync();

            _pinSecurity.SetAuthCookie(HttpContext, user);
            TempData["PinSuccessMessage"] = "Kod PIN został pomyślnie zmieniony.";
            return RedirectToAction("User", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Lock()
        {
            _pinSecurity.ClearAuthCookie(HttpContext);
            return RedirectToAction(nameof(Unlock));
        }
    }
}