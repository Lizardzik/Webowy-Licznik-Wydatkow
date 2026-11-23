using System.Globalization;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    public class ReportController : Controller
    {
        private static readonly CultureInfo PolishCulture = CultureInfo.CreateSpecificCulture("pl-PL");
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
                        CategoryTitle = category.Title,
                        CategoryIcon = string.IsNullOrWhiteSpace(category.Icon) ? "fa-solid fa-tag" : category.Icon,
                        Amount = currentSum,
                        PreviousAmount = previousSum,
                        SharePercentage = share,
                        TransactionsCount = group.Count()
                    };
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            List<FinancialInsight> insights = BuildInsights(
                totalIncome,
                totalExpense,
                balance,
                user.MonthlyBudgetLimit,
                subsMonthlyCost,
                activeSubs.Count,
                categoryBreakdown);

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

        private static List<FinancialInsight> BuildInsights(
            decimal totalIncome,
            decimal totalExpense,
            decimal balance,
            decimal monthlyBudgetLimit,
            decimal subsMonthlyCost,
            int activeSubsCount,
            List<ReportCategoryItem> categoryBreakdown)
        {
            var insights = new List<FinancialInsight>();

            if (totalExpense > totalIncome && totalExpense > 0)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "danger",
                    Title = "Ujemny bilans w wybranym okresie",
                    Description = $"Twoje wydatki ({FormatAmount(totalExpense)}) przekroczyły przychody ({FormatAmount(totalIncome)}) o {FormatAmount(Math.Abs(balance))}. Przeanalizuj największe kategorie poniżej."
                });
            }
            else if (totalIncome > 0 && balance > 0)
            {
                double savingsRate = Math.Round((double)(balance / totalIncome) * 100, 1);
                insights.Add(new FinancialInsight
                {
                    Severity = "success",
                    Title = "Dodatni bilans i oszczędności",
                    Description = $"Zaoszczędziłeś {FormatAmount(balance)}, co stanowi {savingsRate.ToString("0.#", PolishCulture)}% Twoich przychodów w tym okresie."
                });
            }

            if (monthlyBudgetLimit > 0)
            {
                if (totalExpense > monthlyBudgetLimit)
                {
                    insights.Add(new FinancialInsight
                    {
                        Severity = "danger",
                        Title = "Przekroczono założony limit budżetu",
                        Description = $"Ustawiony limit wynosi {FormatAmount(monthlyBudgetLimit)}, natomiast wydatki osiągnęły {FormatAmount(totalExpense)} (przekroczenie o {FormatAmount(totalExpense - monthlyBudgetLimit)})."
                    });
                }
                else if (totalExpense > monthlyBudgetLimit * 0.85m)
                {
                    decimal usedPercent = Math.Round(totalExpense / monthlyBudgetLimit * 100, 0);
                    insights.Add(new FinancialInsight
                    {
                        Severity = "warning",
                        Title = "Zbliżasz się do limitu budżetu",
                        Description = $"Wykorzystałeś już {usedPercent}% swojego miesięcznego limitu ({FormatAmount(totalExpense)} z {FormatAmount(monthlyBudgetLimit)})."
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
                    Description = $"Na kategorię {topCategory.CategoryTitle} wydałeś {FormatAmount(topCategory.Amount)}, co stanowi aż {topCategory.SharePercentage.ToString("0.#", PolishCulture)}% wszystkich wydatków w tym okresie."
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
                        Description = $"Wydałeś {FormatAmount(item.Amount)} wobec {FormatAmount(item.PreviousAmount)} w poprzednim analogicznym okresie (+{FormatAmount(item.Amount - item.PreviousAmount)} więcej)."
                    });
                }
            }

            if (subsMonthlyCost > 0)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "info",
                    Title = $"Stałe opłaty i subskrypcje: {FormatAmount(subsMonthlyCost)} / mies.",
                    Description = $"Masz {activeSubsCount} aktywnych subskrypcji, które co miesiąc automatycznie obciążają Twój portfel na łączną kwotę {FormatAmount(subsMonthlyCost)}."
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

            return insights;
        }

        private static string FormatAmount(decimal amount)
        {
            return $"{amount.ToString("N2", PolishCulture)} zł";
        }
    }

    public class ReportCategoryItem
    {
        public string CategoryTitle { get; set; } = string.Empty;
        public string CategoryIcon { get; set; } = "fa-solid fa-tag";
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