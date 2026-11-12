using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateNotification(string message, NotificationType type)
        {
            User? user = await _context.User.FirstOrDefaultAsync();

            var notification = new Notification
            {
                Message = message,
                CreatedAt = DateTime.Now,
                Type = type,
                IsRead = false,
                UserId = user?.Id ?? 1
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task CreateTransactionNotification(Transaction transaction)
        {
            try
            {
                Category? category = await _context.Categories.FindAsync(transaction.CategoryId);
                string categoryTitle = category?.Title ?? "Unknown Category";

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas tworzenia powiadomienia: {ex.Message}");
            }
        }

        public async Task ProcessSubscriptionsAsync()
        {
            List<Subscription> activeSubscriptions = await _context.Subscriptions
                .Include(s => s.Category)
                .Where(s => s.IsActive)
                .ToListAsync();

            if (!activeSubscriptions.Any())
            {
                return;
            }

            User? user = await _context.User.FirstOrDefaultAsync();
            int userId = user?.Id ?? 1;
            DateTime today = DateTime.Today;

            foreach (Subscription subscription in activeSubscriptions)
            {
                if (subscription.NextBillingDate.Date <= today)
                {
                    var transaction = new Transaction
                    {
                        CategoryId = subscription.CategoryId,
                        Amount = subscription.Amount,
                        Date = subscription.NextBillingDate.Date,
                        Note = $"Subskrypcja: {subscription.Title}",
                        UserId = userId
                    };

                    _context.Transactions.Add(transaction);
                    subscription.LastProcessedDate = today;

                    DateTime nextMonth = subscription.NextBillingDate.AddMonths(1);
                    int daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                    int targetDay = Math.Min(subscription.BillingDay, daysInNextMonth);
                    subscription.NextBillingDate = new DateTime(nextMonth.Year, nextMonth.Month, targetDay);

                    _context.Notifications.Add(new Notification
                    {
                        Message = $"Zaksięgowano subskrypcję: {subscription.Title} ({subscription.Amount} zł).",
                        CreatedAt = DateTime.Now,
                        Type = NotificationType.TransactionAdded,
                        IsRead = false,
                        UserId = userId
                    });
                }
                else if ((subscription.NextBillingDate.Date - today).TotalDays <= 3)
                {
                    string reminderPrefix = $"Nadchodząca płatność: {subscription.Title}";
                    bool alreadyNotifiedToday = await _context.Notifications.AnyAsync(n =>
                        n.Message.StartsWith(reminderPrefix) && n.CreatedAt.Date == today);

                    if (!alreadyNotifiedToday)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            Message = $"{reminderPrefix} ({subscription.Amount} zł) w dniu {subscription.NextBillingDate:dd.MM.yyyy}.",
                            CreatedAt = DateTime.Now,
                            Type = NotificationType.TransactionAdded,
                            IsRead = false,
                            UserId = userId
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task CreateWeeklyReportNotification(int userId)
        {
            try
            {
                DateTime startDate = DateTime.Now.AddDays(-7);
                DateTime endDate = DateTime.Now;

                List<Transaction> transactions = await _context.Transactions
                    .Where(t => t.UserId == userId && t.Date >= startDate && t.Date <= endDate)
                    .ToListAsync();

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas generowania raportu tygodniowego: {ex.Message}");
            }
        }

        public async Task<List<Notification>> GetUnreadNotifications(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task MarkAllAsRead()
        {
            List<Notification> unreadNotifications = await _context.Notifications
                .Where(n => !n.IsRead)
                .ToListAsync();

            foreach (Notification notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }
    }
}