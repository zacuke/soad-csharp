namespace soad_csharp.Brokers;

public class OrderStatus
{
    public string OrderId { get; set; }
    public string Status { get; set; }
    public string Symbol { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal? RemainingQuantity { get; set; }
    public decimal? Quantity { get; set; }
}
