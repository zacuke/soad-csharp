using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using soad_csharp.Brokers;
using soad_csharp.Database;
using soad_csharp.Extensions;
using System.Diagnostics;
using BrokerPosition = soad_csharp.Brokers.BrokerPosition;

namespace soad_csharp.Strategies.Abstract;
public abstract class SimpleStrategy(ILogger _logger, IBroker _broker, TradeDbContext _dbContext) 
{
    //    // Abstract properties and methods for derived strategies
    public abstract string StrategyName { get; }
    public abstract decimal StartingCapital { get; }
    public abstract decimal ThresholdCapital { get; }
    public abstract List<BrokerPosition> BrokerPositions { get; }
    public abstract List<TradeRequest> TradeRequests { get; }
    public abstract decimal BrokerTotalValue { get; }


    // public abstract decimal StartingCapital { get; }
    public abstract Task Execute();
  
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
    protected void PostValidateAllocations(List<AssetAllocation> allocations )
    {
        decimal checkAllocations = 0;
        foreach (var b in allocations)
        {
            checkAllocations += b.DesiredAllocationValue;
        }
        if (checkAllocations != StartingCapital)
            throw new Exception("Problem calculation allocation");

    }
    public decimal PortfolioValue(List<AssetAllocation> allocations)
    {
        var result = allocations.Select(allocation =>
        {
            var position = BrokerPositions.GetBrokerPositionsWhere(allocation.Symbol); // Normalize symbol
            return position.MarketValue;
        })
        .Sum();
        _logger.LogInformation("Existing allocated portfolio value: {portfolioValue:C} for strategy {StrategyName}", result, StrategyName);

        // If portfolio value is zero (no positions to rebalance), exit early
        if (result == 0)
        {
            throw new Exception("Portfolio value is zero. Skipping rebalancing.");
            
        }
        return result;
    }

    public async Task PlaceOrderAsync(string symbol, decimal quantity, string side, decimal price, string orderType, string timeInForce)
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


    public async Task<bool> IsStrategyInitializedAsync( )
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
                BalanceValue = StartingCapital,
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
                BalanceValue = Math.Round(BrokerTotalValue, 2),
                Timestamp = DateTime.UtcNow
            };

            await _dbContext.Balances.AddAsync(balance);
        }
        await _dbContext.SaveChangesAsync();

        return is_initialized;
    }

    public async Task  CheckUnfilledTrades()
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

        var checkUnfilled = unfilledTradesFromBroker.Count > 0 
                || await (from a in _dbContext.Trades
                       where a.Strategy == StrategyName
                         && a.IsFilled == false
                       select a).AnyAsync() ;

        if (checkUnfilled){
            throw new Exception($"Open or unfilled trades found, so not running strategy {StrategyName}" );
             
        }

    }

    public async Task PurchaseAllocation(List<AssetAllocation> allocations    )
    {

        //increase our holdings the first time if we don't have enough assets
        if (BrokerTotalValue < ThresholdCapital)
        {
            foreach (var b in  allocations)
            {
                var stockPrice = await _broker.GetCurrentPriceAsync(b.Symbol, b.AssetType);
                b.CurrentPrice = stockPrice;

            }

            PostValidateAllocations( allocations );

            foreach (var b in  allocations)
            {
                var currentHoldingQuantity = BrokerPositions.GetBrokerPositionsWhere(b.Symbol).Quantity;

                if (b.DesiredAllocationQuantity > currentHoldingQuantity)
                {
                    var quantityToBuy = b.DesiredAllocationQuantity - currentHoldingQuantity;
                    decimal price = b.CurrentPrice ?? throw new Exception("unexpected null currentprice");
                    await PlaceOrderAsync(
                        symbol: b.Symbol,
                        quantity: quantityToBuy,
                        side: "buy",
                        price: price,
                        orderType: "market",
                        timeInForce: "gtc"
                    );

                }
            }
        }
    }
 
 
    public abstract Task RunStrategy();
    public async Task ExecuteTrades()
    {

        // Execute all accumulated trades
        if (TradeRequests.Count != 0)
        {
            _logger.LogInformation("Executing {Count} for strategy {StrategyName}",  TradeRequests.Count, StrategyName);

            foreach (var trade in TradeRequests)
            {
                await PlaceOrderAsync(
                    symbol: trade.Symbol,
                    quantity: trade.Quantity,
                    side: trade.Side,
                    price: trade.Price,
                    orderType: trade.OrderType,
                    timeInForce: trade.TimeInForce
                );
            }
        }
       
    }
}
