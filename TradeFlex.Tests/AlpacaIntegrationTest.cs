using System;
using System.Threading.Tasks;
using TradeFlex.BrokerAdapters;

namespace TradeFlex.Tests;

/// <summary>
/// Integration test for Alpaca broker connection.
/// Run with: dotnet run --project TradeFlex.Tests -- alpaca-test
/// </summary>
public static class AlpacaIntegrationTest
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "alpaca-test")
        {
            return await RunAlpacaTest();
        }
        
        // Not an integration test run, return success
        return 0;
    }

    private static async Task<int> RunAlpacaTest()
    {
        Console.WriteLine("Starting Alpaca Integration Test...");
        Console.WriteLine();

        try
        {
            // Step 1: Load configuration
            Console.WriteLine("Step 1: Loading Alpaca configuration...");
            AlpacaConfiguration config;
            
            try
            {
                config = AlpacaConfiguration.FromEnvironment();
                Console.WriteLine("  ✓ Configuration loaded successfully");
                Console.WriteLine($"    - API Key ID: {config.ApiKeyId.Substring(0, Math.Min(10, config.ApiKeyId.Length))}...");
                Console.WriteLine($"    - Paper Trading: {config.UsePaperTrading}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Failed to load configuration: {ex.Message}");
                return 1;
            }

            Console.WriteLine();

            // Step 2: Create broker instance
            Console.WriteLine("Step 2: Creating AlpacaBroker instance...");
            AlpacaBroker broker;
            
            try
            {
                broker = new AlpacaBroker(config);
                Console.WriteLine("  ✓ Broker created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Failed to create broker: {ex.Message}");
                return 1;
            }

            Console.WriteLine();

            // Step 3: Get account balance
            Console.WriteLine("Step 3: Fetching account information...");
            try
            {
                var balance = broker.GetAccountBalance();
                Console.WriteLine($"  ✓ Account balance: {balance:C}");
                
                if (balance <= 0)
                {
                    Console.WriteLine("  ⚠️  Warning: Account balance is zero or negative");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Failed to get account balance: {ex.Message}");
                return 1;
            }

            Console.WriteLine();

            // Step 4: Get positions
            Console.WriteLine("Step 4: Fetching current positions...");
            try
            {
                var positions = broker.GetOpenPositions();
                Console.WriteLine($"  ✓ Found {positions.Count} open position(s)");
                
                foreach (var position in positions)
                {
                    Console.WriteLine($"    - {position.Key}: {position.Value:F8}");
                }
                
                if (positions.Count == 0)
                {
                    Console.WriteLine("    (No positions - this is normal for a new account)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Failed to get positions: {ex.Message}");
                return 1;
            }

            Console.WriteLine();

            // Step 5: Test position query for specific symbol
            Console.WriteLine("Step 5: Testing position query for BTCUSD...");
            try
            {
                var btcPosition = broker.GetPosition("BTCUSD");
                Console.WriteLine($"  ✓ BTCUSD position: {btcPosition:F8}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Failed to query position: {ex.Message}");
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("✅ All Alpaca integration tests passed!");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();
            Console.WriteLine("Your Alpaca broker is configured correctly and ready to use.");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Run shadow trading:");
            Console.WriteLine("     dotnet run --project TradeFlex.Cli -- shadow \\");
            Console.WriteLine("       --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \\");
            Console.WriteLine("       --symbol BTCUSD \\");
            Console.WriteLine("       --broker alpaca");
            Console.WriteLine();
            Console.WriteLine("  2. Monitor orders in Alpaca dashboard:");
            Console.WriteLine("     https://app.alpaca.markets/paper/dashboard/overview");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"❌ Integration test failed: {ex.Message}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();
            Console.WriteLine("Troubleshooting:");
            Console.WriteLine("  - Verify API keys are correct in .env file");
            Console.WriteLine("  - Check network connection");
            Console.WriteLine("  - Ensure using paper trading keys (not live)");
            Console.WriteLine();
            return 1;
        }
    }
}
