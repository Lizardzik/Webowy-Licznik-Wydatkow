namespace ExpenseTracker.Models
{
    public class ScanResultDto
    {
        public List<ScannedTransactionDto> Transactions { get; set; } = new();
        public string UsedModel { get; set; } = string.Empty;
    }
}