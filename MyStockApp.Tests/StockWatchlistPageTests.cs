using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyStockApp.Components.Pages;
using MyStockApp.Data;
// using MyStockApp.Data.Models; // Removed to avoid ambiguity
using Xunit;
using StockWatchlistEntity = MyStockApp.Data.Models.StockWatchlist; // Alias for entity

namespace MyStockApp.Tests;

public class StockWatchlistPageTests : TestContext
{
    private DbContextOptions<AppDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public void StockWatchlistPage_ShouldRenderWithLoadingIndicator()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(options));

        // Act
        var cut = RenderComponent<MyStockApp.Components.Pages.StockWatchlist>();

        // Assert
        Assert.Contains("股票追蹤清單", cut.Markup);
    }

    [Fact]
    public void StockWatchlistPage_ShouldShowEmptyMessage_WhenNoStocks()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(options));

        // Act
        var cut = RenderComponent<MyStockApp.Components.Pages.StockWatchlist>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("目前無追蹤股票", cut.Markup);
    }

    [Fact]
    public async Task StockWatchlistPage_ShouldDisplayStocks_WhenStocksExist()
    {
        // Arrange
        var options = CreateInMemoryOptions();

        // 預先填入測試資料
        using (var context = new AppDbContext(options))
        {
            context.StockWatchlists.AddRange(
                new StockWatchlistEntity
                {
                    StockSymbol = "2330",
                    StockName = "台積電",
                    Notes = "半導體龍頭",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new StockWatchlistEntity
                {
                    StockSymbol = "2317",
                    StockName = "鴻海",
                    Notes = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(options));

        // Act
        var cut = RenderComponent<MyStockApp.Components.Pages.StockWatchlist>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("2330", cut.Markup);
        Assert.Contains("台積電", cut.Markup);
        Assert.Contains("半導體龍頭", cut.Markup);
        Assert.Contains("2317", cut.Markup);
        Assert.Contains("鴻海", cut.Markup);
        Assert.DoesNotContain("目前無追蹤股票", cut.Markup);
    }

    [Fact]
    public async Task StockWatchlistPage_ShouldOrderByCreatedAtDescending()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var oldDate = DateTime.UtcNow.AddDays(-2);
        var newDate = DateTime.UtcNow;

        using (var context = new AppDbContext(options))
        {
            context.StockWatchlists.AddRange(
                new StockWatchlistEntity
                {
                    StockSymbol = "0050",
                    StockName = "元大台灣50",
                    CreatedAt = oldDate,
                    UpdatedAt = oldDate
                },
                new StockWatchlistEntity
                {
                    StockSymbol = "2330",
                    StockName = "台積電",
                    CreatedAt = newDate,
                    UpdatedAt = newDate
                }
            );
            await context.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(options));

        // Act
        var cut = RenderComponent<MyStockApp.Components.Pages.StockWatchlist>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert - 2330 應該出現在 0050 之前（最新在前）
        var markup = cut.Markup;
        var index2330 = markup.IndexOf("2330");
        var index0050 = markup.IndexOf("0050");
        Assert.True(index2330 < index0050, "最新股票應該顯示在前面");
    }

    [Fact]
    public async Task StockWatchlistPage_ShouldHaveEditAndDeleteButtons_WhenStocksExist()
    {
        // Arrange
        var options = CreateInMemoryOptions();

        // 預先填入測試資料
        using (var context = new AppDbContext(options))
        {
            context.StockWatchlists.Add(new StockWatchlistEntity
            {
                StockSymbol = "2330",
                StockName = "台積電",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(options));

        // Act
        var cut = RenderComponent<MyStockApp.Components.Pages.StockWatchlist>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        // 確認頁面中有編輯和刪除按鈕（即使 disabled）
        Assert.Contains("編輯", cut.Markup);
        Assert.Contains("刪除", cut.Markup);
    }

    // Helper class for testing
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }
    }
}
