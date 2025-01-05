namespace soad_csharp.Database;

public class Balance
{
    public int Id { get; set; }
    public string Broker { get; set; }
    public string Strategy { get; set; }
    public string Type { get; set; } // 'Cash' or 'Positions'

    public decimal BalanceValue { get; set; } = 0.0M; // Renamed to avoid conflict with class name
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation property for related Positions
    public ICollection<Position> Positions { get; set; }
}