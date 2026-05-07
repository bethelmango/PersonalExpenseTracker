using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PersonalExpenseTracker.Data;
using PersonalExpenseTracker.Models;

namespace PersonalExpenseTracker.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        // Date range: last 12 months by default
        [ObservableProperty]
        private DateTime startDate = DateTime.Today.AddMonths(-12);

        [ObservableProperty]
        private DateTime endDate = DateTime.Today;

        // Summary
        [ObservableProperty]
        private decimal totalSpent;

        // Category summaries for pie chart + list
        [ObservableProperty]
        private ObservableCollection<CategorySummary> categorySummaries = new();

        // Monthly summaries for line chart
        [ObservableProperty]
        private ObservableCollection<MonthlySummary> monthlySummaries = new();

        public ReportsViewModel()
        {
            _ = ReloadAsync();
        }

        public Task ReloadAsync() => LoadReports();

        private async Task LoadReports()
        {
            await using var db = new ExpenseDbContext();
            await db.Database.EnsureCreatedAsync();

            // 1. Filtered expenses for the selected date range (inclusive)
            var from = StartDate.Date;
            var to = EndDate.Date.AddDays(1); // upper bound exclusive

            var filteredExpenses = await db.Expenses
                .AsNoTracking()
                .Where(e => e.Date >= from && e.Date < to)
                .ToListAsync();

            TotalSpent = filteredExpenses.Sum(e => e.Amount);

            // 1a. Category summaries
            var categoryTotals = filteredExpenses
                .GroupBy(e => e.Category)
                .Select(g => new CategorySummary
                {
                    Category = string.IsNullOrWhiteSpace(g.Key) ? "Uncategorized" : g.Key,
                    Total = g.Sum(e => e.Amount)
                })
                .OrderByDescending(c => c.Total)
                .ToList();

            // Calculate percentages
            var grandTotal = categoryTotals.Sum(c => c.Total);
            if (grandTotal > 0)
            {
                foreach (var c in categoryTotals)
                {
                    c.Percentage = (float)(c.Total / grandTotal * 100m);
                }
            }

            // Assign colors (optional)
            var palette = new[]
            {
                "#2196F3", "#4CAF50", "#FF9800", "#F44336", "#9C27B0",
                "#03A9F4", "#8BC34A", "#FFC107", "#E91E63", "#673AB7"
            };

            for (int i = 0; i < categoryTotals.Count; i++)
            {
                categoryTotals[i].Color = palette[i % palette.Length];
            }

            CategorySummaries = new ObservableCollection<CategorySummary>(categoryTotals);

            // 2. Monthly trend line: last 6 full months including current month
            var endMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var startMonth = endMonth.AddMonths(-5); // 6 months window

            var allRecentExpenses = await db.Expenses
                .AsNoTracking()
                .Where(e => e.Date >= startMonth && e.Date < endMonth.AddMonths(1))
                .ToListAsync();

            var monthlyGroups = allRecentExpenses
                .GroupBy(e => new { e.Date.Year, e.Date.Month })
                .Select(g => new MonthlySummary
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(e => e.Amount)
                })
                .ToDictionary(m => new DateTime(m.Year, m.Month, 1));

            var monthlyList = new List<MonthlySummary>();
            for (var dt = startMonth; dt <= endMonth; dt = dt.AddMonths(1))
            {
                if (monthlyGroups.TryGetValue(dt, out var existing))
                {
                    monthlyList.Add(existing);
                }
                else
                {
                    monthlyList.Add(new MonthlySummary
                    {
                        Year = dt.Year,
                        Month = dt.Month,
                        Total = 0m
                    });
                }
            }

            MonthlySummaries = new ObservableCollection<MonthlySummary>(monthlyList);
        }

        [RelayCommand]
        private async Task ApplyDateFilter()
        {
            await LoadReports();
        }
    }

    public class CategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public float Percentage { get; set; }
        public string Color { get; set; } = "#2196F3";
    }

    public class MonthlySummary
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Total { get; set; }

        public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    }
}