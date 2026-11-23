using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace ExpenseTracker.Models
{
    public class Transaction
    {
        [Key]
        public int TransactionId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Wybierz kategorię.")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Range(0.01, 100000000, ErrorMessage = "Kwota musi być większa od zera.")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        public string? Note { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public int? UserId { get; set; }
        public User? User { get; set; }

        [NotMapped]
        public string CategoryTitle => Category?.Title ?? "Brak kategorii";

        [NotMapped]
        public string CategoryIcon => Category?.Icon ?? "fa-solid fa-tag";

        [NotMapped]
        public string CategoryIconColorClass => Category?.Type == "Income" ? "text-success" : "text-danger";

        [NotMapped]
        public string FormattedAmount
        {
            get
            {
                string sign = Category != null && Category.Type == "Income" ? "+ " : "- ";
                return $"{sign}{Amount.ToString("N2", new System.Globalization.CultureInfo("pl-PL"))} zł";
            }
        }
    }
}