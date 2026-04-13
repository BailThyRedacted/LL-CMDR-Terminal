using System.Diagnostics;
using EliteDataCollector.Core.Services;

namespace EliteDataCollector.Launcher
{
    /// <summary>
    /// Elite Data Collector - Launcher
    ///
    /// Thin entry point that selects which interface to launch (Terminal or GUI).
    /// On first run (or with --reselect flag), prompts the user to choose.
    /// Persists the choice in AppSettings.InterfaceMode and launches the
    /// corresponding executable.
    ///
    /// Usage:
    ///   EliteDataCollector.Launcher.exe              - Launch saved interface mode
    ///   EliteDataCollector.Launcher.exe --reselect   - Re-prompt for interface choice
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;

                Console.WriteLine("");
                Console.WriteLine("========================================");
                Console.WriteLine("  Elite Data Collector - Launcher");
                Console.WriteLine("========================================");
                Console.WriteLine("");

                // Load settings (without OutputWriter - launcher is lightweight)
                var settingsManager = new SettingsManagerImpl();
                var settings = await settingsManager.LoadAsync();

                bool forceReselect = args.Length > 0 &&
                    (args[0].Equals("--reselect", StringComparison.OrdinalIgnoreCase) ||
                     args[0].Equals("-r", StringComparison.OrdinalIgnoreCase));

                // Determine interface mode
                string interfaceMode = settings.InterfaceMode;

                if (forceReselect || string.IsNullOrEmpty(interfaceMode))
                {
                    interfaceMode = PromptInterfaceSelection();

                    // Persist the choice
                    settings.InterfaceMode = interfaceMode;
                    await settingsManager.SaveAsync(settings);

                    Console.WriteLine("");
                    Console.WriteLine($"Interface mode saved: {interfaceMode}");
                    Console.WriteLine("(Run with --reselect to change later)");
                    Console.WriteLine("");
                }

                // Resolve executable paths relative to launcher directory
                var launcherDir = AppContext.BaseDirectory;
                string exeName;

                if (interfaceMode.Equals("gui", StringComparison.OrdinalIgnoreCase))
                {
                    exeName = "EliteDataCollector.UI.exe";
                    Console.WriteLine("Launching GUI interface...");
                }
                else
                {
                    exeName = "EliteDataCollector.Host.exe";
                    Console.WriteLine("Launching Terminal interface...");
                }

                var exePath = Path.Combine(launcherDir, exeName);

                // Check if the executable exists
                if (!File.Exists(exePath))
                {
                    // Try looking in sibling directories (dev environment)
                    var parentDir = Directory.GetParent(launcherDir)?.FullName;
                    if (parentDir != null)
                    {
                        var altPaths = new[]
                        {
                            Path.Combine(parentDir, exeName),
                            Path.Combine(parentDir, interfaceMode == "gui" ? "EliteDataCollector.UI" : "EliteDataCollector.Host", "bin", "Debug", "net8.0-windows", exeName),
                            Path.Combine(parentDir, interfaceMode == "gui" ? "EliteDataCollector.UI" : "EliteDataCollector.Host", "bin", "Release", "net8.0-windows", exeName),
                        };

                        foreach (var altPath in altPaths)
                        {
                            if (File.Exists(altPath))
                            {
                                exePath = altPath;
                                break;
                            }
                        }
                    }
                }

                if (!File.Exists(exePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: Could not find {exeName}");
                    Console.WriteLine($"Searched: {exePath}");
                    Console.WriteLine("");
                    Console.WriteLine("Make sure both EliteDataCollector.Host and EliteDataCollector.UI are built.");
                    Console.ResetColor();
                    Console.WriteLine("");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                // Launch the selected interface
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? launcherDir
                };

                Process.Start(startInfo);

                Console.WriteLine($"Started: {Path.GetFileName(exePath)}");
                Console.WriteLine("Launcher exiting.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"LAUNCHER ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Console.WriteLine("");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Prompt the user to select their preferred interface.
        /// </summary>
        private static string PromptInterfaceSelection()
        {
            Console.WriteLine("Select your preferred interface:");
            Console.WriteLine("");
            Console.WriteLine("  [1] Terminal  - Classic console interface");
            Console.WriteLine("  [2] GUI       - WinUI 3 graphical interface");
            Console.WriteLine("");

            while (true)
            {
                Console.Write("Enter choice (1 or 2): ");
                var input = Console.ReadLine()?.Trim();

                switch (input)
                {
                    case "1":
                    case "terminal":
                    case "Terminal":
                        return "terminal";
                    case "2":
                    case "gui":
                    case "GUI":
                        return "gui";
                    default:
                        Console.WriteLine("Invalid choice. Please enter 1 or 2.");
                        break;
                }
            }
        }
    }
}


