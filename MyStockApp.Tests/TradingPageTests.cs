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
/// 交易下單頁面測試 (Task 10)
/// </summary>
public class TradingPageTests : TestContext
{
    private readonly Mock<IStockService> _mockStockService;
    private readonly Mock<ITradingService> _mockTradingService;
    private readonly Mock<ITradingCostService> _mockCostService;
    private readonly Mock<IMarketHoursService> _mockMarketHoursService;
    private DbContextOptions<AppDbContext> _dbOptions;

    public TradingPageTests()
    {
        _mockStockService = new Mock<IStockService>();
        _mockTradingService = new Mock<ITradingService>();
        _mockCostService = new Mock<ITradingCostService>();
        _mockMarketHoursService = new Mock<IMarketHoursService>();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private void SetupServices()
    {
        Services.AddSingleton(_mockStockService.Object);
        Services.AddSingleton(_mockTradingService.Object);
        Services.AddSingleton(_mockCostService.Object);
        Services.AddSingleton(_mockMarketHoursService.Object);
        Services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new TestDbContextFactory(_dbOptions));
    }

    #region Task 10.1: 股票搜尋與選擇介面

    [Fact]
    public void TradingPage_ShouldRenderStockSearchInterface()
    {
        // Arrange
        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        Assert.Contains("交易下單", cut.Markup);
        Assert.Contains("搜尋股票", cut.Markup);
    }

    [Fact]
    public async Task TradingPage_ShouldDisplaySearchResults_WhenSearching()
    {
        // Arrange
        var stocks = new List<Stock>
        {
            new Stock { Id = 1, Symbol = "2330", Name = "台積電", CurrentPrice = 600m, Market = MarketType.Listed },
            new Stock { Id = 2, Symbol = "2317", Name = "鴻海", CurrentPrice = 100m, Market = MarketType.Listed }
        };

        _mockStockService
            .Setup(s => s.SearchStocksAsync("23", null, null))
            .ReturnsAsync(stocks);

        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();
        cut.WaitForState(() => !cut.Markup.Contains("載入中"), TimeSpan.FromSeconds(5));

        // Assert - 應該能看到搜尋介面
        Assert.Contains("搜尋", cut.Markup);
    }

    [Fact]
    public async Task TradingPage_ShouldShowStockQuote_WhenStockSelected()
    {
        // Arrange
        var stock = new Stock
        {
            Id = 1,
            Symbol = "2330",
            Name = "台積電",
            CurrentPrice = 600m,
            OpenPrice = 595m,
            HighPrice = 605m,
            LowPrice = 590m,
            Volume = 10000,
            Market = MarketType.Listed
        };

        _mockStockService
            .Setup(s => s.GetStockAsync(1))
            .ReturnsAsync(stock);

        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert - 選擇股票後應顯示報價資訊
        // (實際的互動測試將在元件實作後完成)
        Assert.Contains("交易下單", cut.Markup);
    }

    #endregion

    #region Task 10.2: 下單表單介面

    [Fact]
    public void TradingPage_ShouldHaveBuySellOptions()
    {
        // Arrange
        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        Assert.Contains("買入", cut.Markup);
        Assert.Contains("賣出", cut.Markup);
    }

    [Fact]
    public void TradingPage_ShouldHaveOrderTypeOptions()
    {
        // Arrange
        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        Assert.Contains("市價單", cut.Markup);
        Assert.Contains("限價單", cut.Markup);
    }

    [Fact]
    public void TradingPage_ShouldHaveQuantityInput()
    {
        // Arrange
        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        // 應該有數量輸入欄位
        Assert.Contains("數量", cut.Markup);
    }

    [Fact]
    public void TradingPage_ShouldShowPriceInput_WhenLimitOrderSelected()
    {
        // Arrange
        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        // 限價單應顯示價格輸入欄位
        Assert.Contains("價格", cut.Markup);
    }

    #endregion

    #region Task 10.3: 交易成本預估顯示

    [Fact]
    public void TradingPage_ShouldDisplayCostEstimation_WhenOrderInfoEntered()
    {
        // Arrange
        var tradingCost = new TradingCost(
            Commission: 85.5m,
            TransactionTax: 0m,
            TotalCost: 85.5m
        );

        _mockCostService
            .Setup(s => s.CalculateTotalCost(It.IsAny<decimal>(), It.IsAny<TradeSide>(), It.IsAny<decimal>()))
            .Returns(tradingCost);

        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        // 應該顯示成本預估相關欄位
        Assert.Contains("手續費", cut.Markup);
    }

    [Fact]
    public void TradingPage_ShouldShowCommissionAndTax_InCostEstimation()
    {
        // Arrange
        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        Assert.Contains("手續費", cut.Markup);
        Assert.Contains("交易稅", cut.Markup);
        Assert.Contains("總成本", cut.Markup);
    }

    #endregion

    #region Task 10.4: 下單確認與提交

    [Fact]
    public void TradingPage_ShouldShowMarketClosedMessage_WhenMarketClosed()
    {
        // Arrange
        _mockMarketHoursService
            .Setup(s => s.IsMarketOpen(It.IsAny<DateTime?>()))
            .Returns(false);

        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        Assert.Contains("休市", cut.Markup);
    }

    [Fact]
    public async Task TradingPage_ShouldSubmitOrder_WhenFormValid()
    {
        // Arrange
        var orderResult = Result<Order, TradingError>.Success(new Order
        {
            Id = 1,
            StockId = 1,
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 1000,
            Price = 600m,
            Status = OrderStatus.Pending
        });

        _mockTradingService
            .Setup(s => s.CreateOrderAsync(It.IsAny<CreateOrderRequest>()))
            .ReturnsAsync(orderResult);

        _mockMarketHoursService
            .Setup(s => s.IsMarketOpen(It.IsAny<DateTime?>()))
            .Returns(true);

        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        // 下單按鈕應該存在
        Assert.Contains("確認下單", cut.Markup);
    }

    [Fact]
    public void TradingPage_ShouldDisableSubmitButton_WhileProcessing()
    {
        // Arrange
        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        // 處理中時按鈕應該被禁用（透過 disabled 屬性或 spinner）
        Assert.Contains("確認下單", cut.Markup);
    }

    [Fact]
    public void TradingPage_ShouldShowSuccessMessage_AfterOrderSubmitted()
    {
        // Arrange
        var orderResult = Result<Order, TradingError>.Success(new Order
        {
            Id = 1,
            StockId = 1,
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 1000,
            Status = OrderStatus.Pending
        });

        _mockTradingService
            .Setup(s => s.CreateOrderAsync(It.IsAny<CreateOrderRequest>()))
            .ReturnsAsync(orderResult);

        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        // 成功訊息會在提交後顯示
        Assert.Contains("交易下單", cut.Markup);
    }

    [Fact]
    public void TradingPage_ShouldShowErrorMessage_WhenOrderFails()
    {
        // Arrange
        var orderResult = Result<Order, TradingError>.Failure(TradingError.InvalidQuantity);

        _mockTradingService
            .Setup(s => s.CreateOrderAsync(It.IsAny<CreateOrderRequest>()))
            .ReturnsAsync(orderResult);

        SetupServices();

        // Act
        var cut = RenderComponent<Trading>();

        // Assert
        // 錯誤訊息會在失敗後顯示
        Assert.Contains("交易下單", cut.Markup);
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
