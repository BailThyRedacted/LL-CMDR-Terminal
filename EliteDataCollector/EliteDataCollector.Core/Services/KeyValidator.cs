using System;
using System.Security.Cryptography;
using System.Text;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Local key validation service.
    /// Validates entered keys using a calculated checksum algorithm.
    /// 
    /// Teaching: Local validation pattern
    /// - No external API calls required
    /// - Validates key format and structure
    /// - Calculates expected checksum and compares with provided key
    /// - Secure validation without network dependency
    /// </summary>
    public interface KeyValidator
    {
        /// <summary>
        /// Validates the entered key.
        /// Calculates the key's expected value and compares with the provided key.
        /// Returns (valid, commanderId, commanderName) if valid.
        /// Throws InvalidOperationException if key is invalid.
        /// </summary>
        (bool valid, int commanderId, string commanderName) ValidateKey(string key);
    }

    /// <summary>
    /// Default key validator implementation.
    /// Uses a checksum algorithm to validate keys.
    /// </summary>
    public class KeyValidatorImpl : KeyValidator
    {
        private readonly OutputWriter? _outputWriter;

        // Key validation constants
        // Key format: KEY-CMDR[9digits]-[8hexchars] = 3+1+13+1+8 = 26 chars
        private const int MINIMUM_KEY_LENGTH = 26;
        private const int MAXIMUM_KEY_LENGTH = 64;

        public KeyValidatorImpl(OutputWriter? outputWriter = null)
        {
            _outputWriter = outputWriter;
        }

        /// <summary>
        /// Validates the entered key by calculating its checksum.
        /// Returns (valid=true, commanderId, commanderName) if valid.
        /// Throws InvalidOperationException if key is invalid.
        /// </summary>
        public (bool valid, int commanderId, string commanderName) ValidateKey(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    _outputWriter?.WriteLine("[KeyValidator] Key cannot be empty");
                    throw new InvalidOperationException("Key cannot be empty");
                }

                _outputWriter?.WriteLine("[KeyValidator] Validating key...");

                // Check key length
                if (key.Length < MINIMUM_KEY_LENGTH || key.Length > MAXIMUM_KEY_LENGTH)
                {
                    _outputWriter?.WriteLine($"[KeyValidator] Key length invalid (must be between {MINIMUM_KEY_LENGTH} and {MAXIMUM_KEY_LENGTH} characters)");
                    throw new InvalidOperationException($"Key must be between {MINIMUM_KEY_LENGTH} and {MAXIMUM_KEY_LENGTH} characters");
                }

                // Check if key is valid hexadecimal or alphanumeric
                if (!IsValidKeyFormat(key))
                {
                    _outputWriter?.WriteLine("[KeyValidator] Key format invalid (must be alphanumeric or hexadecimal)");
                    throw new InvalidOperationException("Key format invalid (must be alphanumeric or hexadecimal)");
                }

                // Calculate and validate checksum
                var (isValid, commanderId, commanderName) = CalculateAndValidateKey(key);

                if (!isValid)
                {
                    _outputWriter?.WriteLine("[KeyValidator] Key checksum validation failed");
                    throw new InvalidOperationException("Key validation failed - invalid checksum");
                }

                _outputWriter?.WriteLine($"[KeyValidator] ✓ Key validation successful: {commanderName} (ID: {commanderId})");

                return (true, commanderId, commanderName);
            }
            catch (InvalidOperationException)
            {
                throw;  // Re-throw validation errors
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[KeyValidator] Unexpected error: {ex.Message}");
                throw new InvalidOperationException($"Key validation error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates that the key format is valid (alphanumeric or hexadecimal).
        /// </summary>
        private bool IsValidKeyFormat(string key)
        {
            // Allow alphanumeric characters, hyphens, and underscores
            foreach (char c in key)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Calculates and validates the key using a checksum algorithm.
        /// Extracts commander information from the key structure.
        /// </summary>
        private (bool isValid, int commanderId, string commanderName) CalculateAndValidateKey(string key)
        {
            try
            {
                // Key format: [PREFIX]-[DATA]-[CHECKSUM]
                // Example: KEY-CMDR000000000000-ABC123EF
                
                var parts = key.Split('-');
                if (parts.Length < 3)
                {
                    return (false, 0, "");
                }

                var prefix = parts[0];
                var dataSection = parts[1];
                var providedChecksum = parts[2];

                // Validate prefix
                if (prefix != "KEY")
                {
                    return (false, 0, "");
                }

                // Extract commander ID from data section
                // Format: CMDR + 9 digit ID
                if (!dataSection.StartsWith("CMDR") || dataSection.Length != 13)
                {
                    return (false, 0, "");
                }

                var idString = dataSection.Substring(4);
                if (!int.TryParse(idString, out var commanderId))
                {
                    return (false, 0, "");
                }

                // Generate commander name from ID
                var commanderName = GenerateCommanderName(commanderId);

                // Calculate expected checksum
                // Must match KeyGen's seed format: "CMDR-{commanderId}-ELITE-DANGEROUS"
                var expectedChecksum = CalculateChecksum($"CMDR-{commanderId}-ELITE-DANGEROUS");

                // Compare checksums (case-insensitive)
                var isValid = providedChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);

                return (isValid, commanderId, commanderName);
            }
            catch (Exception ex)
            {
                _outputWriter?.WriteLine($"[KeyValidator] Error parsing key: {ex.Message}");
                return (false, 0, "");
            }
        }

        /// <summary>
        /// Calculates a SHA256-based checksum for the key data.
        /// Returns first 8 characters of the hex hash.
        /// </summary>
        private string CalculateChecksum(string data)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                // Return first 8 hex characters (uppercase to match KeyGen)
                return BitConverter.ToString(hashBytes, 0, 4).Replace("-", "").ToUpperInvariant();
            }
        }

        /// <summary>
        /// Generates a commander name from the commander ID.
        /// Uses ID to seed a deterministic name generator.
        /// </summary>
        private string GenerateCommanderName(int commanderId)
        {
            // Generate a deterministic name based on ID
            var prefixes = new[] { "Commander", "Cmdr", "CMDR", "Pilot", "Captain" };
            var suffixes = new[] 
            { 
                "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel",
                "Iris", "Juliet", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa"
            };

            int prefixIndex = commanderId % prefixes.Length;
            int suffixIndex = (commanderId / prefixes.Length) % suffixes.Length;

            return $"{prefixes[prefixIndex]}-{suffixes[suffixIndex]}{commanderId}";
        }
    }
}
