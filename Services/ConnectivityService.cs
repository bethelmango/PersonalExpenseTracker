using System;
using System.Threading.Tasks;
using Microsoft.Maui.Networking;

namespace PersonalExpenseTracker.Services
{
    public class ConnectivityService
    {
        public event EventHandler<bool>? ConnectivityChanged;

        public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        public void StartMonitoring()
        {
            Connectivity.Current.ConnectivityChanged += (s, e) =>
            {
                ConnectivityChanged?.Invoke(this, IsConnected);

                if (IsConnected)
                {
                    // Trigger sync when connection restored
                    Task.Run(async () => await SyncPendingChangesAsync());
                }
            };
        }

        private async Task SyncPendingChangesAsync()
        {
            // TODO: hook this into CloudSyncService / AsyncQueue in later steps
            await Task.CompletedTask;
        }
    }
}