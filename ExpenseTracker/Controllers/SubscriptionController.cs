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

        public async Task<IActionResult> Index()
        {
            List<Subscription> subscriptions = await _context.Subscriptions
                .Include(s => s.Category)
                .OrderBy(s => s.NextBillingDate)
                .ToListAsync();

            ViewBag.TotalMonthlyCost = subscriptions.Where(s => s.IsActive).Sum(s => s.Amount);
            ViewBag.ActiveCount = subscriptions.Count(s => s.IsActive);

            return View(subscriptions);
        }

        public async Task<IActionResult> AddOrEdit(int id = 0)
        {
            await PopulateCategoriesAsync();

            if (id == 0)
            {
                return View(new Subscription { BillingDay = DateTime.Today.Day, IsActive = true });
            }

            Subscription? subscription = await _context.Subscriptions.FindAsync(id);
            if (subscription == null)
            {
                return NotFound();
            }

            return View(subscription);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("SubscriptionId,Title,Amount,BillingDay,CategoryId,IsActive")] Subscription subscription)
        {
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                DateTime today = DateTime.Today;
                int daysInCurrentMonth = DateTime.DaysInMonth(today.Year, today.Month);
                int targetDayCurrentMonth = Math.Min(subscription.BillingDay, daysInCurrentMonth);
                DateTime candidateDate = new DateTime(today.Year, today.Month, targetDayCurrentMonth);

                if (candidateDate < today)
                {
                    DateTime nextMonth = today.AddMonths(1);
                    int daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                    int targetDayNextMonth = Math.Min(subscription.BillingDay, daysInNextMonth);
                    candidateDate = new DateTime(nextMonth.Year, nextMonth.Month, targetDayNextMonth);
                }

                subscription.NextBillingDate = candidateDate;

                if (subscription.SubscriptionId == 0)
                {
                    _context.Add(subscription);
                }
                else
                {
                    _context.Update(subscription);
                }

                await _context.SaveChangesAsync();
                await _notificationService.ProcessSubscriptionsAsync();

                TempData["SuccessMessage"] = "Zapisano subskrypcję.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateCategoriesAsync();
            return View(subscription);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            Subscription? subscription = await _context.Subscriptions.FindAsync(id);
            if (subscription != null)
            {
                _context.Subscriptions.Remove(subscription);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

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
            List<Category> categories = await _context.Categories
                .Where(c => c.Type == "Expense")
                .OrderBy(c => c.Title)
                .ToListAsync();
            ViewBag.Categories = categories;
        }
    }
}