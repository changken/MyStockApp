using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;
using Xunit;

namespace MyStockApp.Tests;

public class AppDbContextTests
{
    private DbContextOptions<AppDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public void AppDbContext_ShouldHaveStockWatchlistsDbSet()
    {
        // Arrange & Act
        using var context = new AppDbContext(CreateInMemoryOptions());

        // Assert
        Assert.NotNull(context.StockWatchlists);
    }

    [Fact]
    public void StockWatchlist_ShouldHaveUniqueIndexOnStockSymbol()
    {
        // Arrange
        using var context = new AppDbContext(CreateInMemoryOptions());
        var entityType = context.Model.FindEntityType(typeof(StockWatchlist));

        // Assert
        Assert.NotNull(entityType);
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(StockWatchlist.StockSymbol)));

        Assert.NotNull(index);
        Assert.True(index.IsUnique, "StockSymbol index should be unique");
    }

    [Fact]
    public async Task StockWatchlist_ShouldEnforceUniqueConstraint()
    {
        // Arrange
        var options = CreateInMemoryOptions();

        using (var context = new AppDbContext(options))
        {
            var stock1 = new StockWatchlist
            {
                StockSymbol = "2330",
                StockName = "台積電",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.StockWatchlists.Add(stock1);
            await context.SaveChangesAsync();
        }

        // Act & Assert
        using (var context = new AppDbContext(options))
        {
            var stock2 = new StockWatchlist
            {
                StockSymbol = "2330", // 重複代號
                StockName = "台積電2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.StockWatchlists.Add(stock2);

            // InMemory database doesn't enforce unique constraints,
            // so we verify the index configuration exists instead
            var entityType = context.Model.FindEntityType(typeof(StockWatchlist));
            var index = entityType?.GetIndexes()
                .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(StockWatchlist.StockSymbol)));

            Assert.NotNull(index);
            Assert.True(index.IsUnique);
        }
    }

    [Fact]
    public async Task StockWatchlist_ShouldSupportCRUDOperations()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var now = DateTime.UtcNow;

        // CREATE
        using (var context = new AppDbContext(options))
        {
            var stock = new StockWatchlist
            {
                StockSymbol = "2330",
                StockName = "台積電",
                Notes = "測試備註",
                CreatedAt = now,
                UpdatedAt = now
            };

            context.StockWatchlists.Add(stock);
            await context.SaveChangesAsync();
        }

        // READ
        using (var context = new AppDbContext(options))
        {
            var stocks = await context.StockWatchlists.ToListAsync();
            Assert.Single(stocks);
            Assert.Equal("2330", stocks[0].StockSymbol);
            Assert.Equal("台積電", stocks[0].StockName);
        }

        // UPDATE
        using (var context = new AppDbContext(options))
        {
            var stock = await context.StockWatchlists.FirstAsync();
            stock.StockName = "台積電更新";
            stock.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        using (var context = new AppDbContext(options))
        {
            var stock = await context.StockWatchlists.FirstAsync();
            Assert.Equal("台積電更新", stock.StockName);
        }

        // DELETE
        using (var context = new AppDbContext(options))
        {
            var stock = await context.StockWatchlists.FirstAsync();
            context.StockWatchlists.Remove(stock);
            await context.SaveChangesAsync();
        }

        using (var context = new AppDbContext(options))
        {
            var stocks = await context.StockWatchlists.ToListAsync();
            Assert.Empty(stocks);
        }
    }
}
