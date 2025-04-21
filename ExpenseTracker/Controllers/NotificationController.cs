using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public NotificationController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ✅ Liczenie globalnie (dla wszystkich użytkowników)
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var transactionNotificationsCount = await _context.Notifications
                    .Where(n => !n.IsRead && n.Type == NotificationType.TransactionAdded)
                    .CountAsync();

                var weeklyReportExists = await _context.Notifications
                    .AnyAsync(n => !n.IsRead && n.Type == NotificationType.WeeklyReport);

                return Json(new
                {
                    success = true,
                    transactionCount = transactionNotificationsCount,
                    hasWeeklyReport = weeklyReportExists
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas pobierania liczby powiadomień: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ✅ Generowanie testowego raportu
        [HttpPost]
        public async Task<IActionResult> GenerateWeeklyReport()
        {
            try
            {
                // testowo: ID = 1, albo generowanie globalnie
                await _notificationService.CreateWeeklyReportNotification(0); // możesz zmienić na stałe ID lub dodać globalną obsługę
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas generowania raportu: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ✅ Wyświetlanie wszystkich powiadomień (bez filtrowania po użytkowniku)
        public async Task<IActionResult> Index()
        {
            var notifications = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        // ✅ Oznaczanie wszystkich powiadomień jako przeczytane
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                await _notificationService.MarkAllAsRead(); // zmodyfikujemy serwis
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas oznaczania powiadomień jako przeczytane: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
