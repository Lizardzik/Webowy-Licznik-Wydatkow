namespace ExpenseTracker.Models
{
    public class ScannedTransactionDto
    {
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public string Note { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense";
    }
}