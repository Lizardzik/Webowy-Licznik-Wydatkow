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

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                int transactionNotificationsCount = await _context.Notifications
                    .Where(n => !n.IsRead && n.Type == NotificationType.TransactionAdded)
                    .CountAsync();

                bool weeklyReportExists = await _context.Notifications
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
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateWeeklyReport()
        {
            try
            {
                User user = await _context.GetOrCreateDefaultUserAsync();
                await _notificationService.CreateWeeklyReportNotification(user.Id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Index()
        {
            List<Notification> notifications = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                await _notificationService.MarkAllAsRead();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}