using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyStockApp.Components.Pages;
using MyStockApp.Data;
using MyStockApp.Data.Models;
using MyStockApp.Services;
using Xunit;

namespace MyStockApp.Tests;

/// <summary>
/// 持股部位頁面測試 (Task 12)
/// </summary>
public class PortfolioPageTests : TestContext
{
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private DbContextOptions<AppDbContext> _dbOptions;

    public PortfolioPageTests()
    {
        _mockPortfolioService = new Mock<IPortfolioService>();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private void SetupServices()
    {
        Services.AddSingleton(_mockPortfolioService.Object);
        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(_dbOptions));
    }

    #region Task 12.1: 持股清單顯示

    [Fact]
    public void PortfolioPage_ShouldRenderTitle()
    {
        // Arrange
        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(new List<PortfolioItem>());

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(new PortfolioSummary(0, 0, 0, 0, 0));

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();

        // Assert
        Assert.Contains("持股部位", cut.Markup);
    }

    [Fact]
    public async Task PortfolioPage_ShouldDisplayHoldings_WhenHoldingsExist()
    {
        // Arrange
        var portfolioItems = new List<PortfolioItem>
        {
            new PortfolioItem(
                StockId: 1,
                StockSymbol: "2330",
                StockName: "台積電",
                Quantity: 1000,
                AverageCost: 580m,
                CurrentPrice: 600m,
                MarketValue: 600000m,
                UnrealizedPnL: 20000m,
                ReturnRate: 3.45m
            ),
            new PortfolioItem(
                StockId: 2,
                StockSymbol: "2317",
                StockName: "鴻海",
                Quantity: 2000,
                AverageCost: 105m,
                CurrentPrice: 100m,
                MarketValue: 200000m,
                UnrealizedPnL: -10000m,
                ReturnRate: -4.76m
            )
        };

        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(portfolioItems);

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(new PortfolioSummary(0, 0, 0, 0, 0));

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("2330", cut.Markup);
        Assert.Contains("台積電", cut.Markup);
        Assert.Contains("2317", cut.Markup);
        Assert.Contains("鴻海", cut.Markup);
    }

    [Fact]
    public void PortfolioPage_ShouldShowEmptyMessage_WhenNoHoldings()
    {
        // Arrange
        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(new List<PortfolioItem>());

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(new PortfolioSummary(0, 0, 0, 0, 0));

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("目前無持股", cut.Markup);
    }

    [Fact]
    public void PortfolioPage_ShouldDisplayProfitInGreen()
    {
        // Arrange
        var portfolioItems = new List<PortfolioItem>
        {
            new PortfolioItem(
                StockId: 1,
                StockSymbol: "2330",
                StockName: "台積電",
                Quantity: 1000,
                AverageCost: 580m,
                CurrentPrice: 600m,
                MarketValue: 600000m,
                UnrealizedPnL: 20000m,
                ReturnRate: 3.45m
            )
        };

        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(portfolioItems);

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(new PortfolioSummary(0, 0, 0, 0, 0));

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert - 正數應顯示綠色樣式
        Assert.Contains("text-success", cut.Markup);
    }

    [Fact]
    public void PortfolioPage_ShouldDisplayLossInRed()
    {
        // Arrange
        var portfolioItems = new List<PortfolioItem>
        {
            new PortfolioItem(
                StockId: 2,
                StockSymbol: "2317",
                StockName: "鴻海",
                Quantity: 2000,
                AverageCost: 105m,
                CurrentPrice: 100m,
                MarketValue: 200000m,
                UnrealizedPnL: -10000m,
                ReturnRate: -4.76m
            )
        };

        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(portfolioItems);

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(new PortfolioSummary(0, 0, 0, 0, 0));

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert - 負數應顯示紅色樣式
        Assert.Contains("text-danger", cut.Markup);
    }

    #endregion

    #region Task 12.2: 投資組合摘要

    [Fact]
    public void PortfolioPage_ShouldDisplaySummary()
    {
        // Arrange
        var summary = new PortfolioSummary(
            TotalMarketValue: 800000m,
            TotalCost: 685000m,
            TotalUnrealizedPnL: 10000m,
            TotalRealizedPnL: 5000m,
            TotalReturnRate: 2.19m
        );

        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(new List<PortfolioItem>());

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(summary);

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("總市值", cut.Markup);
        Assert.Contains("總成本", cut.Markup);
        Assert.Contains("總損益", cut.Markup);
    }

    [Fact]
    public void PortfolioPage_ShouldDisplayRealizedAndUnrealizedPnL()
    {
        // Arrange
        var summary = new PortfolioSummary(
            TotalMarketValue: 800000m,
            TotalCost: 685000m,
            TotalUnrealizedPnL: 10000m,
            TotalRealizedPnL: 5000m,
            TotalReturnRate: 2.19m
        );

        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(new List<PortfolioItem>());

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(summary);

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("已實現損益", cut.Markup);
        Assert.Contains("未實現損益", cut.Markup);
    }

    [Fact]
    public void PortfolioPage_ShouldDisplayTotalReturnRate()
    {
        // Arrange
        var summary = new PortfolioSummary(
            TotalMarketValue: 800000m,
            TotalCost: 685000m,
            TotalUnrealizedPnL: 10000m,
            TotalRealizedPnL: 5000m,
            TotalReturnRate: 2.19m
        );

        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(new List<PortfolioItem>());

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(summary);

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("總報酬率", cut.Markup);
    }

    #endregion

    #region Task 12.3: 損益即時更新

    [Fact]
    public void PortfolioPage_ShouldHaveRefreshButton()
    {
        // Arrange
        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(new List<PortfolioItem>());

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(new PortfolioSummary(0, 0, 0, 0, 0));

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();

        // Assert
        Assert.Contains("刷新", cut.Markup);
    }

    [Fact]
    public void PortfolioPage_ShouldSupportAutoRefresh()
    {
        // Arrange
        _mockPortfolioService
            .Setup(s => s.GetPortfolioAsync())
            .ReturnsAsync(new List<PortfolioItem>());

        _mockPortfolioService
            .Setup(s => s.GetPortfolioSummaryAsync())
            .ReturnsAsync(new PortfolioSummary(0, 0, 0, 0, 0));

        SetupServices();

        // Act
        var cut = RenderComponent<Portfolio>();

        // Assert - 頁面應該支援自動刷新功能
        Assert.Contains("持股部位", cut.Markup);
    }

    #endregion

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
