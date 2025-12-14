using Microsoft.EntityFrameworkCore;
using MyStockApp.Data.Models;

namespace MyStockApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<StockWatchlist> StockWatchlists { get; set; } = default!;
        public DbSet<Stock> Stocks { get; set; } = default!;
        public DbSet<StockPriceHistory> StockPriceHistories { get; set; } = default!;
        public DbSet<Order> Orders { get; set; } = default!;
        public DbSet<Trade> Trades { get; set; } = default!;
        public DbSet<Portfolio> Portfolios { get; set; } = default!;
        public DbSet<UserSettings> UserSettings { get; set; } = default!;
        public DbSet<AuditLog> AuditLogs { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StockWatchlist>(entity =>
            {
                entity.HasIndex(e => e.StockSymbol).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<Stock>(entity =>
            {
                entity.HasIndex(e => e.Symbol).IsUnique();
                entity.Property(e => e.Market).HasConversion<int>();
                entity.Property(e => e.CurrentPrice).HasPrecision(18, 4);
                entity.Property(e => e.OpenPrice).HasPrecision(18, 4);
                entity.Property(e => e.HighPrice).HasPrecision(18, 4);
                entity.Property(e => e.LowPrice).HasPrecision(18, 4);
                entity.Property(e => e.LastUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<StockPriceHistory>(entity =>
            {
                entity.HasIndex(e => new { e.StockId, e.Date });
                entity.Property(e => e.OpenPrice).HasPrecision(18, 4);
                entity.Property(e => e.HighPrice).HasPrecision(18, 4);
                entity.Property(e => e.LowPrice).HasPrecision(18, 4);
                entity.Property(e => e.ClosePrice).HasPrecision(18, 4);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasIndex(e => new { e.StockId, e.Status });
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.Side).HasConversion<int>();
                entity.Property(e => e.Type).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();

                entity.Property(e => e.Price).HasPrecision(18, 4);
                entity.Property(e => e.Commission).HasPrecision(18, 4);
                entity.Property(e => e.TransactionTax).HasPrecision(18, 4);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<Trade>(entity =>
            {
                entity.HasIndex(e => e.ExecutedAt);
                entity.HasIndex(e => e.StockSymbol);

                entity.Property(e => e.Side).HasConversion<int>();
                entity.Property(e => e.ExecutedPrice).HasPrecision(18, 4);
                entity.Property(e => e.TotalAmount).HasPrecision(18, 4);
                entity.Property(e => e.Commission).HasPrecision(18, 4);
                entity.Property(e => e.TransactionTax).HasPrecision(18, 4);
                entity.Property(e => e.NetAmount).HasPrecision(18, 4);

                entity.Property(e => e.ExecutedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<Portfolio>(entity =>
            {
                entity.HasIndex(e => e.StockId).IsUnique();

                entity.Property(e => e.AverageCost).HasPrecision(18, 4);
                entity.Property(e => e.TotalCost).HasPrecision(18, 4);
                entity.Property(e => e.RealizedPnL).HasPrecision(18, 4);

                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Stock)
                    .WithOne(e => e.Portfolio)
                    .HasForeignKey<Portfolio>(e => e.StockId);
            });

            modelBuilder.Entity<UserSettings>(entity =>
            {
                entity.Property(e => e.CommissionDiscount).HasPrecision(18, 4);
                entity.Property(e => e.MaxTradeAmount).HasPrecision(18, 4);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.EntityType);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            UpdateTimestamps();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<StockWatchlist>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }

            foreach (var entry in ChangeTracker.Entries<Stock>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.LastUpdated = now;
                }
            }

            foreach (var entry in ChangeTracker.Entries<Order>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }

            foreach (var entry in ChangeTracker.Entries<Portfolio>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }

            foreach (var entry in ChangeTracker.Entries<AuditLog>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                }
            }

            foreach (var entry in ChangeTracker.Entries<Trade>())
            {
                if (entry.State == EntityState.Added && entry.Entity.ExecutedAt == default)
                {
                    entry.Entity.ExecutedAt = now;
                }
            }
        }
    }
}
