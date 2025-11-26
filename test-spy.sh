#!/bin/bash

# Test Alpaca integration with a stock symbol (SPY)
# Stocks are fully supported by Alpaca and easier to test with

# Load .env file
if [ -f .env ]; then
    export $(cat .env | grep -v '^#' | grep -v '^$' | xargs)
    echo "✓ Loaded environment variables from .env"
else
    echo "❌ .env file not found!"
    exit 1
fi

BROKER=${1:-alpaca}

echo "Testing with SPY (S&P 500 ETF) - fully supported by Alpaca"
echo ""

# Build
dotnet build TradeFlex.SampleStrategies --verbosity quiet

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Running shadow trading with SPY"
echo "Press Ctrl+C to stop"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Note: This will fail because we need a data feed for SPY
# But it will test the Alpaca order submission
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol SPY \
  --broker ${BROKER}
