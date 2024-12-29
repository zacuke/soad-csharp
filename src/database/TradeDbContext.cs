using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace soad_csharp.database;
public class Trade
{
    public int Id { get; set; }
    public int? BrokerId { get; set; } // Nullable
    public string Symbol { get; set; }
    public int Quantity { get; set; }
    public float Price { get; set; }
    public float? ExecutedPrice { get; set; } // Nullable
    public string Side { get; set; }
    public string Status { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Broker { get; set; }
    public string Strategy { get; set; }
    public float? ProfitLoss { get; set; } // Nullable
    public string Success { get; set; }
    public string ExecutionStyle { get; set; }
}

public class AccountInfo
{
    public int Id { get; set; }
    public string Broker { get; set; }
    public float Value { get; set; }
}

public class Balance
{
    public int Id { get; set; }
    public string Broker { get; set; }
    public string Strategy { get; set; }
    public string Type { get; set; } // 'Cash' or 'Positions'
    
    public float BalanceValue { get; set; } = 0.0F; // Renamed to avoid conflict with class name
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation property for related Positions
    public ICollection<Position> Positions { get; set; }
}

public class Position
{
    public int Id { get; set; }
    public string Broker { get; set; }
    public string Strategy { get; set; }
    public int? BalanceId { get; set; } // Foreign key, nullable for situations where no balance is associated
    public string Symbol { get; set; }
    public float Quantity { get; set; }
    public float LatestPrice { get; set; }
    public float? CostBasis { get; set; } // Nullable
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public float? UnderlyingVolatility { get; set; } // Nullable
    public float? UnderlyingLatestPrice { get; set; } // Nullable

    // Navigation property for the related Balance
    public Balance Balance { get; set; }
}

public class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    public DbSet<Trade> Trades { get; set; }
    public DbSet<AccountInfo> AccountInfos { get; set; }
    public DbSet<Balance> Balances { get; set; }
    public DbSet<Position> Positions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Trade entity configuration
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Symbol).IsRequired();
            entity.Property(t => t.Quantity).IsRequired();
            entity.Property(t => t.Price).IsRequired();
            entity.Property(t => t.Side).IsRequired();
            entity.Property(t => t.Status).IsRequired();
            entity.Property(t => t.Timestamp);//.HasDefaultValue("GETUTCDATE()");
        });

        // AccountInfo entity configuration
        modelBuilder.Entity<AccountInfo>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Broker).IsRequired().HasMaxLength(255);
            entity.HasIndex(a => a.Broker).IsUnique(); // Unique constraint
        });

        // Balance entity configuration
        modelBuilder.Entity<Balance>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Broker).IsRequired();
            entity.Property(b => b.Type).IsRequired();
            entity.Property(b => b.Timestamp);//.HasDefaultValue("GETUTCDATE()");
            entity.Property(entity => entity.BalanceValue).HasColumnName("Balance"); // Rename column
            // Create indexes
            //entity.HasIndex(b => new { b.Broker, b.Strategy, b.Timestamp }).HasDatabaseName("ix_broker_strategy_timestamp");
            //  entity.HasIndex(b => new { b.Type, b.Timestamp }).HasDatabaseName("ix_type_timestamp");
        });

        // Position entity configuration
        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Broker).IsRequired();
            entity.Property(p => p.Symbol).IsRequired();
            entity.Property(p => p.Quantity).IsRequired();
            entity.Property(p => p.LatestPrice).IsRequired().HasColumnName("Latest_Price");
            entity.Property(p => p.UnderlyingLatestPrice).HasColumnName("Underlying_Latest_Price");
            entity.Property(p => p.UnderlyingVolatility).HasColumnName("Underlying_Volatility");
            entity.Property(p => p.LastUpdated).HasColumnName("Last_Updated");//.HasDefaultValue("GETUTCDATE()");
            entity.Property(p => p.BalanceId).HasColumnName("Balance_Id");
            entity.Property(p => p.CostBasis).HasColumnName("Cost_Basis");
            // Relationship with Balance
            entity.HasOne(p => p.Balance)
                  .WithMany(b => b.Positions)
                  .HasForeignKey(p => p.BalanceId)
                  .OnDelete(DeleteBehavior.SetNull); // Set foreign key to null on delete
        });
    }
}
