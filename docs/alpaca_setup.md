# Alpaca Paper Trading Setup Guide

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

### Shadow Trading with PaperBroker (Default - In-Memory)

```bash
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol BTCUSD \
  --broker paper
```

**Features:**
- ✅ No API keys required
- ✅ Fully offline simulation
- ✅ Instant startup
- ✅ Same behavior as before

### Shadow Trading with Alpaca (Real Paper Trading API)

```bash
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol BTCUSD \
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
Starting Shadow Trading for BTCUSD...
Connected to Coinbase Feed for BTC-USD
[Market] 21:57:00 BTC-USD @ 88824.03 (Vol: 3)
[PaperBroker] Filled Buy 0.11223147 BTC-USD @ 88748.46. Fee: 49.80. Cash: 89593.53
```

### AlpacaBroker (Real API)
```
Starting Shadow Trading for BTCUSD...
[AlpacaBroker] Connected to Alpaca (Paper Trading)
[AlpacaBroker] Initial Cash: $100,000.00
Connected to Coinbase Feed for BTC-USD
[Market] 21:57:00 BTC-USD @ 88824.03 (Vol: 3)
[AlpacaBroker] Order submitted: 12345678-abcd-1234-5678-123456789abc - Buy 0.11223147 BTCUSD
```

## Verifying Orders in Alpaca Dashboard

1. Log in to [app.alpaca.markets/paper/dashboard](https://app.alpaca.markets/paper/dashboard/overview)
2. Navigate to **Orders** tab
3. See orders submitted by TradeFlex
4. Check **Positions** tab for filled orders
5. Monitor account balance changes

## Supported Symbols

Currently supports crypto pairs available on both:
- **Coinbase** (for market data)
- **Alpaca** (for order execution)

Common symbols:
- `BTCUSD` - Bitcoin
- `ETHUSD` - Ethereum
- Other crypto pairs supported by Alpaca

> [!NOTE]
> Symbol format differs between providers:
> - Coinbase uses: `BTC-USD` (with dash)
> - Alpaca uses: `BTCUSD` (no dash)
> 
> TradeFlex automatically normalizes `BTCUSD` → `BTC-USD` for Coinbase feed.

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

> [!IMPORTANT]
> New Alpaca paper accounts often have fractional trading **disabled by default**. You MUST enable it to trade crypto or fractional shares.

### Orders not appearing in Alpaca dashboard

**Check:**
1. Using paper trading keys (not live)
2. Logged into paper trading dashboard
3. Symbol is supported by Alpaca (some crypto may be unavailable)

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
3. **Test with PaperBroker**: Verify existing behavior works
4. **Test with AlpacaBroker**: Run shadow trading with Alpaca
5. **Monitor in dashboard**: Watch orders execute in real-time

## Support

For issues or questions:
- Check Alpaca docs: [docs.alpaca.markets](https://docs.alpaca.markets)
- Review TradeFlex README: [README.md](file:///Users/michaeldobrzynski/x/TradeFlex/README.md)
