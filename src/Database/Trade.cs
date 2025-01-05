namespace soad_csharp.Database;
public class Trade
{
    public int Id { get; set; }
    public int? BrokerId { get; set; } // Nullable
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? ExecutedPrice { get; set; } // Nullable
    public string Side { get; set; }
    public string Status { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Broker { get; set; }
    public string Strategy { get; set; }
    public decimal? ProfitLoss { get; set; } // Nullable
    public string Success { get; set; }
    public string ExecutionStyle { get; set; }

    public string BrokerResponseId { get; set; }
    public string ClientOrderId { get; set; }
    public string BrokerResponseAssetId { get; set; }
    public string BrokerResponseAssetClass { get; set; }

    public decimal? BrokerResponseFilledQty { get; set; }

    public bool IsFilled { get; set; }
}
