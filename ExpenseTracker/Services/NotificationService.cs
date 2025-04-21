using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Powiadomienie o nowej transakcji
        public async Task CreateTransactionNotification(Transaction transaction)
        {
            try
            {
                var category = await _context.Categories.FindAsync(transaction.CategoryId);
                var categoryTitle = category?.Title ?? "Unknown Category";

                var notification = new Notification
                {
                    Message = $"New Transaction {categoryTitle} worth {transaction.Amount:C} has been added.",
                    CreatedAt = DateTime.Now,
                    Type = NotificationType.TransactionAdded,
                    UserId = transaction.UserId,
                    TransactionId = transaction.TransactionId
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Powiadomienie utworzone: {notification.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas tworzenia powiadomienia: {ex.Message}");
            }
        }

        // Generowanie tygodniowego raportu
        public async Task CreateWeeklyReportNotification(int userId)
        {
            try
            {
                var startDate = DateTime.Now.AddDays(-7);
                var endDate = DateTime.Now;

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == userId && t.Date >= startDate && t.Date <= endDate)
                    .ToListAsync();

                var totalAmount = transactions.Sum(t => t.Amount);
                var categorySpending = transactions
                    .GroupBy(t => t.Category.Title)
                    .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                var topCategories = string.Join(", ", categorySpending.Take(3).Select(c => $"{c.Category}: {c.Total:C}"));

                var notification = new Notification
                {
                    Type = NotificationType.WeeklyReport,
                    Message = "Nowy raport tygodniowy",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    UserId = userId 
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Tygodniowy raport wygenerowany: {notification.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas generowania raportu tygodniowego: {ex.Message}");
            }
        }

        // Pobieranie nieprzeczytanych powiadomień
        public async Task<List<Notification>> GetUnreadNotifications(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        // Oznaczanie wszystkich powiadomień jako przeczytane
        public async Task MarkAllAsRead()
        {
            var all = await _context.Notifications.Where(n => !n.IsRead).ToListAsync();
            foreach (var n in all) n.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }
}