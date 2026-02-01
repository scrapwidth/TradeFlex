using System.Threading.Tasks;
using TradeFlex.Abstractions;
using TradeFlex.Core;

namespace TradeFlex.SampleStrategies;

/// <summary>
/// A Martingale strategy that doubles position size after losses.
/// WARNING: This is extremely risky and can lead to total account blowup.
/// Included for educational/experimental purposes only.
/// </summary>
public sealed class MartingaleAlgorithm : BaseAlgorithm
{
    private readonly int _lookbackPeriod;
    private readonly decimal _basePositionPercent;
    private readonly decimal _takeProfitPercent;
    private readonly decimal _stopLossPercent;

    private decimal _entryPrice;
    private decimal _currentPositionSize;
    private int _consecutiveLosses;
    private int _barsSinceEntry;
    private bool _inPosition;

    /// <summary>
    /// Creates the algorithm with default parameters.
    /// </summary>
    public MartingaleAlgorithm() : this(5, 0.05m, 0.02m, 0.01m) { }

    /// <summary>
    /// Creates the algorithm with specified parameters.
    /// </summary>
    /// <param name="lookbackPeriod">Bars to wait before considering new entry.</param>
    /// <param name="basePositionPercent">Base position size as % of cash (default 5%).</param>
    /// <param name="takeProfitPercent">Take profit threshold (default 2%).</param>
    /// <param name="stopLossPercent">Stop loss threshold (default 1%).</param>
    public MartingaleAlgorithm(int lookbackPeriod, decimal basePositionPercent, decimal takeProfitPercent, decimal stopLossPercent)
    {
        _lookbackPeriod = lookbackPeriod;
        _basePositionPercent = basePositionPercent;
        _takeProfitPercent = takeProfitPercent;
        _stopLossPercent = stopLossPercent;
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IAlgorithmContext context)
    {
        base.InitializeAsync(context);
        _entryPrice = 0;
        _currentPositionSize = 0;
        _consecutiveLosses = 0;
        _barsSinceEntry = 0;
        _inPosition = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task OnBarAsync(Bar bar)
    {
        var cash = await Broker.GetAccountBalanceAsync();
        var position = await Broker.GetPositionAsync(bar.Symbol);

        if (_inPosition && position > 0)
        {
            _barsSinceEntry++;

            var pnlPercent = (bar.Close - _entryPrice) / _entryPrice;

            // Take profit
            if (pnlPercent >= _takeProfitPercent)
            {
                await SellAsync(bar.Symbol, position);
                _inPosition = false;
                _consecutiveLosses = 0; // Reset on win
                _barsSinceEntry = 0;
                return;
            }

            // Stop loss
            if (pnlPercent <= -_stopLossPercent)
            {
                await SellAsync(bar.Symbol, position);
                _inPosition = false;
                _consecutiveLosses++; // Increment losses for Martingale
                _barsSinceEntry = 0;
                return;
            }
        }
        else if (!_inPosition && _barsSinceEntry >= _lookbackPeriod)
        {
            // Calculate position size with Martingale multiplier
            // Double position after each loss, cap at 8x to prevent total blowup
            var multiplier = Math.Min(Math.Pow(2, _consecutiveLosses), 8);
            var positionPercent = _basePositionPercent * (decimal)multiplier;

            // Don't exceed 40% of account on any single trade
            positionPercent = Math.Min(positionPercent, 0.40m);

            var dollarAmount = cash * positionPercent;
            var quantity = dollarAmount / bar.Close;

            if (quantity > 0 && cash > dollarAmount)
            {
                await BuyAsync(bar.Symbol, quantity);
                _entryPrice = bar.Close;
                _currentPositionSize = quantity;
                _inPosition = true;
                _barsSinceEntry = 0;
            }
        }
        else
        {
            _barsSinceEntry++;
        }
    }
}
