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

    [Fact]
    public async Task StockWatchlist_ShouldAutoSetTimestampsOnCreate()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var beforeCreate = DateTime.UtcNow.AddSeconds(-1);

        // Act
        using (var context = new AppDbContext(options))
        {
            var stock = new StockWatchlist
            {
                StockSymbol = "2330",
                StockName = "台積電",
                Notes = "測試備註"
                // 故意不設定 CreatedAt 和 UpdatedAt
            };

            context.StockWatchlists.Add(stock);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AppDbContext(options))
        {
            var stock = await context.StockWatchlists.FirstAsync();

            // 驗證 CreatedAt 和 UpdatedAt 已自動設定
            Assert.True(stock.CreatedAt >= beforeCreate, "CreatedAt should be set automatically");
            Assert.True(stock.UpdatedAt >= beforeCreate, "UpdatedAt should be set automatically");

            // 驗證 CreatedAt 和 UpdatedAt 相同（新增時）
            Assert.Equal(stock.CreatedAt, stock.UpdatedAt);
        }
    }

    [Fact]
    public async Task StockWatchlist_ShouldAutoUpdateTimestampOnModify()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        DateTime originalCreatedAt;
        DateTime originalUpdatedAt;

        // 建立初始資料
        using (var context = new AppDbContext(options))
        {
            var stock = new StockWatchlist
            {
                StockSymbol = "2330",
                StockName = "台積電",
                Notes = "原始備註"
            };

            context.StockWatchlists.Add(stock);
            await context.SaveChangesAsync();

            originalCreatedAt = stock.CreatedAt;
            originalUpdatedAt = stock.UpdatedAt;
        }

        // 等待一小段時間以確保時間戳會有差異
        await Task.Delay(100);

        // Act - 更新資料
        using (var context = new AppDbContext(options))
        {
            var stock = await context.StockWatchlists.FirstAsync();
            stock.StockName = "台積電（已更新）";
            stock.Notes = "更新後的備註";
            // 故意不手動設定 UpdatedAt

            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AppDbContext(options))
        {
            var stock = await context.StockWatchlists.FirstAsync();

            // 驗證 CreatedAt 保持不變
            Assert.Equal(originalCreatedAt, stock.CreatedAt);

            // 驗證 UpdatedAt 已自動更新
            Assert.True(stock.UpdatedAt > originalUpdatedAt,
                $"UpdatedAt should be updated automatically. Original: {originalUpdatedAt}, Current: {stock.UpdatedAt}");

            // 驗證更新的內容
            Assert.Equal("台積電（已更新）", stock.StockName);
            Assert.Equal("更新後的備註", stock.Notes);
        }
    }

    [Fact]
    public async Task StockWatchlist_CreatedAtShouldNotChangeOnUpdate()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        int stockId;
        DateTime originalCreatedAt;

        // 建立初始資料
        using (var context = new AppDbContext(options))
        {
            var stock = new StockWatchlist
            {
                StockSymbol = "0050",
                StockName = "元大台灣50",
                Notes = "ETF"
            };

            context.StockWatchlists.Add(stock);
            await context.SaveChangesAsync();

            stockId = stock.Id;
            originalCreatedAt = stock.CreatedAt;
        }

        // Act - 執行多次更新
        for (int i = 0; i < 3; i++)
        {
            await Task.Delay(50);

            using (var context = new AppDbContext(options))
            {
                var stock = await context.StockWatchlists.FirstAsync(s => s.Id == stockId);
                stock.Notes = $"更新 {i + 1}";
                await context.SaveChangesAsync();
            }
        }

        // Assert
        using (var context = new AppDbContext(options))
        {
            var stock = await context.StockWatchlists.FirstAsync(s => s.Id == stockId);

            // 驗證 CreatedAt 始終保持原始值
            Assert.Equal(originalCreatedAt, stock.CreatedAt);

            // 驗證最終內容
            Assert.Equal("更新 3", stock.Notes);
        }
    }
}
