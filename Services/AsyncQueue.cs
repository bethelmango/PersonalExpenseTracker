using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonalExpenseTracker.Services
{
    public class AsyncQueue
    {
        private readonly List<PendingOperation> _pendingOperations = new();

        public void QueueOperation(PendingOperation operation)
        {
            // Store operation for later sync
            _pendingOperations.Add(operation);
            SaveToLocalStorage();
        }

        public async Task ProcessQueueAsync()
        {
            // Process pending operations when online
            await Task.CompletedTask;
        }

        private void SaveToLocalStorage()
        {
            // TODO: Persist _pendingOperations to local storage (e.g., Preferences, file, or DB)
            // Just didn't need it/get around to it
        }
    }

    public class PendingOperation
    {
        public string? OperationType { get; set; } // e.g., "Create", "Update", "Delete"
        public int ExpenseId { get; set; }         // local Expense.Id
    }
}
