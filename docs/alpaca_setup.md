# Alpaca Setup Guide

## Prerequisites

1. **Alpaca Account**
   - Sign up at [alpaca.markets](https://alpaca.markets)
   - Navigate to paper trading dashboard
   - Generate API keys (Key ID and Secret Key)

## Configuration

### Environment Variables

Set the following environment variables:

```bash
export ALPACA_API_KEY_ID="your_paper_key_id_here"
export ALPACA_SECRET_KEY="your_paper_secret_here"
export ALPACA_USE_PAPER="true"  # Optional, defaults to true
```

### Permanent Configuration (macOS/Linux)

Add to your `~/.zshrc` or `~/.bashrc`:

```bash
# Alpaca Paper Trading Credentials
export ALPACA_API_KEY_ID="PK..."
export ALPACA_SECRET_KEY="..."
export ALPACA_USE_PAPER="true"
```

Then reload:
```bash
source ~/.zshrc  # or source ~/.bashrc
```

## Usage

### Download Historical Data

```bash
dotnet run --project TradeFlex.Cli -- download \
  --symbol AAPL \
  --from 2024-01-01 \
  --to 2024-12-31 \
  --granularity 1d \
  --output aapl_2024.parquet
```

### Shadow Trading with PaperBroker (In-Memory Simulation)

```bash
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol AAPL \
  --broker paper
```

**Features:**
- ✅ Uses Alpaca live data feed
- ✅ Simulated broker with realistic fees
- ✅ No real orders submitted

### Shadow Trading with Alpaca (Real Paper Trading API)

```bash
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol AAPL \
  --broker alpaca
```

**Features:**
- ✅ Orders submitted to Alpaca paper trading
- ✅ Orders visible in Alpaca dashboard
- ✅ Positions synced from Alpaca account
- ✅ Real broker behavior without risking capital

## Example Output

### PaperBroker (In-Memory)
```
Starting Shadow Trading for AAPL...
[AlpacaDataFeed] Connected to Alpaca Stream for AAPL
[AlpacaDataFeed] Subscribed to minute bars for AAPL
[Market] 10:31:00 AAPL @ 178.52 (Vol: 12345)
[PaperBroker] Filled Buy 56.02241 AAPL @ 178.52. Fee: 50.00. Cash: 89950.00
```

### AlpacaBroker (Real API)
```
Starting Shadow Trading for AAPL...
[AlpacaBroker] Connected to Alpaca (Paper Trading)
[AlpacaBroker] Initial Cash: $100,000.00
[AlpacaDataFeed] Connected to Alpaca Stream for AAPL
[AlpacaDataFeed] Subscribed to minute bars for AAPL
[Market] 10:31:00 AAPL @ 178.52 (Vol: 12345)
[AlpacaBroker] Order submitted: 12345678-abcd-... - Buy 56.02241 AAPL
```

## Verifying Orders in Alpaca Dashboard

1. Log in to [app.alpaca.markets/paper/dashboard](https://app.alpaca.markets/paper/dashboard/overview)
2. Navigate to **Orders** tab
3. See orders submitted by TradeFlex
4. Check **Positions** tab for filled orders
5. Monitor account balance changes

## Supported Symbols

Any US stock symbol supported by Alpaca:
- `AAPL` - Apple
- `MSFT` - Microsoft
- `GOOGL` - Alphabet
- `SPY` - S&P 500 ETF
- Most US stocks and ETFs

## Troubleshooting

### Error: "Environment variable ALPACA_API_KEY_ID is not set"

**Solution:** Make sure you've set the environment variables (see Configuration above).

### Error: "Failed to create Alpaca broker"

**Possible causes:**
1. API keys not set
2. Invalid API keys
3. Network connectivity issues

**Solution:**
```bash
# Verify environment variables are set
echo $ALPACA_API_KEY_ID
echo $ALPACA_SECRET_KEY

# Make sure they're not empty
```

### Error: "qty must be integer"

**Problem:** Fractional trading is not enabled on your Alpaca Paper account.

**Solution:**
1. Log in to [Alpaca Dashboard](https://app.alpaca.markets/paper/dashboard/overview)
2. Go to **Settings** (or Account Configuration)
3. Enable **Fractional Trading**
4. If you cannot find the setting, you may need to **Reset** your paper account.

### Orders not appearing in Alpaca dashboard

**Check:**
1. Using paper trading keys (not live)
2. Logged into paper trading dashboard
3. Market is open (9:30 AM - 4:00 PM ET, weekdays)

### No data during shadow trading

**Check:**
1. Market hours: 9:30 AM - 4:00 PM ET, weekdays
2. AlpacaDataFeed only yields completed minute bars
3. Wait for the current minute to complete

## Architecture

```
┌─────────────────────┐
│  Trading Algorithm  │
└──────────┬──────────┘
           │
           ▼
   ┌──────────────┐
   │   IBroker    │ (Interface)
   └──────┬───────┘
          │
     ┌────┴────┐
     │         │
     ▼         ▼
┌──────────┐ ┌──────────────┐
│  Paper   │ │   Alpaca     │
│  Broker  │ │   Broker     │
└──────────┘ └───────┬──────┘
  (In-Memory)        │
                     ▼
              ┌─────────────┐
              │ Alpaca API  │
              └─────────────┘
```

## Next Steps

1. **Get Alpaca credentials**: Sign up and generate API keys
2. **Set environment variables**: Configure your shell profile
3. **Download historical data**: Test the download command
4. **Run backtests**: Test strategies on historical data
5. **Test shadow trading**: Run with paper broker first
6. **Test with AlpacaBroker**: Run shadow trading with Alpaca
7. **Monitor in dashboard**: Watch orders execute in real-time
