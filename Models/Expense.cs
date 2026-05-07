using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalExpenseTracker.Models
{
    [Table("Expenses")]
    public class Expense
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime Date { get; set; }

        public string Category { get; set; } = string.Empty;

        // New sync properties
        public string? CloudId { get; set; } // ID in cloud database
        public DateTime LastModified { get; set; }
        public bool IsSynced { get; set; }
        public string? UserId { get; set; } // For multi-user support
        public string? ETag { get; set; } // For conflict detection
    }
}
