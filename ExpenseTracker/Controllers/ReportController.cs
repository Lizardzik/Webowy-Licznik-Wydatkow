using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? startDate = null, DateTime? endDate = null)
        {
            DateTime today = DateTime.Today;
            DateTime start = startDate?.Date ?? new DateTime(today.Year, today.Month, 1);
            DateTime end = endDate?.Date ?? today;

            if (end < start)
            {
                (start, end) = (end, start);
            }

            int daysCount = Math.Max(1, (int)(end - start).TotalDays + 1);
            DateTime prevEnd = start.AddDays(-1);
            DateTime prevStart = prevEnd.AddDays(-(daysCount - 1));

            User user = await _context.User.FirstOrDefaultAsync() ?? new User();

            List<Transaction> currentTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.Date.Date >= start && t.Date.Date <= end)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            List<Transaction> prevTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.Date.Date >= prevStart && t.Date.Date <= prevEnd)
                .ToListAsync();

            List<Subscription> activeSubs = await _context.Subscriptions
                .Where(s => s.IsActive)
                .ToListAsync();

            decimal totalIncome = currentTransactions.Where(t => t.Category?.Type == "Income").Sum(t => t.Amount);
            decimal totalExpense = currentTransactions.Where(t => t.Category?.Type == "Expense").Sum(t => t.Amount);
            decimal prevTotalExpense = prevTransactions.Where(t => t.Category?.Type == "Expense").Sum(t => t.Amount);
            decimal balance = totalIncome - totalExpense;
            decimal subsMonthlyCost = activeSubs.Sum(s => s.Amount);

            List<ReportCategoryItem> categoryBreakdown = currentTransactions
                .Where(t => t.Category?.Type == "Expense")
                .GroupBy(t => t.CategoryId)
                .Select(group =>
                {
                    Category category = group.First().Category!;
                    decimal currentSum = group.Sum(t => t.Amount);
                    decimal previousSum = prevTransactions
                        .Where(pt => pt.CategoryId == group.Key && pt.Category?.Type == "Expense")
                        .Sum(pt => pt.Amount);

                    double share = totalExpense > 0 ? Math.Round((double)(currentSum / totalExpense) * 100, 1) : 0;

                    return new ReportCategoryItem
                    {
                        CategoryTitle = $"{category.Icon} {category.Title}",
                        Amount = currentSum,
                        PreviousAmount = previousSum,
                        SharePercentage = share,
                        TransactionsCount = group.Count()
                    };
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            var insights = new List<FinancialInsight>();

            if (totalExpense > totalIncome && totalExpense > 0)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "danger",
                    Title = "Ujemny bilans w wybranym okresie",
                    Description = $"Twoje wydatki ({totalExpense:N2} zł) przekroczyły przychody ({totalIncome:N2} zł) o {Math.Abs(balance):N2} zł. Przeanalizuj największe kategorie poniżej."
                });
            }
            else if (totalIncome > 0 && balance > 0)
            {
                double savingsRate = Math.Round((double)(balance / totalIncome) * 100, 1);
                insights.Add(new FinancialInsight
                {
                    Severity = "success",
                    Title = "Dodatni bilans i oszczędności",
                    Description = $"Zaoszczędziłeś {balance:N2} zł, co stanowi {savingsRate}% Twoich przychodów w tym okresie."
                });
            }

            if (user.MonthlyBudgetLimit > 0)
            {
                if (totalExpense > user.MonthlyBudgetLimit)
                {
                    insights.Add(new FinancialInsight
                    {
                        Severity = "danger",
                        Title = "Przekroczono założony limit budżetu!",
                        Description = $"Ustawiony limit wynosi {user.MonthlyBudgetLimit:N2} zł, natomiast wydatki osiągnęły {totalExpense:N2} zł (przekroczenie o {(totalExpense - user.MonthlyBudgetLimit):N2} zł)."
                    });
                }
                else if (totalExpense > user.MonthlyBudgetLimit * 0.85m)
                {
                    insights.Add(new FinancialInsight
                    {
                        Severity = "warning",
                        Title = "Zbliżasz się do limitu budżetu",
                        Description = $"Wykorzystałeś już {Math.Round(totalExpense / user.MonthlyBudgetLimit * 100, 0)}% swojego miesięcznego limitu ({totalExpense:N2} zł z {user.MonthlyBudgetLimit:N2} zł)."
                    });
                }
            }

            ReportCategoryItem? topCategory = categoryBreakdown.FirstOrDefault();
            if (topCategory != null && topCategory.SharePercentage >= 35)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "warning",
                    Title = $"Jedna kategoria pochłania większość budżetu ({topCategory.CategoryTitle})",
                    Description = $"Na kategorię {topCategory.CategoryTitle} wydałeś {topCategory.Amount:N2} zł, co stanowi aż {topCategory.SharePercentage}% wszystkich wydatków w tym okresie."
                });
            }

            foreach (ReportCategoryItem item in categoryBreakdown.Where(c => c.PreviousAmount > 0 && c.Amount > c.PreviousAmount))
            {
                double increasePct = Math.Round((double)((item.Amount - item.PreviousAmount) / item.PreviousAmount) * 100, 0);
                if (increasePct >= 25 && (item.Amount - item.PreviousAmount) >= 100m)
                {
                    insights.Add(new FinancialInsight
                    {
                        Severity = "danger",
                        Title = $"Duży wzrost wydatków: {item.CategoryTitle} (+{increasePct}%)",
                        Description = $"Wydałeś {item.Amount:N2} zł wobec {item.PreviousAmount:N2} zł w poprzednim analogicznym okresie (+{(item.Amount - item.PreviousAmount):N2} zł więcej)."
                    });
                }
            }

            if (subsMonthlyCost > 0)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "info",
                    Title = $"Stałe opłaty i subskrypcje: {subsMonthlyCost:N2} zł / mies.",
                    Description = $"Masz {activeSubs.Count} aktywnych subskrypcji, które co miesiąc automatycznie obciążają Twój portfel na łączną kwotę {subsMonthlyCost:N2} zł."
                });
            }

            if (insights.Count == 0)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "info",
                    Title = "Brak wykrytych anomalii finansowych",
                    Description = "Twoje wydatki mieszczą się w normie lub brakuje jeszcze wystarczającej historii do porównania."
                });
            }

            ViewBag.StartDate = start.ToString("yyyy-MM-dd");
            ViewBag.EndDate = end.ToString("yyyy-MM-dd");
            ViewBag.PeriodDisplay = $"{start:dd.MM.yyyy} – {end:dd.MM.yyyy}";
            ViewBag.PrevPeriodDisplay = $"{prevStart:dd.MM.yyyy} – {prevEnd:dd.MM.yyyy}";

            ViewBag.TotalIncome = totalIncome;
            ViewBag.TotalExpense = totalExpense;
            ViewBag.PrevTotalExpense = prevTotalExpense;
            ViewBag.Balance = balance;
            ViewBag.WalletBalance = user.WalletBalance;

            ViewBag.CategoryBreakdown = categoryBreakdown;
            ViewBag.Insights = insights;
            ViewBag.TopExpenses = currentTransactions
                .Where(t => t.Category?.Type == "Expense")
                .OrderByDescending(t => t.Amount)
                .Take(10)
                .ToList();

            return View();
        }
    }

    public class ReportCategoryItem
    {
        public string CategoryTitle { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal PreviousAmount { get; set; }
        public double SharePercentage { get; set; }
        public int TransactionsCount { get; set; }
        public decimal Difference => Amount - PreviousAmount;
    }

    public class FinancialInsight
    {
        public string Severity { get; set; } = "info";
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}