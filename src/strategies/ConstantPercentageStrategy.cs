using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using soad_csharp;
using soad_csharp.brokers;
using soad_csharp.database;
using soad_csharp.Extensions;
using soad_csharp.strategies.@abstract;
using System.Diagnostics;

public class ConstantPercentageStrategy : SimpleStrategy
{
    private readonly IBroker _broker;
    private readonly TradeDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly List<AssetAllocation> _allocations;
    private readonly decimal _rebalanceThreshold = 0.05m; // 5% drift threshold for rebalancing
    private readonly string _strategyName;
    private readonly decimal _startingCapital;

    private List<BrokerPosition> _brokerPositions;
    private List<TradeRequest> _tradeRequests;
    public ConstantPercentageStrategy(
        IBroker broker,
        TradeDbContext dbContext,
        List<AssetAllocation> allocations,
        ILogger logger,
        decimal startingCapital,
        string strategyName
    ) : base(logger,broker,dbContext)
    {
        _broker = broker;
        _dbContext = dbContext;
        _logger = logger;
        _allocations = allocations;
        _startingCapital = startingCapital;
        PreValidateAllocations(_allocations);
        _strategyName = strategyName;
    }

    public override string StrategyName => _strategyName;
    public override decimal StartingCapital => _startingCapital;
    public override decimal ThresholdCapital => _startingCapital - (_startingCapital * _rebalanceThreshold);
    public override decimal BrokerTotalValue => _brokerPositions.TotalMarketValue();


    public override List<BrokerPosition> BrokerPositions => _brokerPositions;
    public override List<TradeRequest> TradeRequests => _tradeRequests;
    public override async Task Execute()
    {
        // Sync broker to local
        _brokerPositions = await SyncPositionsToLocalAsync();

        await CheckUnfilledTrades();

        if (!await IsStrategyInitializedAsync())
        {
            _logger.LogDebug("Initial purchase allocation for strategy {StrategyName}", StrategyName);
            await PurchaseAllocation(_allocations);
            return;
        }
        await RunStrategy();

        await ExecuteTrades();
    }

    public override async Task RunStrategy()
    {
        var portfolioValue = PortfolioValue(_allocations);

        TradeRequests.Clear();
        foreach (var allocation in _allocations)
        {
            var symbol = allocation.Symbol;
            var targetPercentage = allocation.Allocation;

            _logger.LogDebug("Processing allocation for {Symbol} with target percentage {TargetPercentage:P}", symbol, targetPercentage);

            // Calculate the target value for this asset based only on portfolio value (exclude cash)
            var targetValue = portfolioValue * targetPercentage;

            // Get the current broker position for this symbol
            var currentPosition = _brokerPositions.GetBrokerPositionsWhere(allocation.Symbol);
            var currentQuantity = currentPosition?.Quantity ?? 0;
            var currentMarketValue = currentPosition?.MarketValue ?? 0;

            _logger.LogDebug("Current Market Value for {Symbol}: {CurrentMarketValue:C}, Target Value: {TargetValue:C}", symbol, currentMarketValue, targetValue);

            // Check if the position needs rebalancing based on the drift threshold
            var drift = (currentMarketValue - targetValue) / targetValue;

            if (Math.Abs(drift) > _rebalanceThreshold) // Significant drift detected
            {
                _logger.LogInformation("Rebalancing required for {Symbol} due to a drift of {Drift:P}", symbol, drift);

                // Fetch the current price of the asset
                var currentPrice = await _broker.GetCurrentPriceAsync(symbol, allocation.AssetType);

                if (currentPrice <= 0)
                {
                    _logger.LogWarning("Unable to retrieve valid price for {Symbol}. Skipping rebalance.", symbol);
                    continue;
                }

                // Calculate the number of shares required to rebalance
                var targetQuantity = targetValue / currentPrice;
                var quantityToTrade = targetQuantity - currentQuantity;

                if (quantityToTrade != 0)
                {
                    // Determine trade action (buy or sell)
                    var tradeAction = quantityToTrade > 0 ? "buy" : "sell";
                    TradeRequests.Add(new TradeRequest
                    {
                        Symbol = symbol,
                        Quantity = Math.Abs(quantityToTrade),
                        Side = tradeAction,
                        Price = currentPrice,
                        OrderType = "market", // or "limit" based on your system
                        TimeInForce = "gtc"  // Good Till Canceled
                    });

                    _logger.LogInformation("Prepared {TradeAction} trade for {Quantity} units of {Symbol} at {CurrentPrice:C}",
                        tradeAction, Math.Abs(quantityToTrade), symbol, currentPrice);
                }
            }
        }
    }
}
 