using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace KeyGen
{
    /// <summary>
    /// Elite Data Collector - Key Generator
    /// 
    /// Generates authentication keys in the format: KEY-CMDR[9-digit-id]-[checksum]
    /// Example: KEY-CMDR000000123-ABC123EF
    /// 
    /// This utility allows bulk key generation for testing, distribution, or administrative purposes.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   Elite Data Collector - Key Generator                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            int numberOfKeys = GetNumberOfKeys();
            
            Console.WriteLine();
            Console.WriteLine("Generating keys...");
            Console.WriteLine();

            var keys = GenerateKeys(numberOfKeys);

            // Display generated keys
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (var key in keys)
            {
                Console.WriteLine(key);
            }
            Console.ResetColor();

            // Save to file
            SaveKeysToFile(keys, numberOfKeys);

            Console.WriteLine();
            Console.WriteLine("✓ Key generation complete!");
            Console.WriteLine();
            
            // Try to wait for key press, but handle redirected input gracefully
            try
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                // Input is redirected (running in batch or piped mode), just exit
            }
        }

        /// <summary>
        /// Prompt user for the number of keys to generate.
        /// Validates input and returns a positive integer.
        /// </summary>
        static int GetNumberOfKeys()
        {
            while (true)
            {
                Console.Write("How many keys do you want to generate? ");
                if (int.TryParse(Console.ReadLine(), out int count) && count > 0)
                {
                    return count;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid input. Please enter a positive number.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Generates the requested number of authentication keys.
        /// 
        /// Key Format: KEY-CMDR[9-digit-id]-[checksum]
        /// - 9-digit ID: Random commander ID (000000000 to 999999999)
        /// - Checksum: 8-character hex hash derived from the ID
        /// 
        /// The checksum ensures key authenticity and prevents tampering.
        /// </summary>
        static string[] GenerateKeys(int count)
        {
            var keys = new string[count];
            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                // Generate random 9-digit commander ID
                int commanderId = random.Next(0, 1_000_000_000);
                string idString = commanderId.ToString("D9");

                // Generate checksum from ID
                string checksum = GenerateChecksum(commanderId);

                // Format key
                keys[i] = $"KEY-CMDR{idString}-{checksum}";
            }

            return keys;
        }

        /// <summary>
        /// Generates an 8-character hex checksum from the commander ID.
        /// Uses SHA256 for cryptographic integrity.
        /// 
        /// Teaching: Cryptographic hash functions produce deterministic,
        /// non-reversible outputs. Same input always produces same checksum.
        /// </summary>
        static string GenerateChecksum(int commanderId)
        {
            // Create a unique seed combining ID and salt
            string seed = $"CMDR-{commanderId}-ELITE-DANGEROUS";
            
            // Hash the seed
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(seed));
                
                // Take first 4 bytes and convert to 8-character hex
                string hex = BitConverter.ToString(hashBytes, 0, 4)
                    .Replace("-", "")
                    .ToUpperInvariant();
                
                return hex;
            }
        }

        /// <summary>
        /// Saves all generated keys to a text file with timestamp.
        /// File location: keys_[timestamp].txt in current directory
        /// </summary>
        static void SaveKeysToFile(string[] keys, int count)
        {
            string filename = $"keys_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Elite Data Collector - Generated Keys");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Count: {count}");
                sb.AppendLine(new string('=', 60));
                sb.AppendLine();

                foreach (var key in keys)
                {
                    sb.AppendLine(key);
                }

                System.IO.File.WriteAllText(filename, sb.ToString());
                
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"✓ Keys saved to: {filename}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠ Warning: Could not save keys to file: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
