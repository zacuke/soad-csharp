using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using soad_csharp.database;
using soad_csharp.brokers;
using Microsoft.Extensions.Logging.Abstractions;

namespace soad_csharp.strategies
{
    public abstract class BaseStrategy(
        IBroker broker,
        TradeDbContext dbContext,
        string strategyName,
        decimal startingCapital,
        int rebalanceIntervalMinutes = 5,
        string executionStyle = "",
        ILogger logger = null)
    {
        protected readonly IBroker Broker = broker ?? throw new ArgumentNullException(nameof(broker));
        protected readonly TradeDbContext DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        protected readonly string StrategyName = strategyName;
        protected readonly decimal StartingCapital = startingCapital;
        protected readonly int RebalanceIntervalMinutes = rebalanceIntervalMinutes;
        protected readonly string ExecutionStyle = executionStyle;
        bool IsInitialized;

        protected readonly ILogger Logger = logger ?? NullLogger.Instance;

        // Abstract method that derived strategies must implement
        public abstract Task RebalanceAsync();

        // Initialize starting balance in the database
        public async Task InitializeStartingBalanceAsync()
        {
            if (IsInitialized)
            {
                Logger.LogDebug("Starting balance already initialized for strategy {StrategyName}", StrategyName);
                return;
            }

            Logger.LogDebug("Initializing starting balance for strategy {StrategyName}", StrategyName);

            // Fetch broker account information
            var accountInfo = await Broker.GetAccountInfoAsync();
            if (accountInfo.BuyingPower < StartingCapital)
            {
                Logger.LogError(
                    "Not enough buying power. Required: {Required}, Available: {Available} for strategy {StrategyName}",
                    StartingCapital, accountInfo.BuyingPower, StrategyName);
                throw new InvalidOperationException("Insufficient funds to initialize strategy.");
            }

            // Check if the strategy balance already exists in the database
            var existingBalance = await DbContext.Balances
                .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
                .OrderByDescending(b => b.Timestamp)
                .FirstOrDefaultAsync();

            if (existingBalance == null)
            {
                // Add new balance record
                var balance = new Balance
                {
                    Broker = Broker.GetType().Name,
                    Strategy = StrategyName,
                    Type = "Cash",
                    BalanceValue = (float)StartingCapital,
                    Timestamp = DateTime.UtcNow
                };

                await DbContext.Balances.AddAsync(balance);
                await DbContext.SaveChangesAsync();
                Logger.LogInformation(
                    "Initialized starting balance for strategy {StrategyName} with {StartingCapital:C}",
                    StrategyName, StartingCapital);
            }
            else
            {
                Logger.LogInformation(
                    "Existing balance {BalanceValue:C} found for strategy {StrategyName}",
                    existingBalance.BalanceValue, StrategyName);
            }

            IsInitialized = true;
        }

        // Sync positions with the broker and database
        public async Task SyncPositionsAsync()
        {
            Logger.LogDebug("Syncing positions for strategy {StrategyName}", StrategyName);

            // Fetch positions from the broker
            var brokerPositions = await Broker.GetPositionsAsync();

            // Fetch database positions for the strategy
            var dbPositions = await DbContext.Positions
                .Where(p => p.Strategy == StrategyName && p.Broker == Broker.GetType().Name)
                .ToListAsync();

            foreach (var brokerPos in brokerPositions)
            {
                // Check if the symbol already exists in the database
                var dbPos = dbPositions.FirstOrDefault(p => p.Symbol == brokerPos.Symbol);

                if (dbPos == null)
                {
                    // Add new position to the database
                    var newPosition = new database.Position
                    {
                        Broker = Broker.GetType().Name,
                        Strategy = StrategyName,
                        Symbol = brokerPos.Symbol,
                        Quantity = (float)brokerPos.Quantity,
                        LatestPrice = (float)brokerPos.MarketValue,
                        LastUpdated = DateTime.UtcNow
                    };

                    await DbContext.Positions.AddAsync(newPosition);
                    Logger.LogInformation("Added new position for symbol {Symbol} in strategy {StrategyName}", brokerPos.Symbol, StrategyName);
                }
                else
                {
                    // Update existing position
                    dbPos.Quantity = (float)brokerPos.Quantity;
                    dbPos.LatestPrice = (float)brokerPos.MarketValue;
                    dbPos.LastUpdated = DateTime.UtcNow;
                    DbContext.Positions.Update(dbPos);
                    Logger.LogInformation("Updated position for symbol {Symbol} in strategy {StrategyName}", brokerPos.Symbol, StrategyName);
                }
            }

            // Save changes to the database
            await DbContext.SaveChangesAsync();

            Logger.LogDebug("Position sync completed for strategy {StrategyName}", StrategyName);
        }

        // Fetch the current cash balance from the database or broker
        public async Task<decimal> GetCashAsync()
        {
            // Get latest balance from the database
            var balance = await DbContext.Balances
                .Where(b => b.Strategy == StrategyName && b.Broker == Broker.GetType().Name && b.Type == "Cash")
                .OrderByDescending(b => b.Timestamp)
                .FirstOrDefaultAsync();

            if (balance != null)
            {
                return (decimal)balance.BalanceValue;
            }

            // As fallback, fetch cash directly from the broker
            var accountInfo = await Broker.GetAccountInfoAsync();
            return accountInfo.BuyingPower;
        }

        // Fetch the total investment value (positions only) from the database
        public async Task<decimal> GetInvestmentValueAsync()
        {
            var positions = await DbContext.Positions
                .Where(p => p.Strategy == StrategyName && p.Broker == Broker.GetType().Name)
                .ToListAsync();

            return positions.Sum(p => (decimal)(p.Quantity * p.LatestPrice));
        }

        // Calculate total portfolio value (cash + investments)
        public async Task<decimal> GetPortfolioValueAsync()
        {
            var cash = await GetCashAsync();
            var investments = await GetInvestmentValueAsync();
            return cash + investments;
        }

        // Place an order with the broker and record it in the trades table
        public async Task PlaceOrderAsync(
            string symbol,
            decimal quantity,
            string side,
            decimal? price = null,
            string orderType = "limit",
            string timeInForce = "day")
        {
            var response = await Broker.PlaceOrderAsync(symbol, quantity, side, price, orderType, timeInForce);

            // Save the trade details in the database
            var trade = new Trade
            {
                Broker = Broker.GetType().Name,
                Strategy = StrategyName,
                Symbol = symbol,
                Quantity = quantity,
                Price = price ?? 0 ,
                Side = side,
                Status = response.Status,
                Timestamp = DateTime.UtcNow,
                ExecutionStyle = ExecutionStyle
            };

            await DbContext.Trades.AddAsync(trade);
            await DbContext.SaveChangesAsync();

            Logger.LogInformation("Order placed for strategy {StrategyName}: {OrderDetails}", StrategyName, trade);
        }
 
        // Helper method: Calculate target cash and investment allocations
        protected (decimal TargetCash, decimal TargetInvestment) CalculateTargetBalances(decimal totalPortfolioValue, decimal cashPercentage)
        {
            var targetCash = totalPortfolioValue * cashPercentage;
            var targetInvestment = totalPortfolioValue - targetCash;

            Logger.LogDebug("Target balances calculated for strategy {StrategyName}: Cash={TargetCash:C}, Investment={TargetInvestment:C}",
                StrategyName, targetCash, targetInvestment);

            return (targetCash, targetInvestment);
        }
   

    public abstract   Task<decimal?> ShouldOwnAsync(string symbol, decimal currentPrice);
    }
}