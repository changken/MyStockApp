using Microsoft.EntityFrameworkCore;
using MyStockApp.Data.Models;

namespace MyStockApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<StockWatchlist> StockWatchlists { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StockWatchlist>(entity =>
            {
                // 唯一索引: StockSymbol
                entity.HasIndex(e => e.StockSymbol).IsUnique();

                // 預設值: CreatedAt
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 預設值: UpdatedAt
                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }
}
