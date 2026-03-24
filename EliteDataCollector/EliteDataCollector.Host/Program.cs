using System;
using System.Threading.Tasks;
using EliteDataCollector.Core;
using EliteDataCollector.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EliteDataCollector.Host
{
    /// <summary>
    /// Elite Data Collector - Main Entry Point
    ///
    /// Handles INARA authentication and module configuration
    /// before starting the main application.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;

                // Load configuration
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .Build();

                // Set up dependency injection
                var services = new ServiceCollection();
                services.AddSingleton(configuration);
                services.AddSingleton<OutputWriter, ConsoleOutputWriter>();
                services.AddSingleton<SettingsManager, SettingsManagerImpl>();
                services.AddHttpClient<InaraAuth, InaraAuthImpl>();
                services.AddSingleton<SupabaseClient>(sp =>
                    new SupabaseClientImpl(configuration, sp.GetRequiredService<OutputWriter>()));
                services.AddSingleton<SetupConsole>();

                var serviceProvider = services.BuildServiceProvider();

                var outputWriter = serviceProvider.GetRequiredService<OutputWriter>();
                var setupConsole = serviceProvider.GetRequiredService<SetupConsole>();

                // ===== FIRST-TIME SETUP =====
                var settings = await setupConsole.RunSetupIfNeededAsync();

                outputWriter.WriteLine("");
                outputWriter.WriteLine("========================================");
                outputWriter.WriteLine("  Elite Data Collector - Starting");
                outputWriter.WriteLine("========================================");
                outputWriter.WriteLine("");
                outputWriter.WriteLine($"Commander: {settings.CommanderName}");
                outputWriter.WriteLine($"ColonizationModule: {(settings.Modules.ColonizationEnabled ? "ENABLED" : "disabled")}");
                outputWriter.WriteLine($"ExplorationModule: {(settings.Modules.ExplorationEnabled ? "ENABLED" : "disabled")}");
                outputWriter.WriteLine("");
                outputWriter.WriteLine("Waiting for Elite Dangerous to launch...");
                outputWriter.WriteLine("(Press Ctrl+C to exit)");
                outputWriter.WriteLine("");

                // ===== MAIN LOOP =====
                var exitEvent = new System.Threading.ManualResetEvent(false);
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    exitEvent.Set();
                };

                exitEvent.WaitOne();

                outputWriter.WriteLine("");
                outputWriter.WriteLine("Shutting down...");
                outputWriter.WriteLine("Goodbye!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}

