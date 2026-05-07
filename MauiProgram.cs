using System;
using System.IO;
using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using PersonalExpenseTracker.Data;
using PersonalExpenseTracker.Services;
using Syncfusion.Maui.Core.Hosting;

namespace PersonalExpenseTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            EnsureDatabaseFolderExists();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register DbContext
            builder.Services.AddDbContext<ExpenseDbContext>(options =>
            {
                string dbPath = Path.Combine(FileSystem.AppDataDirectory, "expenses.db");
                options.UseSqlite($"Filename={dbPath}");
            });

            // Auth + sync services
            builder.Services.AddSingleton<IAuthService, DummyAuthService>();
            builder.Services.AddScoped<CloudSyncService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Ensure DB is created on startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExpenseDbContext>();
                db.Database.EnsureCreated();
            }

            return app;
        }

        private static void EnsureDatabaseFolderExists()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "expenses.db");
            string? directory = Path.GetDirectoryName(dbPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}