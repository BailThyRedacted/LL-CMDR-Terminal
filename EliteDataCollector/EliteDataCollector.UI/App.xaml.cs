using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EliteDataCollector.Core;
using EliteDataCollector.Core.Services;
using EliteDataCollector.UI.Services;
using EliteDataCollector.UI.ViewModels;
using System.IO;

namespace EliteDataCollector.UI
{
    public partial class App : Application
    {
        public static ServiceProvider? ServiceProvider { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                // Initialize services
                await InitializeServicesAsync();

                // Create and show main window
                var window = new MainWindow();
                window.Activate();
            }
            catch (Exception ex)
            {
                // Show error dialog
                var dialog = new ContentDialog
                {
                    Title = "Initialization Error",
                    Content = $"Failed to initialize application:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    CloseButtonText = "Exit"
                };
                
                // Show dialog and exit
                _ = dialog.ShowAsync();
                this.Exit();
            }
        }

        private async Task InitializeServicesAsync()
        {
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(System.AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            // Set up dependency injection
            var services = new ServiceCollection();
            services.AddSingleton(configuration);

            // Output Writer (console + file)
            services.AddSingleton<OutputWriter>(sp =>
            {
                var consoleWriter = new ConsoleOutputWriter();
                var fileWriter = new FileOutputWriter();
                consoleWriter.SetMinimumLogLevel(LogLevel.Info);
                return new CompositeOutputWriter(consoleWriter, fileWriter);
            });

            // Core services
            services.AddSingleton<SettingsManager, SettingsManagerImpl>();
            services.AddSingleton<KeyValidator, KeyValidatorImpl>();
            services.AddSingleton<SupabaseClient>(sp =>
                new SupabaseClientImpl(
                    configuration,
                    sp.GetService<SettingsManager>(),
                    sp.GetRequiredService<OutputWriter>()));
            services.AddSingleton<SetupConsole>();

            // Game monitoring services
            services.AddSingleton<GameProcessMonitor>(sp =>
                new GameProcessMonitorImpl(sp.GetRequiredService<OutputWriter>(), pollIntervalMs: 5000));

            services.AddSingleton<JournalMonitor>(sp =>
                new JournalMonitorImpl(sp.GetRequiredService<OutputWriter>()));

            // Auto-update services
            services.AddHttpClient();
            services.AddSingleton<UpdateService>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var gitHubRepo = configuration["UpdateCheck:GitHubRepository"] ?? "BailThyRedacted/EliteDataCollector";
                var currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "1.0.0";
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

            // UI-specific services
            services.AddSingleton<DashboardSettingsService>();
            services.AddSingleton<ContuberniumService>();
            services.AddSingleton<JournalDataService>();

            // ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<ColonizationViewModel>();
            services.AddSingleton<BgsViewModel>();
            services.AddSingleton<PowerplayViewModel>();
            services.AddSingleton<ContuberniumViewModel>();
            services.AddSingleton<NavigationViewModel>();

            ServiceProvider = services.BuildServiceProvider();

            // Run setup if needed
            var setupConsole = ServiceProvider.GetRequiredService<SetupConsole>();
            var settings = await setupConsole.RunSetupIfNeededAsync();

            // Initialize MainCore
            var gameMonitor = ServiceProvider.GetRequiredService<GameProcessMonitor>();
            var journalMonitor = ServiceProvider.GetRequiredService<JournalMonitor>();
            var updateService = ServiceProvider.GetRequiredService<UpdateService>();
            var updateDownloader = ServiceProvider.GetRequiredService<UpdateDownloader>();
            var idleDetector = ServiceProvider.GetRequiredService<IdleDetector>();
            var outputWriter = ServiceProvider.GetRequiredService<OutputWriter>();

            var mainCore = new MainCore(
                gameMonitor,
                journalMonitor,
                outputWriter: outputWriter,
                updateService: updateService,
                updateDownloader: updateDownloader,
                idleDetector: idleDetector);
            mainCore.SetCommanderContext(1, settings.CommanderName);
            mainCore.SetModulePreferences(settings.Modules);

            // Initialize and register modules
            var modules = new List<GameLoopModule>();

            if (settings.Modules.ColonizationEnabled)
            {
                var m = new ColonizationModule.ColonizationModule();
                await m.InitializeAsync(ServiceProvider);
                modules.Add(m);
            }

            if (settings.Modules.ExplorationEnabled)
            {
                var m = new ExplorationModule.ExplorationModule();
                await m.InitializeAsync(ServiceProvider);
                modules.Add(m);
            }

            if (settings.Modules.PowerplayEnabled)
            {
                var m = new PowerplayModule.PowerplayModule();
                await m.InitializeAsync(ServiceProvider);
                modules.Add(m);
            }

            mainCore.RegisterModules(modules);
            await mainCore.InitializeAsync();

            // Store MainCore in app context for ViewModels to access
            AppContext.MainCore = mainCore;
        }
    }
}

