 
namespace soad_csharp.Database;
public class Position
{
    public int Id { get; set; }
    public string Broker { get; set; }
    public string Strategy { get; set; }
    public int? BalanceId { get; set; } // Foreign key, nullable for situations where no balance is associated
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal LatestPrice { get; set; }
    public decimal? CostBasis { get; set; } // Nullable
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public decimal? UnderlyingVolatility { get; set; } // Nullable
    public decimal? UnderlyingLatestPrice { get; set; } // Nullable

    // Navigation property for the related Balance
    public Balance Balance { get; set; }
}