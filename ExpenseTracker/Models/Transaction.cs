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
        public string CategoryTitleWithIcon
        {
            get
            {
                return Category == null ? string.Empty : Category.Icon + " " + Category.Title;
            }
        }

        [NotMapped]
        public string FormattedAmount
        {
            get
            {
                CultureInfo culture = CultureInfo.CreateSpecificCulture("pl-PL");
                culture.NumberFormat.CurrencyNegativePattern = 1;
                string sign = (Category == null || Category.Type == "Expense") ? "- " : "+ ";
                return sign + Amount.ToString("C2", culture);
            }
        }
    }
}