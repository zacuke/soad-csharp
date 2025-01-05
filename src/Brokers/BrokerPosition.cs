namespace soad_csharp.Brokers;

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