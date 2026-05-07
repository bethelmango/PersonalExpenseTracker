using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel; // Permissions
using Microsoft.Maui.Storage;          // FilePicker
using PersonalExpenseTracker.Data;
using PersonalExpenseTracker.Messages;
using PersonalExpenseTracker.Models;
using PersonalExpenseTracker.ViewModels; // for AddExpensePageViewModel categories

namespace PersonalExpenseTracker.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Expense> expenses = new();

        // Indicates when a long-running operation (like export/import) is in progress.
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private enum DuplicateHandlingMode
        {
            Skip,
            Update,
            CreateNew
        }

        public MainPageViewModel()
        {
            _ = LoadExpenses();

            WeakReferenceMessenger.Default.Register<RefreshExpensesMessage>(this, async (r, m) =>
            {
                await LoadExpenses();
            });
        }

        [RelayCommand]
        private async Task LoadExpenses()
        {
            await using var db = new ExpenseDbContext();
            await db.Database.EnsureCreatedAsync();

            var expenseList = await db.Expenses.ToListAsync();
            Expenses = new ObservableCollection<Expense>(expenseList);
        }

        [RelayCommand]
        private async Task AddNewExpense()
        {
            await Shell.Current.GoToAsync(nameof(AddExpensePage));
        }

        // Part 1: CSV Export

        [RelayCommand]
        private async Task ExportExpenses()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                var hasPermission = await EnsureStoragePermissionsAsync();
                if (!hasPermission)
                {
                    await Shell.Current.DisplayAlert(
                        "Permission denied",
                        "Storage permission is required to export expenses.",
                        "OK");
                    return;
                }

                string csvContent;
                await using (var db = new ExpenseDbContext())
                {
                    await db.Database.EnsureCreatedAsync();
                    var allExpenses = await db.Expenses.ToListAsync();

                    csvContent = GenerateExpensesCsv(allExpenses);
                }

                var preview = string.Join("\n", csvContent.Split('\n').Take(5));
                await Shell.Current.DisplayAlert("Export preview", preview, "OK");


                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var downloadsDir = Path.Combine(userProfile, "Downloads");

                if (!Directory.Exists(downloadsDir))
                {
                    Directory.CreateDirectory(downloadsDir);
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"expenses_{timestamp}.csv";
                var fullPath = Path.Combine(downloadsDir, fileName);

                // DEBUG: log where we're writing
                System.Diagnostics.Debug.WriteLine($"[ExportExpenses] Writing CSV to: {fullPath}");

                await File.WriteAllTextAsync(fullPath, csvContent, Encoding.UTF8);

                System.Diagnostics.Debug.WriteLine($"[ExportExpenses] Export succeeded.");

                await Shell.Current.DisplayAlert(
                    "Export complete",
                    $"Expenses exported to:\n{fullPath}",
                    "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExportExpenses] ERROR: {ex}");
                await Shell.Current.DisplayAlert(
                    "Export failed",
                    $"An error occurred while exporting expenses:\n{ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // CSV Import (with duplicate handling + transaction)

        [RelayCommand]
        private async Task ImportExpenses()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                // Pick CSV file
                var pickOptions = new PickOptions
                {
                    PickerTitle = "Select expenses CSV file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android,     new[] { "text/csv", "text/comma-separated-values", "application/csv" } },
                        { DevicePlatform.iOS,         new[] { "public.comma-separated-values-text" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } },
                        { DevicePlatform.WinUI,       new[] { ".csv" } },
                    })
                };

                var result = await FilePicker.Default.PickAsync(pickOptions);
                if (result == null)
                    return; // user cancelled

                var filePath = result.FullPath;
                var csvContent = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // Categories for validation
                var validCategories = GetValidCategories();

                // Parse + validate
                var (parsedExpenses, parseErrors) = ParseExpensesCsv(csvContent, validCategories);

                if (parsedExpenses.Count == 0 && parseErrors.Count > 0)
                {
                    var msg = new StringBuilder();
                    msg.AppendLine("No valid rows found in CSV.");
                    msg.AppendLine($"Skipped {parseErrors.Count} row(s) due to errors.");
                    foreach (var err in parseErrors.Take(5))
                        msg.AppendLine($"Row {err.RowNumber}: {err.ErrorMessage}");

                    await Shell.Current.DisplayAlert("Import", msg.ToString(), "OK");
                    return;
                }

                // Duplicate handling choice
                var mode = await AskDuplicateHandlingModeAsync();
                if (mode == null)
                    return; // user cancelled

                using var db = new ExpenseDbContext();
                await db.Database.EnsureCreatedAsync();

                // Pre-load existing data
                var existingExpenses = await db.Expenses.ToListAsync();
                var (newItems, duplicateItems) = ClassifyDuplicates(parsedExpenses, existingExpenses);

                // --- Transaction Support: atomic import ---
                using (var transaction = await db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        switch (mode.Value)
                        {
                            case DuplicateHandlingMode.Skip:
                                // Insert only non-duplicates
                                if (newItems.Count > 0)
                                {
                                    await db.Expenses.AddRangeAsync(newItems);
                                }
                                break;

                            case DuplicateHandlingMode.Update:
                                // Update existing duplicates, insert new
                                foreach (var (parsed, existing) in duplicateItems)
                                {
                                    existing.Description = parsed.Description;
                                    existing.Amount = parsed.Amount;
                                    existing.Date = parsed.Date;
                                    existing.Category = parsed.Category;
                                }

                                if (newItems.Count > 0)
                                {
                                    await db.Expenses.AddRangeAsync(newItems);
                                }
                                break;

                            case DuplicateHandlingMode.CreateNew:
                                // Insert everything as new rows (ignore Ids)
                                await db.Expenses.AddRangeAsync(parsedExpenses);
                                break;
                        }

                        var affected = await db.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Summary including DB changes
                        var summary = new StringBuilder();
                        summary.AppendLine($"Valid rows parsed: {parsedExpenses.Count}");
                        summary.AppendLine($"Inserted/updated rows in database: {affected}");
                        summary.AppendLine($"New items (no duplicates): {newItems.Count}");
                        summary.AppendLine($"Potential duplicates: {duplicateItems.Count}");
                        summary.AppendLine();
                        summary.AppendLine("Duplicate strategy used:");
                        summary.AppendLine(mode.Value switch
                        {
                            DuplicateHandlingMode.Skip => "Skip duplicates (import only new rows).",
                            DuplicateHandlingMode.Update => "Update existing duplicates.",
                            DuplicateHandlingMode.CreateNew => "Create new entries even for duplicates.",
                            _ => "Unknown"
                        });

                        if (parseErrors.Count > 0)
                        {
                            summary.AppendLine();
                            summary.AppendLine($"Rows with validation errors: {parseErrors.Count}");
                            foreach (var err in parseErrors.Take(5))
                                summary.AppendLine($"Row {err.RowNumber}: {err.ErrorMessage}");
                        }

                        await Shell.Current.DisplayAlert(
                            "Import complete",
                            summary.ToString(),
                            "OK");

                        // Refresh UI list
                        await LoadExpenses();
                    }
                    catch (Exception ex)
                    {
                        // Roll back all DB changes if anything fails
                        await transaction.RollbackAsync();

                        await Shell.Current.DisplayAlert(
                            "Import failed",
                            $"An error occurred while saving imported expenses:\n{ex.Message}",
                            "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(
                    "Import failed",
                    $"An error occurred while importing expenses:\n{ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ----- Duplicate handling helpers -----

        private static async Task<DuplicateHandlingMode?> AskDuplicateHandlingModeAsync()
        {
            var action = await Shell.Current.DisplayActionSheet(
                "How should duplicates be handled?",
                "Cancel",
                null,
                "Skip duplicates",
                "Update existing",
                "Create new entries");

            return action switch
            {
                "Skip duplicates"    => DuplicateHandlingMode.Skip,
                "Update existing"    => DuplicateHandlingMode.Update,
                "Create new entries" => DuplicateHandlingMode.CreateNew,
                "Cancel"             => (DuplicateHandlingMode?)null,
                null                 => (DuplicateHandlingMode?)null,
                _                    => (DuplicateHandlingMode?)null
            };
        }

        /// <summary>
        /// Classify parsed expenses into "new" and "duplicates" based on a composite key:
        /// Description + Amount + Date(Date part only) + Category.
        /// </summary>
        private static (List<Expense> NewItems, List<(Expense Parsed, Expense Existing)> Duplicates)
            ClassifyDuplicates(IEnumerable<Expense> imported, IEnumerable<Expense> existing)
        {
            static string MakeKey(Expense e) =>
                $"{(e.Description ?? string.Empty).Trim().ToLowerInvariant()}|" +
                $"{e.Amount.ToString(CultureInfo.InvariantCulture)}|" +
                $"{e.Date.Date:yyyy-MM-dd}|" +
                $"{(e.Category ?? string.Empty).Trim().ToLowerInvariant()}";

            // Build a lookup that tolerates duplicates in the existing data:
            // keep the first seen Expense for each key, ignore additional ones.
            var existingMap = new Dictionary<string, Expense>(StringComparer.Ordinal);
            foreach (var e in existing)
            {
                var key = MakeKey(e);
                if (!existingMap.ContainsKey(key))
                {
                    existingMap[key] = e;
                }
            }

            var newItems = new List<Expense>();
            var duplicates = new List<(Expense Parsed, Expense Existing)>();

            foreach (var imp in imported)
            {
                var key = MakeKey(imp);
                if (existingMap.TryGetValue(key, out var existingExpense))
                {
                    duplicates.Add((imp, existingExpense));
                }
                else
                {
                    newItems.Add(imp);
                }
            }

            return (newItems, duplicates);
        }

        // ----- Shared helpers -----

        private static async Task<bool> EnsureStoragePermissionsAsync()
        {
#if ANDROID
            var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (readStatus != PermissionStatus.Granted)
            {
                readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (writeStatus != PermissionStatus.Granted)
            {
                writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }

            return readStatus == PermissionStatus.Granted &&
                   writeStatus == PermissionStatus.Granted;
#elif IOS || MACCATALYST
            return true;
#else
            return true;
#endif
        }

        private static string GenerateExpensesCsv(IEnumerable<Expense> expenses)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Description,Amount,Date,Category");

            foreach (var e in expenses)
            {
                var id = e.Id.ToString();
                var description = CsvEscape(e.Description ?? string.Empty);
                var amount = e.Amount.ToString(CultureInfo.InvariantCulture);
                var date = e.Date.ToString("o");
                var category = CsvEscape(e.Category ?? string.Empty);

                sb.Append(id).Append(',')
                  .Append(description).Append(',')
                  .Append(amount).Append(',')
                  .Append(date).Append(',')
                  .Append(category)
                  .AppendLine();
            }

            return sb.ToString();
        }

        private static string CsvEscape(string value)
        {
            var needsQuotes = value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r');

            if (value.Contains('"'))
                value = value.Replace("\"", "\"\"");

            return needsQuotes ? $"\"{value}\"" : value;
        }

        // ----- CSV parsing + validation helpers for Import -----

        private sealed class CsvParseError
        {
            public int RowNumber { get; init; }
            public string ErrorMessage { get; init; } = string.Empty;
        }

        private static HashSet<string> GetValidCategories()
        {
            var vm = new AddExpensePageViewModel();
            return vm.Categories
                     .Select(c => c.Trim())
                     .Where(c => !string.IsNullOrWhiteSpace(c))
                     .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static (List<Expense> ValidExpenses, List<CsvParseError> Errors) ParseExpensesCsv(
            string csv,
            HashSet<string> validCategories)
        {
            var validExpenses = new List<Expense>();
            var errors = new List<CsvParseError>();

            if (string.IsNullOrWhiteSpace(csv))
                return (validExpenses, errors);

            using var reader = new StringReader(csv);
            string? line;
            int rowIndex = 0;

            // Header
            line = reader.ReadLine();
            rowIndex++;
            if (line == null)
                return (validExpenses, errors);

            while ((line = reader.ReadLine()) != null)
            {
                rowIndex++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var fields = ParseCsvLine(line);

                    if (fields.Count < 5)
                    {
                        errors.Add(new CsvParseError
                        {
                            RowNumber = rowIndex,
                            ErrorMessage = "Not enough columns."
                        });
                        continue;
                    }

                    var description = fields[1];
                    var amountText = fields[2];
                    var dateText = fields[3];
                    var category = fields[4];

                    if (!decimal.TryParse(
                            amountText,
                            NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                            CultureInfo.InvariantCulture,
                            out var amount))
                    {
                        errors.Add(new CsvParseError
                        {
                            RowNumber = rowIndex,
                            ErrorMessage = "Invalid amount."
                        });
                        continue;
                    }

                    var date = ParseDateFlexible(dateText);
                    if (date == null)
                    {
                        errors.Add(new CsvParseError
                        {
                            RowNumber = rowIndex,
                            ErrorMessage = "Invalid date format."
                        });
                        continue;
                    }

                    if (!validCategories.Contains(category?.Trim() ?? string.Empty))
                    {
                        errors.Add(new CsvParseError
                        {
                            RowNumber = rowIndex,
                            ErrorMessage = $"Invalid category '{category}'."
                        });
                        continue;
                    }

                    var expense = new Expense
                    {
                        Description = description,
                        Amount = amount,
                        Date = date.Value,
                        Category = category
                    };

                    validExpenses.Add(expense);
                }
                catch (Exception ex)
                {
                    errors.Add(new CsvParseError
                    {
                        RowNumber = rowIndex,
                        ErrorMessage = $"Unexpected parse error: {ex.Message}"
                    });
                }
            }

            return (validExpenses, errors);
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line))
            {
                result.Add(string.Empty);
                return result;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }

        private static DateTime? ParseDateFlexible(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (DateTime.TryParseExact(
                    text,
                    "o",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var isoDate))
            {
                return isoDate;
            }

            var formats = new[]
            {
                "MM/dd/yyyy",
                "M/d/yyyy",
                "yyyy-MM-dd",
                "dd/MM/yyyy",
                "d/M/yyyy"
            };

            if (DateTime.TryParseExact(
                    text,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var generic))
            {
                return generic;
            }

            return null;
        }
    }
}
