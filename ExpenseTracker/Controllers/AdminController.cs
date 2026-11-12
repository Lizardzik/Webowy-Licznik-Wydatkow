using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ExpenseTracker.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TransactionsCount = await _context.Transactions.CountAsync();
            ViewBag.CategoriesCount = await _context.Categories.CountAsync();
            ViewBag.SubscriptionsCount = await _context.Subscriptions.CountAsync();
            ViewBag.NotificationsCount = await _context.Notifications.CountAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreDefaultCategories()
        {
            int addedCount = await EnsureDefaultCategoriesAsync();
            TempData["SuccessMessage"] = addedCount > 0
                ? $"Dodano brakujące domyślne kategorie ({addedCount})."
                : "Wszystkie domyślne kategorie są już dostępne.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ExportDataJson()
        {
            var backupData = new
            {
                ExportedAt = DateTime.Now,
                Categories = await _context.Categories.AsNoTracking().ToListAsync(),
                Transactions = await _context.Transactions.AsNoTracking().ToListAsync(),
                Subscriptions = await _context.Subscriptions.AsNoTracking().ToListAsync()
            };

            string json = JsonSerializer.Serialize(
                backupData,
                new JsonSerializerOptions { WriteIndented = true }
            );

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string fileName = $"expense-tracker-backup-{DateTime.Now:yyyyMMdd-HHmm}.json";

            return File(bytes, "application/json", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearNotifications()
        {
            var notifications = await _context.Notifications.ToListAsync();
            if (notifications.Count > 0)
            {
                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Wszystkie powiadomienia zostały usunięte.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearSubscriptions()
        {
            var subscriptions = await _context.Subscriptions.ToListAsync();
            if (subscriptions.Count > 0)
            {
                _context.Subscriptions.RemoveRange(subscriptions);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Lista subskrypcji została wyczyszczona.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearTransactions()
        {
            var transactions = await _context.Transactions.ToListAsync();
            if (transactions.Count > 0)
            {
                _context.Transactions.RemoveRange(transactions);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Historia transakcji została wyczyszczona.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAllData()
        {
            _context.Transactions.RemoveRange(await _context.Transactions.ToListAsync());
            _context.Subscriptions.RemoveRange(await _context.Subscriptions.ToListAsync());
            _context.Notifications.RemoveRange(await _context.Notifications.ToListAsync());

            var currentUser = await _context.User.FirstOrDefaultAsync();
            if (currentUser != null)
            {
                currentUser.WalletBalance = 0m;
                currentUser.MonthlyBudgetLimit = 0m;
            }

            await _context.SaveChangesAsync();
            await EnsureDefaultCategoriesAsync();

            TempData["SuccessMessage"] = "Wszystkie dane zostały wyzerowane, a domyślne kategorie przywrócone.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<int> EnsureDefaultCategoriesAsync()
        {
            var defaultCategories = new List<Category>
            {
                new Category { Title = "Wypłata", Icon = "fa-solid fa-money-bill-wave", Type = "Income" },
                new Category { Title = "Projekty / Freelance", Icon = "fa-solid fa-laptop-code", Type = "Income" },
                new Category { Title = "Inwestycje / Odsetki", Icon = "fa-solid fa-chart-line", Type = "Income" },
                new Category { Title = "Zwroty i prezenty", Icon = "fa-solid fa-gift", Type = "Income" },
                new Category { Title = "Zakupy spożywcze", Icon = "fa-solid fa-cart-shopping", Type = "Expense" },
                new Category { Title = "Mieszkanie i rachunki", Icon = "fa-solid fa-house", Type = "Expense" },
                new Category { Title = "Transport i paliwo", Icon = "fa-solid fa-car", Type = "Expense" },
                new Category { Title = "Rozrywka", Icon = "fa-solid fa-film", Type = "Expense" },
                new Category { Title = "Zdrowie", Icon = "fa-solid fa-heart-pulse", Type = "Expense" },
                new Category { Title = "Inne wydatki", Icon = "fa-solid fa-box-open", Type = "Expense" }
            };

            var existingTitles = await _context.Categories
                .Select(c => c.Title.ToLower())
                .ToListAsync();

            var missingCategories = defaultCategories
                .Where(c => !existingTitles.Contains(c.Title.ToLower()))
                .ToList();

            if (missingCategories.Count > 0)
            {
                await _context.Categories.AddRangeAsync(missingCategories);
                await _context.SaveChangesAsync();
            }

            return missingCategories.Count;
        }
    }
}