using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace ExpenseTracker.Models
{
    public class Subscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [Required(ErrorMessage = "Podaj nazwę subskrypcji (np. Netflix, Czynsz)")]
        [Column(TypeName = "nvarchar(75)")]
        public string Title { get; set; } = string.Empty;

        [Range(0.01, 100000000, ErrorMessage = "Kwota musi być większa od 0")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Range(1, 31, ErrorMessage = "Wybierz dzień miesiąca od 1 do 31")]
        public int BillingDay { get; set; } = DateTime.Today.Day;

        [Range(1, int.MaxValue, ErrorMessage = "Wybierz kategorię")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastProcessedDate { get; set; }

        public DateTime NextBillingDate { get; set; } = DateTime.Today;

        [NotMapped]
        public string FormattedAmount
        {
            get
            {
                CultureInfo culture = CultureInfo.CreateSpecificCulture("pl-PL");
                return $"{Amount.ToString("N2", culture)} zł";
            }
        }

        [NotMapped]
        public string CategoryTitleWithIcon => Category != null ? $"{Category.Icon} {Category.Title}" : string.Empty;
    }
}