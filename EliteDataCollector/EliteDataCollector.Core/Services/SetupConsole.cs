using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Interactive console for first-time setup.
    ///
    /// Teaching: Console UI pattern for user interaction
    /// - Prompts user for authentication key
    /// - Validates key using local key validator
    /// - Asks about module preferences
    /// - Saves encrypted settings
    /// - Never exposes internal complexity (no mention of Supabase, encryption, etc.)
    ///
    /// User experience is clean and simple:
    /// 1. Enter authentication key
    /// 2. Validate locally
    /// 3. Enable/disable modules
    /// 4. Done - app starts
    /// </summary>
    public class SetupConsole
    {
        private readonly SettingsManager _settingsManager;
        private readonly KeyValidator _keyValidator;
        private readonly OutputWriter? _outputWriter;

        public SetupConsole(SettingsManager settingsManager, KeyValidator keyValidator, OutputWriter? outputWriter = null)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _keyValidator = keyValidator ?? throw new ArgumentNullException(nameof(keyValidator));
            _outputWriter = outputWriter;
        }

        /// <summary>
        /// Run first-time setup if needed.
        /// Returns settings object (either existing or newly created).
        /// </summary>
        public async Task<AppSettings> RunSetupIfNeededAsync()
        {
            // Load existing settings
            var settings = await _settingsManager.LoadAsync();

            // If setup already complete, just return settings
            if (settings.SetupComplete)
            {
                _outputWriter?.WriteLine($"[Setup] Settings already configured for {settings.CommanderName}");
                return settings;
            }

            // Run interactive setup
            _outputWriter?.WriteLine("");
            _outputWriter?.WriteLine("========================================");
            _outputWriter?.WriteLine("  Elite Data Collector - First Setup");
            _outputWriter?.WriteLine("========================================");
            _outputWriter?.WriteLine("");

            // Step 1: Get authentication key
            var key = PromptForKey();

            // Step 2: Validate key (throws exception if invalid)
            try
            {
                var (valid, commanderId, commanderName) = _keyValidator.ValidateKey(key);
                settings.InaraApiKeyEncrypted = key;  // Store the key
                settings.CommanderId = commanderId;
                settings.CommanderName = commanderName;
                settings.LastVerified = DateTime.UtcNow;

                _outputWriter?.WriteLine($"✓ Key validation successful: {commanderName}");
                _outputWriter?.WriteLine("");
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"✗ Error: {ex.Message}");
                _outputWriter?.WriteLine("Please check your key and try again.");
                throw;
            }

            // Step 3: Module preferences
            _outputWriter?.WriteLine("Configure modules:");
            settings.Modules.ColonizationEnabled = PromptYesNo("Enable ColonizationModule (track BGS and PowerPlay)?", true);
            settings.Modules.ExplorationEnabled = PromptYesNo("Enable ExplorationModule (exobiology alerts)?", false);

            // Step 4: Save settings
            settings.SetupComplete = true;
            await _settingsManager.SaveAsync(settings);

            _outputWriter?.WriteLine("");
            _outputWriter?.WriteLine("✓ Setup complete! Starting app...");
            _outputWriter?.WriteLine("");

            return settings;
        }

        /// <summary>
        /// Prompt user for authentication key.
        /// Key format: KEY-CMDR[9-digit-id]-[checksum]
        /// Example: KEY-CMDR000000123-ABC123EF
        /// </summary>
        private string PromptForKey()
        {
            _outputWriter?.WriteLine("Enter your authentication key:");
            _outputWriter?.WriteLine("(Format: KEY-CMDR[ID]-[CHECKSUM])");
            _outputWriter?.WriteLine("");

            // Read key from console
            Console.Write("> ");
            var key = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                _outputWriter?.WriteLine("ERROR: Key cannot be empty");
                throw new InvalidOperationException("Key required");
            }

            return key;
        }

        /// <summary>
        /// Prompt user for yes/no question with default.
        /// </summary>
        private bool PromptYesNo(string question, bool defaultValue)
        {
            var defaultText = defaultValue ? "(Y/n)" : "(y/N)";
            _outputWriter?.WriteLine($"{question} {defaultText}:");
            Console.Write("> ");

            var response = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(response))
                return defaultValue;

            return response == "y" || response == "yes";
        }
    }
}
