namespace soad_csharp.Brokers;

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

    // Cancels an order
    public Task<CancelOrderResponse> CancelOrderAsync(string orderId);

    // Retrieves the current price of a symbol (async)
    public Task<decimal> GetCurrentPriceAsync(string symbol, AssetType assetType);

    // Retrieves the bid and ask prices of a symbol
    public Task<BidAsk> GetBidAskAsync(string symbol, AssetType assetType);

    public Task<OrderResponse> GetExistingOrderAsync(string responseId = null, string clientOrderId = null);
    public Task<List<OrderResponse>> GetOrdersAsync(string status);
}
