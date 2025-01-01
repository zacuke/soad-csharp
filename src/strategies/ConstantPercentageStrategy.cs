using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using soad_csharp.database;
using soad_csharp.brokers;

namespace soad_csharp.strategies;

public class ConstantPercentageStrategy : BaseStrategy
{
        private readonly List<AssetAllocation> StockAllocations;
        private readonly decimal CashPercentage;
        //private readonly int RebalanceIntervalMinutes;
        private readonly decimal Buffer;


    public ConstantPercentageStrategy(
        IBroker broker,
        TradeDbContext dbContext,
        string strategyName,
        List<AssetAllocation> stockAllocations,
        decimal cashPercentage,
        int rebalanceIntervalMinutes,
        decimal startingCapital,
        decimal buffer = 0.1M,
        ILogger logger = null)
        : base(broker, dbContext, strategyName, startingCapital, rebalanceIntervalMinutes, logger: logger)
    {
        StockAllocations = stockAllocations ?? throw new ArgumentNullException(nameof(stockAllocations));
        CashPercentage = cashPercentage;
        //RebalanceIntervalMinutes = rebalanceIntervalMinutes;
        Buffer = buffer;

        Logger.LogInformation("Initialized {StrategyName} strategy with starting capital {StartingCapital:C}", strategyName, startingCapital);
    }

    // Initialize strategy by fetching balances and syncing positions
    public async Task InitializeAsync()
    {
        await InitializeStartingBalanceAsync();
        await SyncPositionsAsync();
    }

    // Core rebalancing logic
    public override async Task RebalanceAsync()
    {
        Logger.LogDebug("Starting rebalance process for strategy {StrategyName}", StrategyName);

        // Sync positions with the broker
        await SyncPositionsAsync();

        // Fetch account information
        var accountInfo = await Broker.GetAccountInfoAsync();

        // Get the latest cash balance
        var cashBalance = accountInfo.BuyingPower;

        var balance = await DbContext.Balances
                                     .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
                                     .OrderByDescending(b => b.Timestamp)
                                     .FirstOrDefaultAsync();

        if (balance == null)
        {
            Logger.LogError("Strategy balance not initialized for {StrategyName} strategy on {BrokerName}.", StrategyName, Broker.GetType().Name);
            throw new InvalidOperationException($"Strategy balance not initialized for {StrategyName} strategy on {Broker.GetType().Name}.");
        }

        var totalBalance = (decimal)balance.BalanceValue;

        // Fetch current positions from the database
        var currentDbPositions = await DbContext.Positions
                                                .Where(p => p.Strategy == StrategyName && p.Broker == Broker.GetType().Name)
                                                .ToListAsync();
        var currentDbPositionsDict = currentDbPositions.ToDictionary(p => p.Symbol, p => (decimal)p.Quantity);

        // Calculate target cash and investment balances
        var (targetCashBalance, targetInvestmentBalance) = CalculateTargetBalances(totalBalance, CashPercentage);

        // Fetch current positions from the broker
        var currentPositions = await Broker.GetPositionsAsync();

        // Iterate over stock allocations and rebalance
        foreach (var allocation in StockAllocations)
        {
            var stock = allocation.Name;
            var targetAllocationPercentage = allocation.Allocation;

            // Calculate target balance for the stock
            var targetInvestment = targetInvestmentBalance * targetAllocationPercentage;

            var nonSlashed = TradingPairHelper.Translate(stock);
            // Check existing positions for the stock
            var currentQuantity = (decimal)currentDbPositions
                .FirstOrDefault(pos => pos.Symbol == nonSlashed).Quantity ;

            // Get current price of the stock
            var currentPrice = await Broker.GetCurrentPriceAsync(stock, allocation.Type) ?? 0;

            if (currentPrice <= 0)
            {
                Logger.LogWarning("Invalid price for stock {Stock}. Skipping rebalance for strategy {StrategyName}.", stock, StrategyName);
                continue;
            }

            // Calculate target quantity
            var targetQuantity = targetInvestment / currentPrice;

            // If holdings are below the target quantity (minus buffer), buy more
            if (currentQuantity < targetQuantity * (1 - Buffer))
            {
                var quantityToBuy = targetQuantity - currentQuantity;
                await PlaceOrderAsync(stock, quantityToBuy, "buy", price: currentPrice);
            }
            // If holdings exceed the target quantity (plus buffer), sell the excess
            else if (currentQuantity > targetQuantity * (1 + Buffer))
            {
                var quantityToSell = currentQuantity - targetQuantity;
                await PlaceOrderAsync(stock, quantityToSell, "sell", price: currentPrice);
            }
        }

        // Sell any stocks not included in the defined allocations
        foreach (var position in currentDbPositionsDict)
        {
            // Check if the asset is present in the updated "StockAllocations" list
            bool existsInAllocations = StockAllocations.Any(asset => asset.Name == position.Key);

            // If not present, sell it
            if (!existsInAllocations)
            {
                await PlaceOrderAsync(position.Key, (int)position.Value, "sell");
            }
        }

        Logger.LogDebug("Rebalance process completed for strategy {StrategyName}", StrategyName);
    }

    // Custom implementation to determine target quantity for a stock
    public override async Task<decimal?> ShouldOwnAsync(string symbol, decimal currentPrice)
    {
        Logger.LogDebug("Calculating target ownership for stock {Symbol} in strategy {StrategyName}", symbol, StrategyName);

        if (currentPrice <= 0)
            throw new ArgumentException($"Invalid current price: {currentPrice}");

        // Fetch balance
        var balance = await DbContext.Balances
                                     .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
                                     .OrderByDescending(b => b.Timestamp)
                                     .FirstOrDefaultAsync() 
            ?? throw new InvalidOperationException($"No balance found for strategy {StrategyName} and broker {Broker.GetType().Name}.");

        var totalBalance = (decimal)balance.BalanceValue;
        var targetInvestmentBalance = totalBalance * (1 - CashPercentage);

        //var allocationPercentage = StockAllocations.GetValueOrDefault(symbol, 0M);
        var allocationPercentage = StockAllocations
                .Where(asset => asset.Name == symbol)
                .Select(asset => asset.Allocation)
                .FirstOrDefault(); // Returns 0M if no match is found

        var targetQuantity = Math.Floor(targetInvestmentBalance * allocationPercentage / currentPrice);

        Logger.LogDebug("Target quantity for stock {Symbol}: {TargetQuantity} (Allocation: {Allocation:P}, Current Price: {CurrentPrice:C})",
            symbol, targetQuantity, allocationPercentage, currentPrice);

        return targetQuantity;
    }
}