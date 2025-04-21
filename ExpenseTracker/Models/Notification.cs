using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseTracker.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        [Required]
        public NotificationType Type { get; set; }

        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        // Dla powiadomień o transakcjach
        public int? TransactionId { get; set; }

        [ForeignKey("TransactionId")]
        public Transaction Transaction { get; set; }
    }

    public enum NotificationType
    {
        TransactionAdded = 1,
        WeeklyReport = 2
    }
}