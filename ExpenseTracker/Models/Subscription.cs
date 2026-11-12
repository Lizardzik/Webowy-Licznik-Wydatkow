using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Models
{
    public class Subscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [Required(ErrorMessage = "Podaj nazwę subskrypcji (np. Netflix, Czynsz)")]
        [Column(TypeName = "nvarchar(75)")]
        public string Title { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Kwota musi być większa od 0")]
        public int Amount { get; set; }

        [Range(1, 31, ErrorMessage = "Wybierz dzień miesiąca od 1 do 31")]
        public int BillingDay { get; set; } = DateTime.Today.Day;

        [Range(1, int.MaxValue, ErrorMessage = "Wybierz kategorię")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastProcessedDate { get; set; }

        public DateTime NextBillingDate { get; set; } = DateTime.Today;

        [NotMapped]
        public string FormattedAmount => $"{Amount:N0} zł";

        [NotMapped]
        public string CategoryTitleWithIcon => Category != null ? $"{Category.Icon} {Category.Title}" : "";
    }
}