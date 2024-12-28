using soad_csharp.database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace soad_csharp.brokers;

interface IBroker
{
    // Establishes a connection
    void Connect();

    // Retrieves account information
    Task<AccountInfo> GetAccountInfoAsync();

    // Retrieves positions for the account
    Task<List<Position>> GetPositionsAsync();

    // Places a stock order
    Task<OrderResponse> PlaceOrderAsync(
        string symbol,
        int quantity,
        string side,
        decimal? price = null,
        string orderType = "limit",
        string timeInForce = "day");

    // Places an option order
    Task<OrderResponse> PlaceOptionOrderAsync(
        string symbol,
        int quantity,
        string side,
        string optionType,
        decimal strikePrice,
        string expirationDate,
        decimal? price = null,
        string orderType = "limit",
        string timeInForce = "day");

    // Retrieves the status of an order
    Task<OrderStatus> GetOrderStatusAsync(string orderId);

    // Cancels an order
    Task<CancelOrderResponse> CancelOrderAsync(string orderId);

    // Retrieves the current price of a symbol (async)
    Task<decimal?> GetCurrentPriceAsync(string symbol);

    // Retrieves the bid and ask prices of a symbol
    Task<BidAsk> GetBidAskAsync(string symbol);

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
    public decimal? Quantity { get; set; }
    public string Side { get; set; }
    public string OrderType { get; set; }
    public decimal? LimitPrice { get; set; }
    public string TimeInForce { get; set; }
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

public class Position
{
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal MarketValue { get; set; }
}
