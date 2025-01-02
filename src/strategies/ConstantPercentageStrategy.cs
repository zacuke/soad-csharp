using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using soad_csharp;
using soad_csharp.brokers;
using soad_csharp.database;
using soad_csharp.strategies.@abstract;

public class ConstantPercentageStrategy : SimpleStrategy
{
    private readonly IBroker _broker;
    private readonly TradeDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly List<AssetAllocation> _allocations;
    //private readonly decimal _strategyPercentage; // E.g., 10% of total account balance
    private readonly decimal _rebalanceThreshold = 0.05m; // 5% drift threshold for rebalancing
    private readonly string _strategyName;
    private readonly decimal _startingCapital;
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
    public decimal ThreshHoldCapital => _startingCapital - (_startingCapital * _rebalanceThreshold);
    public override async Task Execute()
    {
        //sync broker to local
        var brokerPositions = await SyncPositionsToLocalAsync();
        var hasUnfilledTrades = await CheckUnfilledTrades();
        if (hasUnfilledTrades)
        {
            _logger.LogWarning("Open or Unfilled trades found, so not running strategy {StrategyName}", StrategyName);
            return;
        }
        var brokerTotalValue = 0M;
        foreach (var position in brokerPositions)
        {
            brokerTotalValue += position.MarketValue;
        }
        _logger.LogDebug("Starting rebalance process for strategy {StrategyName}", StrategyName);

        //// Fetch account information
        // var accountInfo = await Broker.GetAccountInfoAsync();

        //// Get the latest cash balance
        //var cashBalance = accountInfo.BuyingPower;
 
        var is_initialized = await InitializeStrategyAsync(_startingCapital, brokerTotalValue);
       //is_initialized = false;
        if (!is_initialized)
        {
            //the idea here is we detected this is our first time
            //so all we do is buy the stocks if we need to get up to our starting capital

            //increase our holdings the first time if we don't have enough assets
            if (brokerTotalValue < ThreshHoldCapital) 
            { 
                foreach (var b in _allocations)
                {
                    var stockPrice = await _broker.GetCurrentPriceAsync(b.Symbol, b.AssetType);
                    b.CurrentPrice = stockPrice;
                //    b.DesiredAllocationValue = _startingCapital * b.Allocation;
                  //  b.DesiredAllocationQuantity = b.DesiredAllocationValue / b.CurrentPrice;

                }

                PostValidateAllocations(_allocations, _startingCapital);

                foreach (var b in _allocations)
                {
                    var currentHoldingQuantity = brokerPositions.GetBrokerPositionsWhere(b.Symbol).Quantity ;

                    if (b.DesiredAllocationQuantity > currentHoldingQuantity)
                    {
                        var quantityToBuy =  b.DesiredAllocationQuantity - currentHoldingQuantity;
                        decimal price = b.CurrentPrice ?? throw new Exception("unexpected null currentprice");
                        await PlaceOrderAsync(
                            symbol: b.Symbol, 
                            quantity: quantityToBuy, 
                            side: "buy", 
                            price: price, 
                            orderType:"market",
                            timeInForce:"gtc"
                        );

                    }
                }
            }
        }
        else
        {
            //but subsequent calls don't since we only handle reallocation based off original initialization
        }

        // var totalBalance =  balance.BalanceValue;

        // Fetch current positions from the database
        //var currentDbPositions = await _dbContext.Positions
        //                                        .Where(p => p.Strategy == StrategyName && p.Broker == _broker.GetType().Name)
        //                                        .ToListAsync();
        //var currentDbPositionsDict = currentDbPositions.ToDictionary(p => p.Symbol, p => (decimal)p.Quantity);

        // Calculate target cash and investment balances
        //var (targetCashBalance, targetInvestmentBalance) = CalculateTargetBalances(totalBalance, CashPercentage);

        //// Fetch current positions from the broker
        //var currentPositions = await Broker.GetPositionsAsync();

        //// Iterate over stock allocations and rebalance
        //foreach (var allocation in StockAllocations)
        //{
        //    var stock = allocation.Name;
        //    var targetAllocationPercentage = allocation.Allocation;

        //    // Calculate target balance for the stock
        //    var targetInvestment = targetInvestmentBalance * targetAllocationPercentage;

        //    var nonSlashed = TradingPairHelper.Translate(stock);
        //    // Check existing positions for the stock
        //    var currentQuantity = (decimal)currentDbPositions
        //        .FirstOrDefault(pos => pos.Symbol == nonSlashed).Quantity;

        //    // Get current price of the stock
        //    var currentPrice = await Broker.GetCurrentPriceAsync(stock, allocation.Type) ?? 0;

        //    if (currentPrice <= 0)
        //    {
        //        Logger.LogWarning("Invalid price for stock {Stock}. Skipping rebalance for strategy {StrategyName}.", stock, StrategyName);
        //        continue;
        //    }

        //    // Calculate target quantity
        //    var targetQuantity = targetInvestment / currentPrice;

        //    // If holdings are below the target quantity (minus buffer), buy more
        //    if (currentQuantity < targetQuantity * (1 - Buffer))
        //    {
        //        var quantityToBuy = targetQuantity - currentQuantity;
        //        await PlaceOrderAsync(stock, quantityToBuy, "buy", price: currentPrice);
        //    }
        //    // If holdings exceed the target quantity (plus buffer), sell the excess
        //    else if (currentQuantity > targetQuantity * (1 + Buffer))
        //    {
        //        var quantityToSell = currentQuantity - targetQuantity;
        //        await PlaceOrderAsync(stock, quantityToSell, "sell", price: currentPrice);
        //    }
        //}

        //// Sell any stocks not included in the defined allocations
        //foreach (var position in currentDbPositionsDict)
        //{
        //    // Check if the asset is present in the updated "StockAllocations" list
        //    bool existsInAllocations = StockAllocations.Any(asset => asset.Name == position.Key);

        //    // If not present, sell it
        //    if (!existsInAllocations)
        //    {
        //        await PlaceOrderAsync(position.Key, (int)position.Value, "sell");
        //    }
        //}

        //Logger.LogDebug("Rebalance process completed for strategy {StrategyName}", StrategyName);

        /////////////////////////////////////////////

        //// 1. Fetch account value and determine strategy budget
        //var accountInfo = await _broker.GetAccountInfoAsync(); // Assume broker provides account value
        //var totalAccountValue = accountInfo.PortfolioValue;
        //var strategyBudget = totalAccountValue * _strategyPercentage;

        //_logger.LogInformation("Total account value: {totalAccountValue}, Strategy budget: {strategyBudget}", totalAccountValue, strategyBudget);

        //// 2. Fetch current positions and values
        //var brokerPositions = await _broker.GetPositionsAsync();
        //var positions = brokerPositions.ToDictionary(pos => pos.Symbol, pos => pos.Quantity);
        //var currentPortfolioValue = brokerPositions.Sum(pos => pos.MarketValue);

        //// 3. Rebalance based on target allocations
        //_logger.LogInformation("Calculating rebalancing trades...");
        //var tradeRequests = new List<TradeRequest>();

        //foreach (var allocation in _allocations)
        //{
        //    var symbol = allocation.Name;
        //    var targetValue = allocation.Allocation * strategyBudget; // Target value for this asset
        //    var currentPrice = await _broker.GetCurrentPriceAsync(symbol, allocation.Type);

        //    if (!positions.ContainsKey(symbol)) positions[symbol] = 0;

        //    var currentValue = positions[symbol] * currentPrice;
        //    var drift = (currentValue - targetValue) / targetValue;

        //    if (Math.Abs(drift) > _rebalanceThreshold)
        //    {
        //        // Rebalance
        //        var quantityNeeded = ((targetValue - currentValue) / currentPrice);

        //        var trade = new TradeRequest
        //        {
        //            Symbol = symbol,
        //            Quantity = Math.Abs(quantityNeeded),
        //            Side = quantityNeeded > 0 ? "buy" : "sell",
        //            Price = currentPrice
        //        };
        //        tradeRequests.Add(trade);
        //    }
        //}

        //_logger.LogInformation("Executing {tradeRequestsCount} trades...", tradeRequests.Count);
        //foreach (var trade in tradeRequests)
        //{
        //    await _broker.PlaceOrderAsync(trade.Symbol, trade.Quantity, trade.Side, trade.Price, trade.OrderType, trade.TimeInForce);
        //}
    }
    // Sync positions with the broker and database

}
//protected override void PerformRebalance()
//    {
//        // Get current synchronized state
//        var positions = GetLocalPositions(); // Ticker -> Quantity
//        var cash = GetLocalCash();

