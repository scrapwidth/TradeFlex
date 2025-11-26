# Testing Your Alpaca Integration

## Quick Start - Sanity Check

### Step 1: Run the Test Script

```bash
./test-alpaca.sh
```

This script will:
1. ✅ Load your `.env` file
2. ✅ Validate environment variables
3. ✅ Build the solution
4. ✅ Run unit tests
5. ✅ Test Alpaca API connection
6. ✅ Verify account access
7. ✅ Check positions

### Step 2: Expected Output

If everything works, you should see:

```
✓ Configuration loaded successfully
  - API Key ID: PKZYJONHYW...
  - Paper Trading: True

✓ Broker created successfully
✓ Account balance: $100,000.00
✓ Found 0 open position(s)
✓ BTCUSD position: 0.00000000

✅ All Alpaca integration tests passed!
```

---

## Manual Testing

### Test 1: PaperBroker (In-Memory Simulation)

Test that the original behavior still works:

```bash
# First, build the sample strategy
dotnet build TradeFlex.SampleStrategies

# Run shadow trading with PaperBroker
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol BTCUSD \
  --broker paper
```

**Expected:**
- No API calls
- In-memory simulation
- Orders logged to console only

**To stop:** Press `Ctrl+C`

### Test 2: AlpacaBroker (Real Paper Trading)

Test the new Alpaca integration:

```bash
# Make sure .env is loaded
source .env  # Or: export $(cat .env | xargs)

# Run shadow trading with AlpacaBroker
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol BTCUSD \
  --broker alpaca
```

**Expected:**
```
[AlpacaBroker] Connected to Alpaca (Paper Trading)
[AlpacaBroker] Initial Cash: $100,000.00
Connected to Coinbase Feed for BTC-USD
[Market] 17:45:23 BTC-USD @ 95824.15 (Vol: 5)
[AlpacaBroker] Order submitted: abc123... - Buy 0.11234567 BTCUSD
```

**Verify in Dashboard:**
1. Go to: https://app.alpaca.markets/paper/dashboard/overview
2. Navigate to **Orders** tab
3. You should see your TradeFlex orders!

**To stop:** Press `Ctrl+C`

---

## Troubleshooting

### Error: "Environment variable not set"

**Problem:** `.env` file not loaded

**Solution:**
```bash
# Option 1: Use the test script (recommended)
./test-alpaca.sh

# Option 2: Source the .env file manually
export $(cat .env | grep -v '^#' | xargs)
```

### Error: "Failed to create broker"

**Problem:** Invalid API keys or network issue

**Solutions:**
1. Verify keys in `.env` file:
   ```bash
   cat .env
   ```
   Should show:
   ```
   ALPACA_API_KEY_ID=PK...
   ALPACA_SECRET_KEY=...
   ```

2. Test API keys directly at: https://app.alpaca.markets/paper/dashboard

3. Check network connectivity:
   ```bash
   curl -I https://paper-api.alpaca.markets/
   ```

### Warning: "Account balance is zero"

**Problem:** Your Alpaca paper account has no funds

**Solution:**
1. Log in to Alpaca dashboard
2. Go to Paper Trading settings
3. Reset paper account (this gives you $100,000 virtual funds)

### Orders not appearing in dashboard

**Check:**
1. Using the paper trading dashboard (not live)
2. Symbol is valid (BTCUSD should work for crypto)
3. Orders had valid prices (market orders should fill immediately)

---

## What the Test Script Does

The `test-alpaca.sh` script performs these checks:

### 1. Environment Validation ✅
- Loads `.env` file
- Checks `ALPACA_API_KEY_ID` is set
- Checks `ALPACA_SECRET_KEY` is set
- Sets `ALPACA_USE_PAPER=true` by default

### 2. Build Validation ✅
- Runs `dotnet build` to ensure solution compiles
- Runs `dotnet test` to ensure no regressions

### 3. Alpaca Integration Test ✅
- Creates `AlpacaConfiguration` from environment
- Creates `AlpacaBroker` instance
- Fetches account balance
- Fetches current positions
- Queries specific symbol position

---

## Test Results Summary

| Test | Status | Notes |
|------|--------|-------|
| Environment variables | ✅ PASSED | Keys loaded from `.env` |
| Build | ✅ PASSED | All projects compiled |
| Unit tests | ✅ PASSED | 5/5 tests passed |
| Configuration loading | ✅ PASSED | API keys recognized |
| Broker creation | ✅ PASSED | Connected to Alpaca Paper API |
| Account balance | ✅ PASSED | $100,000.00 |
| Position query | ✅ PASSED | No positions (expected) |

---

## Next Steps

Now that the integration test passed, you can:

1. **Run live shadow trading:**
   ```bash
   dotnet run --project TradeFlex.Cli -- shadow \
     --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
     --symbol BTCUSD \
     --broker alpaca
   ```

2. **Monitor orders in real-time:**
   - Visit: https://app.alpaca.markets/paper/dashboard/overview
   - Watch orders appear as your algorithm trades

3. **Try different symbols:**
   - ETHUSD (Ethereum)
   - Other crypto pairs supported by Alpaca

4. **Create your own strategy:**
   - Copy `SimpleSmaCrossoverAlgorithm.cs`
   - Modify the logic
   - Test with PaperBroker first, then Alpaca

---

## Files Created

- `test-alpaca.sh` - Automated test script
- `TradeFlex.IntegrationTest/` - Integration test project
  - Tests Alpaca API connectivity
  - Validates configuration
  - Checks account access

---

## Support

If you encounter issues:
1. Check this troubleshooting guide
2. Review logs in console output
3. Verify API keys in Alpaca dashboard
4. Check Alpaca API status: https://status.alpaca.markets/
