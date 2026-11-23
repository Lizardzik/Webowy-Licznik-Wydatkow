using System.Globalization;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class NotificationService
    {
        private static readonly CultureInfo PolishCulture = new("pl-PL");
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
                string categoryTitle = category?.Title ?? "Nieznana kategoria";
                string operationLabel = category?.Type == "Income" ? "Nowy przychód" : "Nowy wydatek";

                var notification = new Notification
                {
                    Message = $"{operationLabel}: {categoryTitle} ({FormatAmount(transaction.Amount)}).",
                    CreatedAt = DateTime.Now,
                    Type = NotificationType.TransactionAdded,
                    IsRead = false,
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
                        Message = $"Zaksięgowano subskrypcję: {subscription.Title} ({FormatAmount(subscription.Amount)}).",
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
                            Message = $"{reminderPrefix} ({FormatAmount(subscription.Amount)}) w dniu {subscription.NextBillingDate:dd.MM.yyyy}.",
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
                DateTime startDate = DateTime.Today.AddDays(-6);
                DateTime endDate = DateTime.Now;

                List<Transaction> transactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == userId && t.Date >= startDate && t.Date <= endDate)
                    .ToListAsync();

                decimal totalIncome = transactions
                    .Where(t => t.Category != null && t.Category.Type == "Income")
                    .Sum(t => t.Amount);

                decimal totalExpense = transactions
                    .Where(t => t.Category == null || t.Category.Type != "Income")
                    .Sum(t => t.Amount);

                decimal netBalance = totalIncome - totalExpense;

                var topExpenseGroup = transactions
                    .Where(t => t.Category == null || t.Category.Type != "Income")
                    .GroupBy(t => t.Category?.Title ?? "Inne")
                    .Select(group => new
                    {
                        CategoryTitle = group.Key,
                        Total = group.Sum(t => t.Amount)
                    })
                    .OrderByDescending(item => item.Total)
                    .FirstOrDefault();

                string topCategorySummary = topExpenseGroup != null
                    ? $"{topExpenseGroup.CategoryTitle} ({FormatAmount(topExpenseGroup.Total)})"
                    : "Brak wydatków";

                string reportMessage = string.Join(" | ", new[]
                {
                    $"Raport tygodniowy ({startDate:dd.MM} - {endDate:dd.MM.yyyy})",
                    $"Przychody: {FormatAmount(totalIncome)}",
                    $"Wydatki: {FormatAmount(totalExpense)}",
                    $"Bilans: {FormatAmount(netBalance)}",
                    $"Operacje: {transactions.Count}",
                    $"Największe wydatki: {topCategorySummary}"
                });

                var notification = new Notification
                {
                    Type = NotificationType.WeeklyReport,
                    Message = reportMessage,
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

        private static string FormatAmount(decimal amount)
        {
            return $"{amount.ToString("N2", PolishCulture)} zł";
        }
    }
}