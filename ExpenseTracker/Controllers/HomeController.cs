using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ExpenseTracker.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> User()
        {
            User user = await _context.GetOrCreateDefaultUserAsync();
            ViewData["isUserPage"] = true;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> User(User updatedUser, IFormFile? profileImage)
        {
            User currentUser = await _context.GetOrCreateDefaultUserAsync();

            ModelState.Remove("ProfileImage");
            ModelState.Remove("Password");
            ModelState.Remove("NewPassword");
            ModelState.Remove("NicknameLogin");
            ModelState.Remove("PasswordLogin");

            if (ModelState.IsValid)
            {
                if (profileImage != null && profileImage.Length > 0)
                {
                    using var memoryStream = new MemoryStream();
                    await profileImage.CopyToAsync(memoryStream);
                    currentUser.ProfileImage = memoryStream.ToArray();
                }

                currentUser.FirstName = updatedUser.FirstName;
                currentUser.LastName = updatedUser.LastName;
                currentUser.Nickname = updatedUser.Nickname;
                currentUser.WalletBalance = updatedUser.WalletBalance;
                currentUser.MonthlyBudgetLimit = updatedUser.MonthlyBudgetLimit;

                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("UserImage", currentUser.DisplayImage);

                TempData["SuccessMessage"] = "Zapisano ustawienia profilu.";
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["isUserPage"] = true;
            return View(currentUser);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}