using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public SubscriptionController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // GET: Subscription
        public async Task<IActionResult> Index()
        {
            var subs = await _context.Subscriptions
                .Include(s => s.Category)
                .OrderBy(s => s.NextBillingDate)
                .ToListAsync();

            ViewBag.TotalMonthlyCost = subs.Where(s => s.IsActive).Sum(s => s.Amount);
            ViewBag.ActiveCount = subs.Count(s => s.IsActive);

            return View(subs);
        }

        // GET: Subscription/AddOrEdit
        public async Task<IActionResult> AddOrEdit(int id = 0)
        {
            await PopulateCategoriesAsync();

            if (id == 0)
            {
                return View(new Subscription { BillingDay = DateTime.Today.Day, IsActive = true });
            }

            var sub = await _context.Subscriptions.FindAsync(id);
            if (sub == null) return NotFound();

            return View(sub);
        }

        // POST: Subscription/AddOrEdit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("SubscriptionId,Title,Amount,BillingDay,CategoryId,IsActive")] Subscription subscription)
        {
            if (ModelState.IsValid)
            {
                var today = DateTime.Today;
                int daysInCurrentMonth = DateTime.DaysInMonth(today.Year, today.Month);
                int targetDayCurrentMonth = Math.Min(subscription.BillingDay, daysInCurrentMonth);
                var candidateDate = new DateTime(today.Year, today.Month, targetDayCurrentMonth);

                if (candidateDate < today)
                {
                    // Jeśli w tym miesiącu ten dzień już minął, ustawiamy pierwsze pobranie na przyszły miesiąc
                    var nextMonth = today.AddMonths(1);
                    int daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                    int targetDayNextMonth = Math.Min(subscription.BillingDay, daysInNextMonth);
                    candidateDate = new DateTime(nextMonth.Year, nextMonth.Month, targetDayNextMonth);
                }

                subscription.NextBillingDate = candidateDate;

                if (subscription.SubscriptionId == 0)
                    _context.Add(subscription);
                else
                    _context.Update(subscription);

                await _context.SaveChangesAsync();

                // Sprawdź od razu, czy subskrypcja przypada na dziś lub w ciągu 3 dni
                await _notificationService.ProcessSubscriptionsAsync();

                TempData["SuccessMessage"] = "Zapisano subskrypcję!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateCategoriesAsync();
            return View(subscription);
        }

        // POST: Subscription/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var sub = await _context.Subscriptions.FindAsync(id);
            if (sub != null)
            {
                _context.Subscriptions.Remove(sub);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Ręczne wymuszenie sprawdzenia subskrypcji
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessNow()
        {
            await _notificationService.ProcessSubscriptionsAsync();
            TempData["SuccessMessage"] = "Sprawdzono i zaktualizowano płatności cykliczne.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCategoriesAsync()
        {
            var categories = await _context.Categories
                .Where(c => c.Type == "Expense")
                .ToListAsync();
            ViewBag.Categories = categories;
        }
    }
}