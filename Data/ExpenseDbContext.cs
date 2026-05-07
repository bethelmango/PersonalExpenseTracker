using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using PersonalExpenseTracker.Models;
using System.IO;

namespace PersonalExpenseTracker.Data
{
    public class ExpenseDbContext : DbContext
    {
        public DbSet<Expense> Expenses { get; set; } = null!;

        // This constructor is required for AddDbContext
        public ExpenseDbContext(DbContextOptions<ExpenseDbContext> options)
            : base(options)
        {
        }

        // Optional parameterless ctor for places where you new it up manually
        public ExpenseDbContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Only apply default configuration if not already configured via AddDbContext
            if (!optionsBuilder.IsConfigured)
            {
                string dbPath = Path.Combine(FileSystem.AppDataDirectory, "expenses.db");
                optionsBuilder.UseSqlite($"Filename={dbPath}");
            }
        }
    }
}