using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Controllers
{
    [RequireLogin]
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public TransactionController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // GET: Transaction
        public async Task<IActionResult> Index()
        {
            var userNickname = HttpContext.Session.GetString("Nickname");
            var userId = _context.User
                                .Where(u => u.Nickname == userNickname)
                                .Select(u => u.Id)
                                .FirstOrDefault();

            var userTransactions = _context.Transactions
                                            .Where(t => t.UserId == userId)
                                            .Include(t => t.Category)
                                            .ToListAsync();

            return View(await userTransactions);
        }

        // GET: Transaction/AddOrEdit
        public IActionResult AddOrEdit(int id=0)
        {
            PopulateCategories();

            if(id == 0)
            {
                return View(new Transaction());
            }
            else
            {
                return View(_context.Transactions.Find(id));
            }
           
        }

        // POST: Transaction/AddOrEdit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("TransactionId,CategoryId,Amount,Note,Date")] Transaction transaction)
        {
            var userNickname = HttpContext.Session.GetString("Nickname");
            var userId = _context.User.Where(u => u.Nickname == userNickname).Select(u => u.Id).FirstOrDefault();

            if (ModelState.IsValid)
            {
                transaction.UserId = userId;

                if (transaction.TransactionId == 0)
                    _context.Add(transaction);
                else
                    _context.Update(transaction);

                // Najpierw zapisz zmiany
                await _context.SaveChangesAsync();

                // Dopiero potem wyślij powiadomienie
                await _notificationService.CreateTransactionNotification(transaction);

                return RedirectToAction(nameof(Index));
            }

            PopulateCategories();
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Title", transaction.CategoryId);
            return View(transaction);
        }

        // POST: Transaction/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [NonAction]
        public void PopulateCategories()
        {
            var CategoryCollection = _context.Categories.ToList();
            ViewBag.Categories = CategoryCollection;
        }
    }
}
