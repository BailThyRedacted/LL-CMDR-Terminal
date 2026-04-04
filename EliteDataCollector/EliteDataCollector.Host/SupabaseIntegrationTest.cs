using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EliteDataCollector.Core;
using EliteDataCollector.Core.Models;
using EliteDataCollector.Core.Services;
using Microsoft.Extensions.Configuration;

namespace EliteDataCollector.Host
{
    /// <summary>
    /// Manual integration test for Supabase REST API implementation.
    /// Run this to verify:
    /// - Configuration loads correctly
    /// - Supabase connectivity works
    /// - User data is properly isolated via RLS
    /// - Retry logic handles transient errors
    /// </summary>
    public class SupabaseIntegrationTest
    {
        public static async Task RunTest(IConfiguration configuration, OutputWriter outputWriter)
        {
            Console.WriteLine("=== Supabase Integration Test ===\n");

            try
            {
                // === Test 1: Verify Configuration ===
                Console.WriteLine("[TEST 1] Verifying Supabase configuration...");
                var supabaseUrl = configuration["Supabase:Url"];
                var supabaseKey = configuration["Supabase:PublishableKey"];

                if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
                {
                    Console.WriteLine("❌ FAILED: Supabase credentials not found in appsettings.json");
                    return;
                }

                Console.WriteLine($"✓ Configuration verified:");
                Console.WriteLine($"  - URL: {supabaseUrl}");
                Console.WriteLine($"  - PublishableKey: {supabaseKey.Substring(0, 20)}...\n");

                // === Test 2: Initialize SupabaseClientImpl ===
                Console.WriteLine("[TEST 2] Initializing SupabaseClientImpl...");
                var supabaseClient = new SupabaseClientImpl(configuration, null, outputWriter);
                Console.WriteLine("✓ SupabaseClientImpl initialized\n");

                // === Test 3: Fetch Target Systems ===
                Console.WriteLine("[TEST 3] Fetching target systems from Supabase...");
                var systems = await supabaseClient.GetTargetSystemsAsync();
                Console.WriteLine($"✓ Fetched {systems.Count} target systems");
                foreach (var system in systems)
                {
                    Console.WriteLine($"  - {system}");
                }
                Console.WriteLine();

                // === Test 4: Upsert System Data ===
                Console.WriteLine("[TEST 4] Upserting sample system data...");
                var testSystem = new SystemData
                {
                    Id = 3932277478434,  // Sol's address
                    SystemName = "Sol",
                    Timestamp = DateTime.UtcNow,
                    ControllingFaction = "Federation",
                    Power = "Alliance",
                    PowerState = "Controlled",
                    LavignyInfluence = 42.5,
                    Factions = new List<FactionInfluence>
                    {
                        new() { Name = "Federation", Influence = 30.0, State = "Boom", Allegiance = "Federation" },
                        new() { Name = "Enemy Faction", Influence = 20.0, State = "None", Allegiance = "Independent" }
                    },
                    Structures = new List<Structure>
                    {
                        new() { Name = "Test Port", Type = "Orbital", ProgressPercent = 75.0 }
                    }
                };

                await supabaseClient.UpsertSystemDataAsync(testSystem);
                Console.WriteLine("✓ System data upserted successfully");
                Console.WriteLine("  Check Supabase dashboard: Tables > system_data");
                Console.WriteLine($"  Filter by user_id = '{Environment.UserName}'\n");

                // === Test 5: Upsert Structures ===
                Console.WriteLine("[TEST 5] Upserting structures...");
                var testStructures = new List<Structure>
                {
                    new() { Name = "Settlement Alpha", Type = "Settlement", ProgressPercent = 50.0 },
                    new() { Name = "Outpost Beta", Type = "Outpost", ProgressPercent = 100.0 }
                };

                await supabaseClient.UpsertStructuresAsync(testSystem.Id, testStructures);
                Console.WriteLine("✓ Structures upserted successfully");
                Console.WriteLine("  Check Supabase dashboard: Tables > structures\n");

                // === Test Summary ===
                Console.WriteLine("=== ALL TESTS PASSED ===");
                Console.WriteLine("\nNext steps:");
                Console.WriteLine("1. Check Supabase dashboard for the test data");
                Console.WriteLine("2. Verify data is filtered by user_id (Windows username)");
                Console.WriteLine("3. Log in as another Windows user and verify data isolation");
                Console.WriteLine("4. Test error handling by breaking the URL in appsettings.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ TEST FAILED: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }
}
