using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ExpenseTracker.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ActionResult> Index()
        {
            User currentUser = await _context.GetOrCreateDefaultUserAsync();
            int userId = currentUser.Id;

            DateTime startDate = DateTime.Today.AddDays(-7);
            CultureInfo culture = CultureInfo.CreateSpecificCulture("pl-PL");
            culture.NumberFormat.CurrencyNegativePattern = 1;

            List<Transaction> transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .Include(x => x.Category)
                .ToListAsync();

            decimal income = transactions
                .Where(i => i.Category != null && i.Category.Type == "Income")
                .Sum(j => j.Amount);
            ViewBag.Income = income.ToString("C2", culture);

            decimal currentMonthExpense = transactions
                .Where(i => i.Category != null && i.Category.Type == "Expense"
                            && i.Date.Year == DateTime.Today.Year
                            && i.Date.Month == DateTime.Today.Month)
                .Sum(j => j.Amount);
            ViewBag.CurrentMonthExpense = currentMonthExpense;

            decimal expense = transactions
                .Where(i => i.Category != null && i.Category.Type == "Expense")
                .Sum(j => j.Amount);
            ViewBag.Expense = expense.ToString("C2", culture);

            decimal balance = income - expense;
            ViewBag.Balance = balance.ToString("C2", culture);

            ViewBag.CharData = transactions
                .Where(i => i.Category != null && i.Category.Type == "Expense")
                .GroupBy(j => j.Category!.CategoryId)
                .Select(k => new
                {
                    categoryTitleWithIcon = k.First().Category!.Title,
                    amount = k.Sum(j => j.Amount),
                    formattedAmount = k.Sum(j => j.Amount).ToString("C2", culture),
                })
                .OrderByDescending(l => l.amount)
                .ToList();

            List<ChartData> incomeData = transactions
                .Where(i => i.Category != null && i.Category.Type == "Income")
                .GroupBy(j => j.Date.Date)
                .Select(k => new ChartData
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    income = k.Sum(l => l.Amount)
                })
                .ToList();

            List<ChartData> expenseData = transactions
                .Where(i => i.Category != null && i.Category.Type == "Expense")
                .GroupBy(j => j.Date.Date)
                .Select(k => new ChartData
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    expense = k.Sum(l => l.Amount)
                })
                .ToList();

            string[] lastWeek = Enumerable.Range(0, 7)
                .Select(i => startDate.AddDays(i).ToString("dd-MMM"))
                .ToArray();

            ViewBag.ChartData = from day in lastWeek
                                join inc in incomeData on day equals inc.day into dayIncome
                                from inc in dayIncome.DefaultIfEmpty()
                                join exp in expenseData on day equals exp.day into dayExpense
                                from exp in dayExpense.DefaultIfEmpty()
                                select new
                                {
                                    day = day,
                                    income = inc == null ? 0m : inc.income,
                                    expense = exp == null ? 0m : exp.expense,
                                };

            ViewBag.RecentTransactions = await _context.Transactions
                .Include(i => i.Category)
                .OrderByDescending(j => j.Date)
                .Take(5)
                .Select(t => new
                {
                    t.Category!.Title,
                    CategoryIcon = t.Category.Icon,
                    t.Date,
                    t.Amount,
                    FormattedAmount = t.Amount.ToString("C2", culture),
                    CategoryTitleWithIcon = t.Category.Title,
                    CategoryType = t.Category.Type
                })
                .ToListAsync();

            ViewBag.RecentCategories = await _context.Categories
                .OrderByDescending(i => i.CategoryId)
                .Take(5)
                .Select(c => new
                {
                    TitleWithIcon = c.Title,
                    c.Icon,
                    c.Type
                })
                .ToListAsync();

            ViewBag.RecentUsers = await _context.User
                .OrderByDescending(i => i.Id)
                .Take(3)
                .ToListAsync();

            HttpContext.Session.SetString("UserImage", currentUser?.DisplayImage ?? string.Empty);
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("WelcomeShown")))
            {
                HttpContext.Session.SetString("WelcomeShown", "true");
                string displayName = !string.IsNullOrWhiteSpace(currentUser?.Nickname)
                    ? currentUser.Nickname
                    : !string.IsNullOrWhiteSpace(currentUser?.FirstName)
                        ? currentUser.FirstName
                        : "Użytkownik";
                TempData["WelcomeUser"] = displayName;
            }
            return View(currentUser);
        }

        [HttpGet]
        public async Task<IActionResult> Compare(string? month1, string? month2)
        {
            DateTime secondMonthDate = ParseMonthOrDefault(month2, new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            DateTime firstMonthDate = ParseMonthOrDefault(month1, secondMonthDate.AddMonths(-1));

            CultureInfo culture = CultureInfo.CreateSpecificCulture("pl-PL");

            List<Transaction> firstMonthTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.Date.Year == firstMonthDate.Year && t.Date.Month == firstMonthDate.Month)
                .ToListAsync();

            List<Transaction> secondMonthTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.Date.Year == secondMonthDate.Year && t.Date.Month == secondMonthDate.Month)
                .ToListAsync();

            decimal m1Expense = firstMonthTransactions
                .Where(t => t.Category != null && t.Category.Type == "Expense")
                .Sum(t => t.Amount);

            decimal m1Income = firstMonthTransactions
                .Where(t => t.Category != null && t.Category.Type == "Income")
                .Sum(t => t.Amount);

            decimal m2Expense = secondMonthTransactions
                .Where(t => t.Category != null && t.Category.Type == "Expense")
                .Sum(t => t.Amount);

            decimal m2Income = secondMonthTransactions
                .Where(t => t.Category != null && t.Category.Type == "Income")
                .Sum(t => t.Amount);

            var allCategories = firstMonthTransactions
                .Concat(secondMonthTransactions)
                .Where(t => t.Category != null && t.Category.Type == "Expense")
                .Select(t => t.Category!)
                .GroupBy(c => c.CategoryId)
                .Select(g => g.First())
                .ToList();

            List<CategoryComparisonItem> comparisonItems = allCategories
                .Select(category =>
                {
                    decimal amount1 = firstMonthTransactions
                        .Where(t => t.CategoryId == category.CategoryId && t.Category?.Type == "Expense")
                        .Sum(t => t.Amount);

                    decimal amount2 = secondMonthTransactions
                        .Where(t => t.CategoryId == category.CategoryId && t.Category?.Type == "Expense")
                        .Sum(t => t.Amount);

                    return new CategoryComparisonItem
                    {
                        CategoryName = category.Title,
                        CategoryIcon = category.Icon,
                        Month1Amount = amount1,
                        Month2Amount = amount2,
                        Difference = amount2 - amount1
                    };
                })
                .OrderByDescending(item => item.Month2Amount)
                .ThenByDescending(item => item.Month1Amount)
                .ToList();

            ViewBag.Month1Value = firstMonthDate.ToString("yyyy-MM");
            ViewBag.Month2Value = secondMonthDate.ToString("yyyy-MM");
            ViewBag.Month1Label = firstMonthDate.ToString("MMMM yyyy", culture);
            ViewBag.Month2Label = secondMonthDate.ToString("MMMM yyyy", culture);
            ViewBag.M1Expense = m1Expense;
            ViewBag.M1Income = m1Income;
            ViewBag.M2Expense = m2Expense;
            ViewBag.M2Income = m2Income;
            ViewBag.ExpenseDiff = m2Expense - m1Expense;
            ViewBag.ComparisonData = comparisonItems;

            return View(comparisonItems);
        }

        private static DateTime ParseMonthOrDefault(string? monthInput, DateTime fallback)
        {
            if (!string.IsNullOrWhiteSpace(monthInput) &&
                DateTime.TryParseExact(monthInput + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }

    public class ChartData
    {
        public string day = string.Empty;
        public decimal income;
        public decimal expense;
    }

    public class CategoryComparisonItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryIcon { get; set; } = string.Empty;
        public decimal Month1Amount { get; set; }
        public decimal Month2Amount { get; set; }
        public decimal Difference { get; set; }
    }
}