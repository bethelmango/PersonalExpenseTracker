using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalExpenseTracker.Data;
using PersonalExpenseTracker.Messages;
using PersonalExpenseTracker.Models;

namespace PersonalExpenseTracker.ViewModels
{
    public partial class AddExpensePageViewModel : ObservableObject
    {
        [ObservableProperty]
        private Expense expense = new() { Date = DateTime.Today };

        [ObservableProperty]
        private List<string> categories = new()
        {
            "Food",
            "Transport",
            "Entertainment",
            "Bills",
            "Shopping",
            "Other"
        };

        [RelayCommand]
        private async Task SaveExpense()
        {
            // Use the backing field 'expense' directly to avoid type/property confusion

            // Log what we're trying to save
            System.Diagnostics.Debug.WriteLine(
                $"[SaveExpense] Description='{expense.Description}', Amount={expense.Amount}");

            // Basic validation
            if (string.IsNullOrWhiteSpace(expense.Description) ||
                expense.Amount <= 0)
            {
                return;
            }

            // Ensure important fields are set
            if (expense.Date == default)
                expense.Date = DateTime.Today;

            expense.LastModified = DateTime.UtcNow;
            expense.IsSynced = false;

            await using var db = new ExpenseDbContext();
            await db.Database.EnsureCreatedAsync();
            db.Expenses.Add(expense);

            try
            {
                await db.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine("[SaveExpense] SaveChangesAsync OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SaveExpense][ERROR]");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                throw;
            }

            // Tell MainPageViewModel to refresh its list
            WeakReferenceMessenger.Default.Send(new RefreshExpensesMessage());

            // Go back
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}