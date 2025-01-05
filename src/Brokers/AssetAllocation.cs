namespace soad_csharp.Brokers;

public class AssetAllocation
{
    public string Symbol { get; set; }
    public decimal Allocation { get; set; }
    public AssetType AssetType { get; set; }
    public decimal StartingCapital { get; set; }

    public decimal? CurrentPrice { get; set; }

    public decimal DesiredAllocationValue => StartingCapital * Allocation;
    public decimal DesiredAllocationQuantity => (DesiredAllocationValue / CurrentPrice) ?? throw new Exception("Set CurrentPrice First");

}
