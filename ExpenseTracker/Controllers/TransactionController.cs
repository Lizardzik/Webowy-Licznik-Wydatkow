using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public TransactionController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            List<Transaction> transactions = await _context.Transactions
                .Include(t => t.Category)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            return View(transactions);
        }

        public async Task<IActionResult> AddOrEdit(int id = 0)
        {
            await PopulateCategoriesAsync();

            if (id == 0)
            {
                return View(new Transaction { Date = DateTime.Today });
            }

            Transaction? transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("TransactionId,CategoryId,Amount,Note,Date")] Transaction transaction)
        {
            ModelState.Remove("Category");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                User user = await _context.GetOrCreateDefaultUserAsync();
                Category? newCategory = await _context.Categories.FindAsync(transaction.CategoryId);

                if (transaction.TransactionId == 0)
                {
                    transaction.UserId = user.Id;
                    _context.Add(transaction);

                    if (newCategory != null)
                    {
                        user.WalletBalance += newCategory.Type == "Income"
                            ? transaction.Amount
                            : -transaction.Amount;
                    }

                    await _context.SaveChangesAsync();
                    await _notificationService.CreateTransactionNotification(transaction);
                }
                else
                {
                    Transaction? existingTransaction = await _context.Transactions
                        .Include(t => t.Category)
                        .FirstOrDefaultAsync(t => t.TransactionId == transaction.TransactionId);

                    if (existingTransaction == null)
                    {
                        return NotFound();
                    }

                    if (existingTransaction.Category != null)
                    {
                        user.WalletBalance -= existingTransaction.Category.Type == "Income"
                            ? existingTransaction.Amount
                            : -existingTransaction.Amount;
                    }

                    existingTransaction.CategoryId = transaction.CategoryId;
                    existingTransaction.Amount = transaction.Amount;
                    existingTransaction.Note = transaction.Note;
                    existingTransaction.Date = transaction.Date;
                    existingTransaction.UserId = user.Id;

                    if (newCategory != null)
                    {
                        user.WalletBalance += newCategory.Type == "Income"
                            ? transaction.Amount
                            : -transaction.Amount;
                    }

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            await PopulateCategoriesAsync();
            return View(transaction);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            Transaction? transaction = await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == id);

            if (transaction != null)
            {
                User user = await _context.GetOrCreateDefaultUserAsync();
                if (transaction.Category != null)
                {
                    user.WalletBalance -= transaction.Category.Type == "Income"
                        ? transaction.Amount
                        : -transaction.Amount;
                }

                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCategoriesAsync()
        {
            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.Title)
                .ToListAsync();
        }
    }
}