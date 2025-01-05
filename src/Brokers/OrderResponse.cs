 
namespace soad_csharp.Brokers;

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
