using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using soad_csharp.brokers;
using soad_csharp.database;
using System.Diagnostics;
using BrokerPosition = soad_csharp.brokers.BrokerPosition;

namespace soad_csharp.strategies.@abstract;
public abstract class SimpleStrategy(ILogger _logger, IBroker _broker, TradeDbContext _dbContext) 
{
    //    // Abstract properties and methods for derived strategies
    public abstract string StrategyName { get; }
   // public abstract decimal StartingCapital { get; }
    public abstract Task Execute();
    //    protected abstract void PerformRebalance();

    //    public void Execute()
    //    {
    //        // 1. Synchronize current account state with broker
    //        SyncBrokerState();

    //        // 2. Perform rebalance logic
    //        PerformRebalance();

    //        // 3. Log the execution
    //        LogExecution();
    //    }

    //    // Synchronize the state of the account (cash + positions)
    //    private void SyncBrokerState()
    //    {
    //        var cash = QueryBrokerCashBalance(); // Query available cash
    //        var positions = QueryBrokerPositions(); // Query current positions

    //        // Update the local database to reflect the latest broker state
    //        UpdateLocalDatabase(cash, positions);

    //        // Log for debugging/visibility
    //        Console.WriteLine("Synchronized broker state:");
    //        Console.WriteLine($"Cash: {cash}");
    //        foreach (var (ticker, quantity) in positions)
    //        {
    //            Console.WriteLine($"{ticker}: {quantity}");
    //        }
    //    }

    //    protected abstract decimal QueryBrokerCashBalance();
    //    protected abstract Dictionary<string, decimal> QueryBrokerPositions(); // Ticker -> Quantity
    //    protected abstract void UpdateLocalDatabase(decimal cash, Dictionary<string, decimal> positions);

    //    // Log to confirm the execution process
    //    private void LogExecution()
    //    {
    //        Console.WriteLine($"[{DateTime.Now}] {StrategyName} execution completed.");
    //    }

    /// <summary>
    /// Syncs broker to database
    /// </summary>
    /// <returns>List of positions returned from broker</returns>
    protected async Task<List<BrokerPosition>> SyncPositionsToLocalAsync()
    {
        _logger.LogDebug("Syncing positions for strategy {StrategyName}", StrategyName);

        // Fetch positions from the broker
        var brokerPositions = await _broker.GetPositionsAsync();

        // Fetch database positions for the strategy
        var dbPositions = await _dbContext.Positions
            .Where(p => p.Strategy == StrategyName && p.Broker == _broker.GetType().Name)
            .ToListAsync();

        foreach (var brokerPos in brokerPositions)
        {
            // Check if the symbol already exists in the database
            var dbPos = dbPositions.FirstOrDefault(p => p.Symbol == brokerPos.Symbol);

            if (dbPos == null)
            {
                // Add new position to the database
                var newPosition = new Position
                {
                    Broker = _broker.GetType().Name,
                    Strategy = StrategyName,
                    Symbol = brokerPos.Symbol,
                    Quantity = brokerPos.Quantity,
                    LatestPrice = brokerPos.CurrentPrice,
                    LastUpdated = DateTime.UtcNow,
                    CostBasis = brokerPos.AverageEntryPrice,
                };

                await _dbContext.Positions.AddAsync(newPosition);
                _logger.LogInformation("Added new position for symbol {Symbol} in strategy {StrategyName}", brokerPos.Symbol, StrategyName);
            }
            else
            {
                // Update existing position
                dbPos.Quantity = brokerPos.Quantity;
                dbPos.LatestPrice = brokerPos.MarketValue;
                dbPos.LastUpdated = DateTime.UtcNow;
                dbPos.CostBasis = brokerPos.AverageEntryPrice;
                dbPos.LatestPrice = brokerPos.CurrentPrice;
                _dbContext.Positions.Update(dbPos);
                _logger.LogInformation("Updated position for symbol {Symbol} in strategy {StrategyName}", brokerPos.Symbol, StrategyName);
            }
        }

        // Save changes to the database
        await _dbContext.SaveChangesAsync();

        _logger.LogDebug("Position sync completed for strategy {StrategyName}", StrategyName);
        return brokerPositions;
    }