//        // Calculate total portfolio value including cash
//        var portfolioValue = CalculatePortfolioValue(positions, cash);

//        // Iterate through allocations to calculate and place orders
//        foreach (var allocation in Allocations)
//        {
//            var ticker = allocation.Name;
//            var targetPercentage = allocation.Allocation;

//            // Calculate target and current value for this asset
//            var targetValue = targetPercentage * portfolioValue;
//            var currentValue = CalculatePositionValue(ticker, positions, cash);

//            // Determine the order size needed to rebalance
//            var orderSize = CalculateOrderSize(ticker, targetValue, currentValue);

//            // Send the buy/sell order if necessary
//            if (orderSize != 0)
//            {
//                PlaceOrder(ticker, orderSize);
//            }
//        }
//    }

//protected override decimal QueryBrokerCashBalance()
//{
//    throw new NotImplementedException();
//}

//protected override Dictionary<string, decimal> QueryBrokerPositions()
//{
//    throw new NotImplementedException();
//}

//protected override void UpdateLocalDatabase(decimal cash, Dictionary<string, decimal> positions)
//{
//    throw new NotImplementedException();
//}
//}
//public class ConstantPercentageStrategy : SimpleStrategy
//{
//        private readonly List<AssetAllocation> StockAllocations;
//        private readonly decimal CashPercentage;
//        //private readonly int RebalanceIntervalMinutes;
//        private readonly decimal Buffer;


