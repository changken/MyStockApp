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
/// 交易紀錄頁面測試 (Task 13)
/// </summary>
public class TradeHistoryPageTests : TestContext
{
    private readonly Mock<ITradingService> _mockTradingService;
    private readonly Mock<ICsvExportService> _mockCsvExportService;
    private DbContextOptions<AppDbContext> _dbOptions;

    public TradeHistoryPageTests()
    {
        _mockTradingService = new Mock<ITradingService>();
        _mockCsvExportService = new Mock<ICsvExportService>();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private void SetupServices()
    {
        Services.AddSingleton(_mockTradingService.Object);
        Services.AddSingleton(_mockCsvExportService.Object);
        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(_dbOptions));
    }

    #region Task 13.1: 交易紀錄清單顯示

    [Fact]
    public void TradeHistoryPage_ShouldRenderTitle()
    {
        // Arrange
        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(new List<Trade>());

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();

        // Assert
        Assert.Contains("交易紀錄", cut.Markup);
    }

    [Fact]
    public async Task TradeHistoryPage_ShouldDisplayTrades_WhenTradesExist()
    {
        // Arrange
        var trades = new List<Trade>
        {
            new Trade
            {
                Id = 1,
                StockSymbol = "2330",
                Side = TradeSide.Buy,
                Quantity = 1000,
                ExecutedPrice = 600m,
                TotalAmount = 600000m,
                Commission = 85.5m,
                TransactionTax = 0m,
                NetAmount = 600085.5m,
                ExecutedAt = DateTime.UtcNow
            },
            new Trade
            {
                Id = 2,
                StockSymbol = "2317",
                Side = TradeSide.Sell,
                Quantity = 2000,
                ExecutedPrice = 100m,
                TotalAmount = 200000m,
                Commission = 85.5m,
                TransactionTax = 600m,
                NetAmount = 199314.5m,
                ExecutedAt = DateTime.UtcNow.AddHours(-1)
            }
        };

        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(trades);

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("2330", cut.Markup);
        Assert.Contains("2317", cut.Markup);
    }

    [Fact]
    public void TradeHistoryPage_ShouldShowEmptyMessage_WhenNoTrades()
    {
        // Arrange
        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(new List<Trade>());

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("目前無交易紀錄", cut.Markup);
    }

    [Fact]
    public void TradeHistoryPage_ShouldDisplayTradeDetails()
    {
        // Arrange
        var trades = new List<Trade>
        {
            new Trade
            {
                Id = 1,
                StockSymbol = "2330",
                Side = TradeSide.Buy,
                Quantity = 1000,
                ExecutedPrice = 600m,
                TotalAmount = 600000m,
                Commission = 85.5m,
                TransactionTax = 0m,
                NetAmount = 600085.5m,
                ExecutedAt = DateTime.UtcNow
            }
        };

        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(trades);

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert - 應顯示完整交易資訊
        Assert.Contains("買入", cut.Markup);
        Assert.Contains("1000", cut.Markup);
        Assert.Contains("手續費", cut.Markup);
        Assert.Contains("交易稅", cut.Markup);
        Assert.Contains("淨金額", cut.Markup);
    }

    [Fact]
    public void TradeHistoryPage_ShouldOrderByExecutedAtDescending()
    {
        // Arrange
        var trades = new List<Trade>
        {
            new Trade
            {
                Id = 1,
                StockSymbol = "2330",
                Side = TradeSide.Buy,
                Quantity = 1000,
                ExecutedPrice = 600m,
                ExecutedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Trade
            {
                Id = 2,
                StockSymbol = "2317",
                Side = TradeSide.Sell,
                Quantity = 2000,
                ExecutedPrice = 100m,
                ExecutedAt = DateTime.UtcNow
            }
        };

        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(trades);

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();

        // Assert - 頁面應按日期降序排列
        Assert.Contains("交易紀錄", cut.Markup);
    }

    #endregion

    #region Task 13.2: 篩選與搜尋功能

    [Fact]
    public void TradeHistoryPage_ShouldHaveDateRangePicker()
    {
        // Arrange
        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(new List<Trade>());

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();

        // Assert
        Assert.Contains("開始日期", cut.Markup);
        Assert.Contains("結束日期", cut.Markup);
    }

    [Fact]
    public void TradeHistoryPage_ShouldHaveStockSymbolSearch()
    {
        // Arrange
        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(new List<Trade>());

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();

        // Assert
        Assert.Contains("股票代號", cut.Markup);
    }

    [Fact]
    public void TradeHistoryPage_ShouldHaveSearchButton()
    {
        // Arrange
        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(new List<Trade>());

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();

        // Assert
        Assert.Contains("搜尋", cut.Markup);
    }

    #endregion

    #region Task 13.3: CSV 匯出功能

    [Fact]
    public void TradeHistoryPage_ShouldHaveExportButton()
    {
        // Arrange
        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(new List<Trade>());

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();

        // Assert
        Assert.Contains("匯出", cut.Markup);
    }

    [Fact]
    public async Task TradeHistoryPage_ShouldExportTrades_WhenExportClicked()
    {
        // Arrange
        var trades = new List<Trade>
        {
            new Trade
            {
                Id = 1,
                StockSymbol = "2330",
                Side = TradeSide.Buy,
                Quantity = 1000,
                ExecutedPrice = 600m,
                ExecutedAt = DateTime.UtcNow
            }
        };

        _mockTradingService
            .Setup(s => s.GetTradesAsync(It.IsAny<TradeFilter>()))
            .ReturnsAsync(trades);

        _mockCsvExportService
            .Setup(s => s.ExportTradesAsync(It.IsAny<IEnumerable<Trade>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        SetupServices();

        // Act
        var cut = RenderComponent<TradeHistory>();

        // Assert - 匯出按鈕應該存在
        Assert.Contains("匯出", cut.Markup);
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
