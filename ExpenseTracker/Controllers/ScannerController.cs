using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    public class ScannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ReceiptScannerService _scannerService;
        private readonly NotificationService _notificationService;

        public ScannerController(
            ApplicationDbContext context,
            ReceiptScannerService scannerService,
            NotificationService notificationService)
        {
            _context = context;
            _scannerService = scannerService;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.Title)
                .ToListAsync();

            return View(new List<ScannedTransactionDto>());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Analyze(IFormFile? receiptImage)
        {
            List<Category> categories = await _context.Categories
                .OrderBy(c => c.Title)
                .ToListAsync();
            ViewBag.Categories = categories;

            if (receiptImage == null || receiptImage.Length == 0)
            {
                TempData["ErrorMessage"] = "Wybierz plik ze zdjęciem paragonu lub wklej zrzut ekranu.";
                return View("Index", new List<ScannedTransactionDto>());
            }

            if (!categories.Any())
            {
                TempData["ErrorMessage"] = "Dodaj najpierw przynajmniej jedną kategorię przed skanowaniem.";
                return View("Index", new List<ScannedTransactionDto>());
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await receiptImage.CopyToAsync(memoryStream);
                byte[] imageBytes = memoryStream.ToArray();
                string mimeType = string.IsNullOrWhiteSpace(receiptImage.ContentType)
                    ? "image/png"
                    : receiptImage.ContentType;

                ScanResultDto scanResult = await _scannerService.ExtractTransactionsAsync(
                    imageBytes,
                    mimeType,
                    categories);

                ViewBag.UsedModel = scanResult.UsedModel;

                if (!scanResult.Transactions.Any())
                {
                    TempData["ErrorMessage"] = "AI nie wykryło żadnych transakcji na przesłanym obrazie.";
                }

                return View("Index", scanResult.Transactions);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Błąd podczas analizy obrazu: {ex.Message}";
                return View("Index", new List<ScannedTransactionDto>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(List<ScannedTransactionDto> transactions)
        {
            if (transactions == null || !transactions.Any())
            {
                return RedirectToAction(nameof(Index));
            }

            Dictionary<int, Category> categories = await _context.Categories
                .ToDictionaryAsync(c => c.CategoryId);
            User? user = await _context.User.FirstOrDefaultAsync();
            int savedCount = 0;

            foreach (ScannedTransactionDto item in transactions)
            {
                if (item.Amount <= 0 || !categories.TryGetValue(item.CategoryId, out Category? category))
                {
                    continue;
                }

                var entity = new Transaction
                {
                    CategoryId = item.CategoryId,
                    Amount = item.Amount,
                    Date = item.Date == default ? DateTime.Today : item.Date,
                    Note = item.Note?.Trim() ?? string.Empty,
                    UserId = user?.Id ?? 1
                };

                _context.Transactions.Add(entity);
                savedCount++;

                if (user != null)
                {
                    user.WalletBalance += category.Type == "Income" ? item.Amount : -item.Amount;
                }
            }

            await _context.SaveChangesAsync();

            if (savedCount > 0)
            {
                await _notificationService.CreateNotification(
                    $"Dodano {savedCount} transakcji ze skanera AI.",
                    NotificationType.TransactionAdded);

                TempData["SuccessMessage"] = $"Zapisano pomyślnie {savedCount} transakcji i zaktualizowano saldo portfela.";
            }

            return RedirectToAction("Index", "Transaction");
        }
    }
}