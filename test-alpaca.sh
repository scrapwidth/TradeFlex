#!/bin/bash

# Alpaca Integration Sanity Check Script
# This script tests the Alpaca broker integration

set -e  # Exit on error

echo "========================================="
echo "  Alpaca Integration Sanity Check"
echo "========================================="
echo ""

# Load .env file if it exists
if [ -f .env ]; then
    echo "✓ Loading environment variables from .env file..."
    export $(cat .env | grep -v '^#' | grep -v '^$' | xargs)
else
    echo "⚠️  No .env file found. Make sure environment variables are set."
    exit 1
fi

echo ""
echo "Checking environment variables..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Check required environment variables
if [ -z "$ALPACA_API_KEY_ID" ]; then
    echo "❌ ALPACA_API_KEY_ID is not set"
    exit 1
else
    echo "✓ ALPACA_API_KEY_ID: ${ALPACA_API_KEY_ID:0:10}..."
fi

if [ -z "$ALPACA_SECRET_KEY" ]; then
    echo "❌ ALPACA_SECRET_KEY is not set"
    exit 1
else
    echo "✓ ALPACA_SECRET_KEY: ${ALPACA_SECRET_KEY:0:10}..."
fi

if [ -z "$ALPACA_USE_PAPER" ]; then
    export ALPACA_USE_PAPER="true"
    echo "✓ ALPACA_USE_PAPER: true (default)"
else
    echo "✓ ALPACA_USE_PAPER: $ALPACA_USE_PAPER"
fi

echo ""
echo "Building solution..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
dotnet build --verbosity quiet

echo ""
echo "Running unit tests..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
dotnet test --verbosity quiet --no-build

echo ""
dotnet run --project TradeFlex.IntegrationTest

