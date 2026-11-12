using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
            var today = DateTime.Today;
            DateTime start = startDate?.Date ?? new DateTime(today.Year, today.Month, 1);
            DateTime end = endDate?.Date ?? today;

            if (end < start) (start, end) = (end, start);

            int daysCount = Math.Max(1, (int)(end - start).TotalDays + 1);
            DateTime prevEnd = start.AddDays(-1);
            DateTime prevStart = prevEnd.AddDays(-(daysCount - 1));

            var user = await _context.User.FirstOrDefaultAsync() ?? new User();

            var currentTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.Date.Date >= start && t.Date.Date <= end)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var prevTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.Date.Date >= prevStart && t.Date.Date <= prevEnd)
                .ToListAsync();

            var activeSubs = await _context.Subscriptions
                .Where(s => s.IsActive)
                .ToListAsync();

            int totalIncome = currentTransactions.Where(t => t.Category?.Type == "Income").Sum(t => t.Amount);
            int totalExpense = currentTransactions.Where(t => t.Category?.Type == "Expense").Sum(t => t.Amount);
            int prevTotalExpense = prevTransactions.Where(t => t.Category?.Type == "Expense").Sum(t => t.Amount);
            int balance = totalIncome - totalExpense;
            int subsMonthlyCost = activeSubs.Sum(s => s.Amount);

            // Grupowanie wydatków wg kategorii i porównanie z poprzednim okresem
            var categoryBreakdown = currentTransactions
                .Where(t => t.Category?.Type == "Expense")
                .GroupBy(t => t.CategoryId)
                .Select(g =>
                {
                    var cat = g.First().Category!;
                    int currSum = g.Sum(t => t.Amount);
                    int prevSum = prevTransactions
                        .Where(pt => pt.CategoryId == g.Key && pt.Category?.Type == "Expense")
                        .Sum(pt => pt.Amount);

                    double share = totalExpense > 0 ? Math.Round((double)currSum / totalExpense * 100, 1) : 0;

                    return new ReportCategoryItem
                    {
                        CategoryTitle = $"{cat.Icon} {cat.Title}",
                        Amount = currSum,
                        PreviousAmount = prevSum,
                        SharePercentage = share,
                        TransactionsCount = g.Count()
                    };
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            // Automatyczna diagnoza problemów finansowych
            var insights = new List<FinancialInsight>();

            // 1. Bilans okresu
            if (totalExpense > totalIncome && totalExpense > 0)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "danger",
                    Title = "Ujemny bilans w wybranym okresie",
                    Description = $"Twoje wydatki ({totalExpense:N0} zł) przekroczyły przychody ({totalIncome:N0} zł) o {Math.Abs(balance):N0} zł. Przeanalizuj największe kategorie poniżej."
                });
            }
            else if (totalIncome > 0 && balance > 0)
            {
                double savingsRate = Math.Round((double)balance / totalIncome * 100, 1);
                insights.Add(new FinancialInsight
                {
                    Severity = "success",
                    Title = "Dodatni bilans i oszczędności",
                    Description = $"Zaoszczędziłeś {balance:N0} zł, co stanowi {savingsRate}% Twoich przychodów w tym okresie."
                });
            }

            // 2. Przekroczenie miesięcznego limitu budżetowego
            if (user.MonthlyBudgetLimit > 0)
            {
                if (totalExpense > user.MonthlyBudgetLimit)
                {
                    insights.Add(new FinancialInsight
                    {
                        Severity = "danger",
                        Title = "Przekroczono założony limit budżetu!",
                        Description = $"Ustawiony limit wynosi {user.MonthlyBudgetLimit:N0} zł, natomiast wydatki osiągnęły {totalExpense:N0} zł (przekroczenie o {(totalExpense - user.MonthlyBudgetLimit):N0} zł)."
                    });
                }
                else if (totalExpense > user.MonthlyBudgetLimit * 0.85m)
                {
                    insights.Add(new FinancialInsight
                    {
                        Severity = "warning",
                        Title = "Zbliżasz się do limitu budżetu",
                        Description = $"Wykorzystałeś już {Math.Round((decimal)totalExpense / user.MonthlyBudgetLimit * 100, 0)}% swojego miesięcznego limitu ({totalExpense:N0} zł z {user.MonthlyBudgetLimit:N0} zł)."
                    });
                }
            }

            // 3. Dominująca kategoria wydatków
            var topCategory = categoryBreakdown.FirstOrDefault();
            if (topCategory != null && topCategory.SharePercentage >= 35)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "warning",
                    Title = $"Jedna kategoria pochłania większość budżetu ({topCategory.CategoryTitle})",
                    Description = $"Na kategorię {topCategory.CategoryTitle} wydałeś {topCategory.Amount:N0} zł, co stanowi aż {topCategory.SharePercentage}% wszystkich wydatków w tym okresie."
                });
            }

            // 4. Nagłe wzrosty względem poprzedniego okresu
            foreach (var item in categoryBreakdown.Where(c => c.PreviousAmount > 0 && c.Amount > c.PreviousAmount))
            {
                double increasePct = Math.Round((double)(item.Amount - item.PreviousAmount) / item.PreviousAmount * 100, 0);
                if (increasePct >= 25 && (item.Amount - item.PreviousAmount) >= 100)
                {
                    insights.Add(new FinancialInsight
                    {
                        Severity = "danger",
                        Title = $"Duży wzrost wydatków: {item.CategoryTitle} (+{increasePct}%)",
                        Description = $"Wydałeś {item.Amount:N0} zł wobec {item.PreviousAmount:N0} zł w poprzednim analogicznym okresie (+{(item.Amount - item.PreviousAmount):N0} zł więcej)."
                    });
                }
            }

            // 5. Koszt subskrypcji
            if (subsMonthlyCost > 0)
            {
                insights.Add(new FinancialInsight
                {
                    Severity = "info",
                    Title = $"Stałe opłaty i subskrypcje: {subsMonthlyCost:N0} zł / mies.",
                    Description = $"Masz {activeSubs.Count} aktywnych subskrypcji, które co miesiąc automatycznie obciążają Twój portfel na łączną kwotę {subsMonthlyCost:N0} zł."
                });
            }

            if (!insights.Any())
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
        public string CategoryTitle { get; set; } = "";
        public int Amount { get; set; }
        public int PreviousAmount { get; set; }
        public double SharePercentage { get; set; }
        public int TransactionsCount { get; set; }
        public int Difference => Amount - PreviousAmount;
    }

    public class FinancialInsight
    {
        public string Severity { get; set; } = "info"; // danger, warning, success, info
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}