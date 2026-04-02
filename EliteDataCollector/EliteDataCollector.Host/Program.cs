using System;
using System.IO;
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
    /// Handles local key authentication and module configuration
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

                // TEACHING NOTE - Composite Logging:
                // We now use CompositeOutputWriter to log to BOTH console AND file.
                // This gives us persistent logs + real-time console feedback.
                //
                // Why this is good for debugging:
                // - See important events in real-time on console
                // - Review detailed history in %APPDATA%\EliteDangerousDataCollector\debug.log
                // - If app crashes, logs are safely on disk
                // - Can change log detail level (Debug, Info, Warning) at runtime
                services.AddSingleton<OutputWriter>(sp =>
                {
                    var consoleWriter = new ConsoleOutputWriter();
                    var fileWriter = new FileOutputWriter();

                    // By default, show Info level and above (hide Debug messages)
                    // Users can modify console log level later if needed
                    consoleWriter.SetMinimumLogLevel(LogLevel.Info);

                    return new CompositeOutputWriter(consoleWriter, fileWriter);
                });

                services.AddSingleton<SettingsManager, SettingsManagerImpl>();
                services.AddSingleton<KeyValidator, KeyValidatorImpl>();
                services.AddSingleton<SupabaseClient>(sp =>
                    new SupabaseClientImpl(configuration, sp.GetRequiredService<OutputWriter>()));
                services.AddSingleton<SetupConsole>();

                // TEACHING NOTE - Game Detection Services:
                // These are the actual implementations of the monitoring interfaces.
                // They are registered as singletons so they persist for the app lifetime.
                services.AddSingleton<GameProcessMonitor>(sp =>
                    new GameProcessMonitorImpl(sp.GetRequiredService<OutputWriter>(), pollIntervalMs: 5000));

                services.AddSingleton<JournalMonitor>(sp =>
                    new JournalMonitorImpl(sp.GetRequiredService<OutputWriter>()));

                // TEACHING NOTE - Auto-Update Services:
                // These services handle checking for, downloading, and installing updates from GitHub.
                services.AddHttpClient();
                services.AddSingleton<UpdateService>(sp =>
                {
                    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                    var gitHubRepo = configuration["UpdateCheck:GitHubRepository"] ?? "your-username/EliteDataCollector";
                    var currentVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
                    return new UpdateServiceImpl(httpClient, sp.GetRequiredService<OutputWriter>(), gitHubRepo, currentVersion);
                });

                services.AddSingleton<UpdateDownloader>(sp =>
                {
                    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                    var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EliteDangerousDataCollector");
                    return new UpdateDownloaderImpl(httpClient, sp.GetRequiredService<OutputWriter>(), appDataPath, maxBackupRetention: 3);
                });

                services.AddSingleton<IdleDetector>(sp =>
                    new IdleDetectorImpl(sp.GetRequiredService<GameProcessMonitor>(), sp.GetRequiredService<OutputWriter>()));


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

                // ===== INITIALIZE MAINCORE =====
                var gameMonitor = serviceProvider.GetRequiredService<GameProcessMonitor>();
                var journalMonitor = serviceProvider.GetRequiredService<JournalMonitor>();
                var updateService = serviceProvider.GetRequiredService<UpdateService>();
                var updateDownloader = serviceProvider.GetRequiredService<UpdateDownloader>();
                var idleDetector = serviceProvider.GetRequiredService<IdleDetector>();

                var mainCore = new MainCore(
                    gameMonitor,
                    journalMonitor,
                    outputWriter: outputWriter,
                    updateService: updateService,
                    updateDownloader: updateDownloader,
                    idleDetector: idleDetector);
                mainCore.SetCommanderContext(1, settings.CommanderName);
                mainCore.SetModulePreferences(settings.Modules);

                await mainCore.InitializeAsync();

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

                // ===== SHUTDOWN =====
                outputWriter.WriteLine("");
                outputWriter.WriteLine("Shutting down MainCore...");
                await mainCore.ShutdownAsync();

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