    protected void PreValidateAllocations(List<AssetAllocation> allocations)
    {
        var total = allocations.Sum(a => a.Allocation);
        if (Math.Round(total, 8) != 1.0M)
        {
            throw new Exception("Allocations must sum to 1.0 (100%)");
        }
    }
    protected void PostValidateAllocations(List<AssetAllocation> allocations, decimal startingCapital)
    {
        decimal checkAllocations = 0;
        foreach (var b in allocations)
        {
            checkAllocations += b.DesiredAllocationValue;
        }
        if (checkAllocations != startingCapital)
            throw new Exception("Problem calculation allocation");

    }
    // Place an order with the broker and record it in the trades table
    public async Task PlaceOrderAsync(
        string symbol,
        decimal quantity,
        string side,
        decimal price,
        string orderType,
        string timeInForce)
    {   
        // Save the trade details in the database
        var trade = new Trade
        {
            Broker = _broker.GetType().Name,
            Strategy = StrategyName,
            Symbol = symbol,
            Quantity = quantity,
            Price = price,
            Side = side,
            //Status = response.Status,
            Timestamp = DateTime.UtcNow,
            ExecutionStyle = string.Empty
        };

        await _dbContext.Trades.AddAsync(trade);
        await _dbContext.SaveChangesAsync();
        OrderResponse response;
        try
        {
            var clientOrderId = Guid.NewGuid().ToString();
            response = await _broker.PlaceOrderAsync(symbol, quantity, side, price, orderType, timeInForce, clientOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order");
            _dbContext.Trades.Remove(trade);
            await _dbContext.SaveChangesAsync();
            Debugger.Break();
            throw;
        }
        trade.Quantity = response.Quantity;
        trade.Status = response.Status;
        trade.BrokerResponseId = response.BrokerResponseId;
        trade.ClientOrderId = response.ClientOrderId;
        trade.BrokerResponseAssetId = response.BrokerResponseAssetId;
        trade.BrokerResponseAssetClass = response.BrokerResponseAssetClass;
        trade.BrokerResponseFilledQty = response.BrokerResponseFilledQty;
        trade.IsFilled = response.Quantity == response.BrokerResponseFilledQty;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Order placed for strategy {StrategyName}: {OrderDetails}", StrategyName, trade);
    }


    public async Task<bool> InitializeStrategyAsync(decimal startingCapital, decimal brokerTotalValue)
    {
        bool is_initialized = false;
        var balance = await _dbContext.Balances
                                     .Where(b => b.Strategy == StrategyName && b.Broker == _broker.GetType().Name)
                                     .OrderByDescending(b => b.Timestamp)
                                     .FirstOrDefaultAsync();

        if (balance == null)
        {
            // Add new balance record
            balance = new Balance
            {
                Broker = _broker.GetType().Name,
                Strategy = StrategyName,
                Type = "cash",
                BalanceValue = startingCapital,
                Timestamp = DateTime.UtcNow
            };

            await _dbContext.Balances.AddAsync(balance);

        }
        else
        {
            is_initialized = true;

            // Add new balance record
            balance = new Balance
            {
                Broker = _broker.GetType().Name,
                Strategy = StrategyName,
                Type = "positions",
                BalanceValue = Math.Round(brokerTotalValue,2),
                Timestamp = DateTime.UtcNow
            };

            await _dbContext.Balances.AddAsync(balance);
        }
        await _dbContext.SaveChangesAsync();

        return is_initialized;
    }

    public async Task<bool> CheckUnfilledTrades()
    { 
        //check for filled
        var unfilledTrades = await (from a in _dbContext.Trades
                  where a.Strategy == StrategyName
                    && a.IsFilled == false
                  select a).ToListAsync();

        foreach(var trade in unfilledTrades)
        {
            var existingTrade = await _broker.GetExistingOrderAsync(trade.BrokerResponseId, trade.ClientOrderId);
            if ( existingTrade.Quantity == existingTrade.BrokerResponseFilledQty)
            {
                
                trade.IsFilled = true;
            }
        }

        await _dbContext.SaveChangesAsync();

        //extra check for any open orders (in case some not in database)
        var unfilledTradesFromBroker = await _broker.GetOrdersAsync("open");

        return unfilledTradesFromBroker.Count > 0 
                || await (from a in _dbContext.Trades
                       where a.Strategy == StrategyName
                         && a.IsFilled == false
                       select a).AnyAsync() ;
       
    }
}

////using System;
////using System.Collections.Generic;
////using System.Linq;
////using System.Threading.Tasks;
////using Microsoft.EntityFrameworkCore;
////using Microsoft.Extensions.Logging;
////using soad_csharp.database;
////using soad_csharp.brokers;
////using Microsoft.Extensions.Logging.Abstractions;

////namespace soad_csharp.strategies
////{
////    public abstract class BaseStrategy(
////        IBroker broker,
////        TradeDbContext dbContext,
////        string strategyName,
////        decimal startingCapital,
////        int rebalanceIntervalMinutes = 5,
////        string executionStyle = "",
////        ILogger logger = null)
////    {
////        protected readonly IBroker Broker = broker ?? throw new ArgumentNullException(nameof(broker));
////        protected readonly TradeDbContext DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
////        protected readonly string StrategyName = strategyName;
////        protected readonly decimal StartingCapital = startingCapital;
////        protected readonly int RebalanceIntervalMinutes = rebalanceIntervalMinutes;
////        protected readonly string ExecutionStyle = executionStyle;
////        bool IsInitialized;

////        protected readonly ILogger Logger = logger ?? NullLogger.Instance;

////        // Abstract method that derived strategies must implement
////        public abstract Task RebalanceAsync();

////        // Initialize starting balance in the database
////        public async Task InitializeStartingBalanceAsync()
////        {
////            if (IsInitialized)
////            {
////                Logger.LogDebug("Starting balance already initialized for strategy {StrategyName}", StrategyName);
////                return;
////            }

////            Logger.LogDebug("Initializing starting balance for strategy {StrategyName}", StrategyName);

////            // Fetch broker account information
////            var accountInfo = await Broker.GetAccountInfoAsync();
////            if (accountInfo.BuyingPower < StartingCapital)
////            {
////                Logger.LogError(
////                    "Not enough buying power. Required: {Required}, Available: {Available} for strategy {StrategyName}",
////                    StartingCapital, accountInfo.BuyingPower, StrategyName);
////                throw new InvalidOperationException("Insufficient funds to initialize strategy.");
////            }

////            // Check if the strategy balance already exists in the database
////            var existingBalance = await DbContext.Balances
////                .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
////                .OrderByDescending(b => b.Timestamp)
////                .FirstOrDefaultAsync();

////            if (existingBalance == null)
////            {
////                // Add new balance record
////                var balance = new Balance
////                {
////                    Broker = Broker.GetType().Name,
////                    Strategy = StrategyName,
////                    Type = "Cash",
////                    BalanceValue = (float)StartingCapital,
////                    Timestamp = DateTime.UtcNow
////                };

////                await DbContext.Balances.AddAsync(balance);
////                await DbContext.SaveChangesAsync();
////                Logger.LogInformation(
////                    "Initialized starting balance for strategy {StrategyName} with {StartingCapital:C}",
////                    StrategyName, StartingCapital);
////            }
////            else
////            {
////                Logger.LogInformation(
////                    "Existing balance {BalanceValue:C} found for strategy {StrategyName}",
////                    existingBalance.BalanceValue, StrategyName);
////            }

////            IsInitialized = true;
////        }

////        // Sync positions with the broker and database
////        public async Task SyncPositionsAsync()
////        {
////            Logger.LogDebug("Syncing positions for strategy {StrategyName}", StrategyName);

////            // Fetch positions from the broker
////            var brokerPositions = await Broker.GetPositionsAsync();

////            // Fetch database positions for the strategy
////            var dbPositions = await DbContext.Positions
////                .Where(p => p.Strategy == StrategyName && p.Broker == Broker.GetType().Name)
////                .ToListAsync();

////            foreach (var brokerPos in brokerPositions)
////            {
////                // Check if the symbol already exists in the database
////                var dbPos = dbPositions.FirstOrDefault(p => p.Symbol == brokerPos.Symbol);

////                if (dbPos == null)
////                {
////                    // Add new position to the database
////                    var newPosition = new database.Position
////                    {
////                        Broker = Broker.GetType().Name,
////                        Strategy = StrategyName,
////                        Symbol = brokerPos.Symbol,
////                        Quantity = (float)brokerPos.Quantity,
////                        LatestPrice = (float)brokerPos.MarketValue,
////                        LastUpdated = DateTime.UtcNow
////                    };

////                    await DbContext.Positions.AddAsync(newPosition);
////                    Logger.LogInformation("Added new position for symbol {Symbol} in strategy {StrategyName}", brokerPos.Symbol, StrategyName);
////                }
////                else
////                {
////                    // Update existing position
////                    dbPos.Quantity = (float)brokerPos.Quantity;
////                    dbPos.LatestPrice = (float)brokerPos.MarketValue;
////                    dbPos.LastUpdated = DateTime.UtcNow;
////                    DbContext.Positions.Update(dbPos);
////                    Logger.LogInformation("Updated position for symbol {Symbol} in strategy {StrategyName}", brokerPos.Symbol, StrategyName);
////                }
////            }

////            // Save changes to the database
////            await DbContext.SaveChangesAsync();

////            Logger.LogDebug("Position sync completed for strategy {StrategyName}", StrategyName);
////        }

////        // Fetch the current cash balance from the database or broker
////        public async Task<decimal> GetCashAsync()
////        {
////            // Get latest balance from the database
////            var balance = await DbContext.Balances
////                .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
////                .OrderByDescending(b => b.Timestamp)
////                .FirstOrDefaultAsync();

////            if (balance != null)
////            {
////                return (decimal)balance.BalanceValue;
////            }

////            // As fallback, fetch cash directly from the broker
////            var accountInfo = await Broker.GetAccountInfoAsync();
////            return accountInfo.BuyingPower;
////        }

////        // Fetch the total investment value (positions only) from the database
////        public async Task<decimal> GetInvestmentValueAsync()
////        {
////            var positions = await DbContext.Positions
////                .Where(p => p.Strategy == StrategyName && p.Broker == Broker.GetType().Name)
////                .ToListAsync();

////            return positions.Sum(p => (decimal)(p.Quantity * p.LatestPrice));
////        }

////        // Calculate total portfolio value (cash + investments)
////        public async Task<decimal> GetPortfolioValueAsync()
////        {
////            var cash = await GetCashAsync();
////            var investments = await GetInvestmentValueAsync();
////            return cash + investments;
////        }



////        // Helper method: Calculate target cash and investment allocations
////        protected (decimal TargetCash, decimal TargetInvestment) CalculateTargetBalances(decimal totalPortfolioValue, decimal cashPercentage)
////        {
////            var targetCash = totalPortfolioValue * cashPercentage;
////            var targetInvestment = totalPortfolioValue - targetCash;

////            Logger.LogDebug("Target balances calculated for strategy {StrategyName}: Cash={TargetCash:C}, Investment={TargetInvestment:C}",
////                StrategyName, targetCash, targetInvestment);

////            return (targetCash, targetInvestment);
////        }


////    public abstract   Task<decimal?> ShouldOwnAsync(string symbol, decimal currentPrice);
////    }
////}