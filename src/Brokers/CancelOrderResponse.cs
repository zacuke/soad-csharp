 
namespace soad_csharp.Brokers;

public class CancelOrderResponse
{
    public string OrderId { get; set; }
    public string Status { get; set; } // For example: "Canceled"
}