//    public ConstantPercentageStrategy(
//        IBroker broker,
//        TradeDbContext dbContext,
//        string strategyName,
//        List<AssetAllocation> stockAllocations,
//        decimal cashPercentage,
//        int rebalanceIntervalMinutes,
//        decimal startingCapital,
//        decimal buffer = 0.1M,
//        ILogger logger = null)
//        : base(broker, dbContext, strategyName, startingCapital, rebalanceIntervalMinutes, logger: logger)
//    {
//        StockAllocations = stockAllocations ?? throw new ArgumentNullException(nameof(stockAllocations));
//        CashPercentage = cashPercentage;
//        //RebalanceIntervalMinutes = rebalanceIntervalMinutes;
//        Buffer = buffer;

//        Logger.LogInformation("Initialized {StrategyName} strategy with starting capital {StartingCapital:C}", strategyName, startingCapital);
//    }

//    // Initialize strategy by fetching balances and syncing positions
//    public async Task InitializeAsync()
//    {
//        await InitializeStartingBalanceAsync();
//        await SyncPositionsAsync();
//    }

    // Core rebalancing logic
    //public override async Task RebalanceAsync(List<BrokerPosition> brokerPositions)
    //{
    //    _logger.LogDebug("Starting rebalance process for strategy {StrategyName}", StrategyName);
 
    //    // Fetch account information
    //    var accountInfo = await Broker.GetAccountInfoAsync();

    //    // Get the latest cash balance
    //    var cashBalance = accountInfo.BuyingPower;

    //    var balance = await DbContext.Balances
    //                                 .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
    //                                 .OrderByDescending(b => b.Timestamp)
    //                                 .FirstOrDefaultAsync();

    //    if (balance == null)
    //    {
    //        Logger.LogError("Strategy balance not initialized for {StrategyName} strategy on {BrokerName}.", StrategyName, Broker.GetType().Name);
    //        throw new InvalidOperationException($"Strategy balance not initialized for {StrategyName} strategy on {Broker.GetType().Name}.");
    //    }

    //    var totalBalance = (decimal)balance.BalanceValue;

    //    // Fetch current positions from the database
    //    var currentDbPositions = await DbContext.Positions
    //                                            .Where(p => p.Strategy == StrategyName && p.Broker == Broker.GetType().Name)
    //                                            .ToListAsync();
    //    var currentDbPositionsDict = currentDbPositions.ToDictionary(p => p.Symbol, p => (decimal)p.Quantity);

    //    // Calculate target cash and investment balances
    //    var (targetCashBalance, targetInvestmentBalance) = CalculateTargetBalances(totalBalance, CashPercentage);

    //    // Fetch current positions from the broker
    //    var currentPositions = await Broker.GetPositionsAsync();

    //    // Iterate over stock allocations and rebalance
    //    foreach (var allocation in StockAllocations)
    //    {
    //        var stock = allocation.Name;
    //        var targetAllocationPercentage = allocation.Allocation;

    //        // Calculate target balance for the stock
    //        var targetInvestment = targetInvestmentBalance * targetAllocationPercentage;

    //        var nonSlashed = TradingPairHelper.Translate(stock);
    //        // Check existing positions for the stock
    //        var currentQuantity = (decimal)currentDbPositions
    //            .FirstOrDefault(pos => pos.Symbol == nonSlashed).Quantity;

    //        // Get current price of the stock
    //        var currentPrice = await Broker.GetCurrentPriceAsync(stock, allocation.Type) ?? 0;

    //        if (currentPrice <= 0)
    //        {
    //            Logger.LogWarning("Invalid price for stock {Stock}. Skipping rebalance for strategy {StrategyName}.", stock, StrategyName);
    //            continue;
    //        }

    //        // Calculate target quantity
    //        var targetQuantity = targetInvestment / currentPrice;

    //        // If holdings are below the target quantity (minus buffer), buy more
    //        if (currentQuantity < targetQuantity * (1 - Buffer))
    //        {
    //            var quantityToBuy = targetQuantity - currentQuantity;
    //            await PlaceOrderAsync(stock, quantityToBuy, "buy", price: currentPrice);
    //        }
    //        // If holdings exceed the target quantity (plus buffer), sell the excess
    //        else if (currentQuantity > targetQuantity * (1 + Buffer))
    //        {
    //            var quantityToSell = currentQuantity - targetQuantity;
    //            await PlaceOrderAsync(stock, quantityToSell, "sell", price: currentPrice);
    //        }
    //    }

    //    // Sell any stocks not included in the defined allocations
    //    foreach (var position in currentDbPositionsDict)
    //    {
    //        // Check if the asset is present in the updated "StockAllocations" list
    //        bool existsInAllocations = StockAllocations.Any(asset => asset.Name == position.Key);

    //        // If not present, sell it
    //        if (!existsInAllocations)
    //        {
    //            await PlaceOrderAsync(position.Key, (int)position.Value, "sell");
    //        }
    //    }

    //    Logger.LogDebug("Rebalance process completed for strategy {StrategyName}", StrategyName);
