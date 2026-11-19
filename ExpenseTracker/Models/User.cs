using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Imię jest wymagane")]
        [StringLength(50, ErrorMessage = "Imię nie może przekraczać 50 znaków")]
        [Column(TypeName = "nvarchar(50)")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nazwisko jest wymagane")]
        [StringLength(50, ErrorMessage = "Nazwisko nie może przekraczać 50 znaków")]
        [Column(TypeName = "nvarchar(50)")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nick jest wymagany")]
        [StringLength(50, ErrorMessage = "Nick nie może przekraczać 50 znaków")]
        [Column(TypeName = "nvarchar(50)")]
        public string Nickname { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyBudgetLimit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WalletBalance { get; set; }

        [Column(TypeName = "nvarchar(256)")]
        public string? PinHash { get; set; }

        public byte[]? ProfileImage { get; set; }

        [NotMapped]
        public string DisplayImage
        {
            get
            {
                if (ProfileImage != null && ProfileImage.Length > 0)
                {
                    string base64String = Convert.ToBase64String(ProfileImage);
                    return $"data:image/jpg;base64,{base64String}";
                }
                return string.Empty;
            }
        }
    }
}