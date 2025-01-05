namespace soad_csharp.Brokers;
public class TradeRequest
{
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public string Side { get; set; } // Buy or Sell
    public decimal Price { get; set; }
    public string OrderType { get; set; }
    public string TimeInForce { get; set; }
    public AssetType AssetType { get; set; }

    // Optional: Priority or metadata for managing/processing trades
    public int Priority { get; set; } = 0;

    public override string ToString()
    {
        return $"TradeRequest: {Side} {Quantity} of {Symbol} @ {Price.ToString() ?? "Market"} ({OrderType}), Priority: {Priority}";
    }
}