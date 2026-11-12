using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "First Name is required")]
        [StringLength(50, ErrorMessage = "First Name cannot be longer than 50 characters")]
        [Column(TypeName = "nvarchar(50)")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required")]
        [StringLength(50, ErrorMessage = "Last Name cannot be longer than 50 characters")]
        [Column(TypeName = "nvarchar(50)")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "NickName is required")]
        [StringLength(50, ErrorMessage = "Nick Name cannot be longer than 50 characters")]
        [Column(TypeName = "nvarchar(50)")]
        public string Nickname { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyBudgetLimit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WalletBalance { get; set; }

        public byte[]? ProfileImage { get; set; }

        [NotMapped]
        public string? Password { get; set; }

        [NotMapped]
        public string? NewPassword { get; set; }

        [NotMapped]
        public string? NicknameLogin { get; set; }

        [NotMapped]
        public string? PasswordLogin { get; set; }

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