//    }

//    // Custom implementation to determine target quantity for a stock
//    public override async Task<decimal?> ShouldOwnAsync(string symbol, decimal currentPrice)
//    {
//        Logger.LogDebug("Calculating target ownership for stock {Symbol} in strategy {StrategyName}", symbol, StrategyName);

//        if (currentPrice <= 0)
//            throw new ArgumentException($"Invalid current price: {currentPrice}");

//        // Fetch balance
//        var balance = await DbContext.Balances
//                                     .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
//                                     .OrderByDescending(b => b.Timestamp)
//                                     .FirstOrDefaultAsync() 
//            ?? throw new InvalidOperationException($"No balance found for strategy {StrategyName} and broker {Broker.GetType().Name}.");

//        var totalBalance = (decimal)balance.BalanceValue;
//        var targetInvestmentBalance = totalBalance * (1 - CashPercentage);

//        //var allocationPercentage = StockAllocations.GetValueOrDefault(symbol, 0M);
//        var allocationPercentage = StockAllocations
//                .Where(asset => asset.Name == symbol)
//                .Select(asset => asset.Allocation)
//                .FirstOrDefault(); // Returns 0M if no match is found

//        var targetQuantity = Math.Floor(targetInvestmentBalance * allocationPercentage / currentPrice);

//        Logger.LogDebug("Target quantity for stock {Symbol}: {TargetQuantity} (Allocation: {Allocation:P}, Current Price: {CurrentPrice:C})",
//            symbol, targetQuantity, allocationPercentage, currentPrice);

//        return targetQuantity;
//    }
//}