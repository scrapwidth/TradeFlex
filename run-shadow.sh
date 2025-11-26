#!/bin/bash

# Helper script to run shadow trading with environment loaded from .env
# Usage: ./run-shadow.sh [broker]
#   broker: 'paper' (default) or 'alpaca'

# Load .env file
if [ -f .env ]; then
    export $(cat .env | grep -v '^#' | grep -v '^$' | xargs)
    echo "✓ Loaded environment variables from .env"
else
    echo "❌ .env file not found!"
    exit 1
fi

# Determine broker (default to paper)
BROKER=${1:-paper}

echo "Starting shadow trading with ${BROKER} broker..."
echo ""

# Build sample strategies first
echo "Building sample strategies..."
dotnet build TradeFlex.SampleStrategies --verbosity quiet

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Press Ctrl+C to stop trading"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Run shadow trading
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol BTCUSD \
  --broker ${BROKER}
