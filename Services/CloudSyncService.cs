using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PersonalExpenseTracker.Data;
using PersonalExpenseTracker.Models;

namespace PersonalExpenseTracker.Services
{
    public class CloudSyncService
    {
        private readonly IAuthService _authService;
        private readonly ExpenseDbContext _localDb;

        // For now, this is our "cloud" – in a real implementation this would be Firebase / Azure
        // keyed by UserId
        private static readonly Dictionary<string, List<Expense>> _remoteStore = new();

        public CloudSyncService(IAuthService authService, ExpenseDbContext localDb)
        {
            _authService = authService;
            _localDb = localDb;
        }

        public async Task<bool> SyncExpensesAsync()
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
            {
                // Not logged in
                return false;
            }

            // Ensure remote list exists for this user
            if (!_remoteStore.TryGetValue(userId, out var remoteExpenses))
            {
                remoteExpenses = new List<Expense>();
                _remoteStore[userId] = remoteExpenses;
            }

            // Load all local expenses for this user (plus any without a UserId yet)
            var localExpenses = await _localDb.Expenses
                .Where(e => e.UserId == userId || e.UserId == null)
                .ToListAsync();

            // Assign UserId for any local rows missing it
            foreach (var e in localExpenses.Where(e => string.IsNullOrEmpty(e.UserId)))
            {
                e.UserId = userId;
            }

            // Mark LastModified for rows missing it
            foreach (var e in localExpenses.Where(e => e.LastModified == default))
            {
                e.LastModified = DateTime.UtcNow;
            }

            // 1. Push local unsynced changes to remote
            var unsyncedLocal = localExpenses.Where(e => !e.IsSynced).ToList();
            foreach (var local in unsyncedLocal)
            {
                if (string.IsNullOrEmpty(local.CloudId))
                {
                    // New local item, assign a CloudId and push to remote
                    local.CloudId = Guid.NewGuid().ToString();
                    var clone = CloneForRemote(local);
                    remoteExpenses.Add(clone);
                }
                else
                {
                    // Existing remote item, update it
                    var remote = remoteExpenses.FirstOrDefault(r => r.CloudId == local.CloudId);
                    if (remote == null)
                    {
                        // Remote lost it, re-add
                        remoteExpenses.Add(CloneForRemote(local));
                    }
                    else
                    {
                        // Conflict resolution (last write wins by LastModified)
                        if (local.LastModified >= remote.LastModified)
                        {
                            CopyFields(local, remote);
                        }
                    }
                }

                local.IsSynced = true;
            }

            // 2. Pull remote changes down to local
            foreach (var remote in remoteExpenses)
            {
                var local = localExpenses.FirstOrDefault(e => e.CloudId == remote.CloudId);
                if (local == null)
                {
                    // New remote item, insert locally
                    var newLocal = CloneForLocal(remote);
                    await _localDb.Expenses.AddAsync(newLocal);
                }
                else
                {
                    // Both exist, use last-write-wins
                    if (remote.LastModified > local.LastModified)
                    {
                        CopyFields(remote, local);
                        local.IsSynced = true;
                    }
                }
            }

            await _localDb.SaveChangesAsync();
            return true;
        }

        private static Expense CloneForRemote(Expense source)
        {
            return new Expense
            {
                CloudId = source.CloudId,
                Description = source.Description,
                Amount = source.Amount,
                Date = source.Date,
                Category = source.Category,
                LastModified = source.LastModified,
                IsSynced = true,
                UserId = source.UserId,
                ETag = source.ETag
            };
        }

        private static Expense CloneForLocal(Expense source)
        {
            return new Expense
            {
                CloudId = source.CloudId,
                Description = source.Description,
                Amount = source.Amount,
                Date = source.Date,
                Category = source.Category,
                LastModified = source.LastModified,
                IsSynced = true,
                UserId = source.UserId,
                ETag = source.ETag
            };
        }

        private static void CopyFields(Expense from, Expense to)
        {
            to.Description = from.Description;
            to.Amount = from.Amount;
            to.Date = from.Date;
            to.Category = from.Category;
            to.LastModified = from.LastModified;
            to.UserId = from.UserId;
            to.ETag = from.ETag;
            to.CloudId = from.CloudId;
        }
    }
}