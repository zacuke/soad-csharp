using soad_csharp.database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace soad_csharp.brokers;

public interface IBroker
{
    // Establishes a connection
    void Connect();

    // Retrieves account information
    public Task<AccountInfo> GetAccountInfoAsync();

    // Retrieves positions for the account
    public Task<List<BrokerPosition>> GetPositionsAsync();

    // Places a stock order
    public Task<OrderResponse> PlaceOrderAsync(
        string symbol,
        decimal quantity,
        string side,
        decimal price ,
        string orderType,
        string timeInForce, 
        string clientOrderId);

    // Places an option order
    public Task<OrderResponse> PlaceOptionOrderAsync(
        string symbol,
        decimal quantity,
        string side,
        string optionType,
        decimal strikePrice,
        string expirationDate,
        decimal price ,
        string orderType,
        string timeInForce);

    // Retrieves the status of an order
    //public Task<OrderStatus> GetOrderStatusAsync(string orderId);

    // Cancels an order
    public Task<CancelOrderResponse> CancelOrderAsync(string orderId);

    // Retrieves the current price of a symbol (async)
    public Task<decimal> GetCurrentPriceAsync(string symbol, AssetType assetType);

    // Retrieves the bid and ask prices of a symbol
    public Task<BidAsk> GetBidAskAsync(string symbol, AssetType assetType);

    public Task<OrderResponse> GetExistingOrderAsync(string responseId = null, string clientOrderId = null);
    public Task<List<OrderResponse>> GetOrdersAsync(string status);
}

public class AccountInfo
{
    public string AccountId { get; set; }
    public string AccountStatus { get; set; }
    public decimal PortfolioValue { get; set; }
    public int BuyingPower { get; set; }
}

public class BidAsk
{
    public decimal? BidPrice { get; set; }
    public decimal? AskPrice { get; set; }
}

public class CancelOrderResponse
{
    public string OrderId { get; set; }
    public string Status { get; set; } // For example: "Canceled"
}

public class OrderResponse
{
    public string OrderId { get; set; }
    public string Status { get; set; }
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public string Side { get; set; }
    public string OrderType { get; set; }
    public decimal? LimitPrice { get; set; }
    public string TimeInForce { get; set; }
    public string BrokerResponseId { get; set; }
    public string ClientOrderId { get; set; }
    public string BrokerResponseAssetId { get; set; }
    public string BrokerResponseAssetClass { get; set; }

    public decimal? BrokerResponseFilledQty { get; set; }
}

public class OrderStatus
{
    public string OrderId { get; set; }
    public string Status { get; set; }
    public string Symbol { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal? RemainingQuantity { get; set; }
    public decimal? Quantity { get; set; }
}

public class BrokerPosition
{
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal MarketValue { get; set; }
    public AssetType AssetType { get; set; }
    public decimal CostBasis { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal AverageEntryPrice { get; set; }
}
public enum AssetType
{
    Stock,
    Option,
    Crypto,
    Cash
}


public class AssetAllocation
{
    public string Symbol { get; set; }
    public decimal Allocation { get; set; }
    public AssetType AssetType { get; set; }
    public decimal StartingCapital { get; set; }

    public decimal? CurrentPrice { get; set; }

    public decimal DesiredAllocationValue => StartingCapital * Allocation;
    public decimal DesiredAllocationQuantity => (DesiredAllocationValue / CurrentPrice) ?? throw new Exception("Set CurrentPrice First");

}

public class TradeRequest
{
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public string Side { get; set; } // Buy or Sell
    public decimal? Price { get; set; }
    public string OrderType { get; set; } 
    public string TimeInForce { get; set; } 
    public AssetType AssetType { get; set; }

    // Optional: Priority or metadata for managing/processing trades
    public int Priority { get; set; } = 0;

    public override string ToString()
    {
        return $"TradeRequest: {Side} {Quantity} of {Symbol} @ {Price?.ToString() ?? "Market"} ({OrderType}), Priority: {Priority}";
    }
}