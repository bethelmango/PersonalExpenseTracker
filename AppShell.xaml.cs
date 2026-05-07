using System;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using PersonalExpenseTracker.Services;
using PersonalExpenseTracker.Views;

namespace PersonalExpenseTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes used by Shell navigation
            Routing.RegisterRoute(nameof(AddExpensePage), typeof(AddExpensePage));
        }

        private async void OnSyncClicked(object sender, EventArgs e)
        {
            try
            {
                var syncService = Current?.Handler?.MauiContext?.Services.GetService<CloudSyncService>();
                if (syncService is null)
                {
                    await DisplayAlert("Sync", "Sync service not available.", "OK");
                    return;
                }

                var ok = await syncService.SyncExpensesAsync();
                if (!ok)
                {
                    await DisplayAlert("Sync", "You must be logged in to sync.", "OK");
                    return;
                }

                await DisplayAlert("Sync", "Sync completed.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Sync][ERROR] " + ex);
                await DisplayAlert("Sync", "Sync failed. Check debug output.", "OK");
            }
        }
    }
}