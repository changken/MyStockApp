using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyStockApp.Components.Pages;
using MyStockApp.Data.Models;
using MyStockApp.Services;
using Xunit;

namespace MyStockApp.Tests;

/// <summary>
/// Task 16.1: 股票查詢頁面測試
/// </summary>
public class StockSearchPageTests : TestContext
{
    private readonly Mock<IStockService> _mockStockService;

    public StockSearchPageTests()
    {
        _mockStockService = new Mock<IStockService>();
        Services.AddSingleton(_mockStockService.Object);
    }

    [Fact]
    public void StockSearchPage_ShouldRenderSearchInputs()
    {
        // Arrange
        _mockStockService.Setup(s => s.SearchStocksAsync(null, null, null))
            .ReturnsAsync(new List<Stock>());

        // Act
        var cut = RenderComponent<StockSearch>();

        // Assert - 驗證搜尋欄位存在
        Assert.Contains("搜尋", cut.Markup);
    }

    [Fact]
    public void StockSearchPage_ShouldDisplaySearchResults_WhenStocksFound()
    {
        // Arrange
        var stocks = new List<Stock>
        {
            new Stock
            {
                Id = 1,
                Symbol = "2330",
                Name = "台積電",
                Market = MarketType.Listed,
                Industry = "半導體",
                CurrentPrice = 600m,
                UpdatedAt = DateTime.UtcNow
            },
            new Stock
            {
                Id = 2,
                Symbol = "2317",
                Name = "鴻海",
                Market = MarketType.Listed,
                Industry = "電子",
                CurrentPrice = 100m,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockStockService.Setup(s => s.SearchStocksAsync(It.IsAny<string>(), It.IsAny<MarketType?>(), It.IsAny<string?>()))
            .ReturnsAsync(stocks);

        // Act
        var cut = RenderComponent<StockSearch>();

        // Assert
        Assert.Contains("2330", cut.Markup);
        Assert.Contains("台積電", cut.Markup);
        Assert.Contains("2317", cut.Markup);
        Assert.Contains("鴻海", cut.Markup);
    }

    [Fact]
    public void StockSearchPage_ShouldFilterByKeyword()
    {
        // Arrange
        var stocks = new List<Stock>
        {
            new Stock
            {
                Id = 1,
                Symbol = "2330",
                Name = "台積電",
                Market = MarketType.Listed,
                Industry = "半導體",
                CurrentPrice = 600m,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockStockService.Setup(s => s.SearchStocksAsync("2330", null, null))
            .ReturnsAsync(stocks);

        // Act
        var cut = RenderComponent<StockSearch>();
        var input = cut.Find("input[placeholder*='代號']");
        input.Change("2330");
        var button = cut.Find("button:contains('搜尋')");
        button.Click();

        // Assert
        _mockStockService.Verify(s => s.SearchStocksAsync("2330", null, null), Times.Once);
    }

    [Fact]
    public void StockSearchPage_ShouldFilterByMarket()
    {
        // Arrange
        _mockStockService.Setup(s => s.SearchStocksAsync(null, MarketType.Listed, null))
            .ReturnsAsync(new List<Stock>());

        // Act
        var cut = RenderComponent<StockSearch>();
        var select = cut.Find("select");
        select.Change(((int)MarketType.Listed).ToString());

        // Assert
        _mockStockService.Verify(s => s.SearchStocksAsync(null, MarketType.Listed, null), Times.AtLeastOnce);
    }

    [Fact]
    public void StockSearchPage_ShouldFilterByIndustry()
    {
        // Arrange
        _mockStockService.Setup(s => s.SearchStocksAsync(null, null, "半導體"))
            .ReturnsAsync(new List<Stock>());

        // Act
        var cut = RenderComponent<StockSearch>();
        var industrySelect = cut.FindAll("select").Skip(1).First();
        industrySelect.Change("半導體");

        // Assert
        _mockStockService.Verify(s => s.SearchStocksAsync(null, null, "半導體"), Times.AtLeastOnce);
    }

    [Fact]
    public void StockSearchPage_ShouldShowStockQuote_WhenStockSelected()
    {
        // Arrange
        var stock = new Stock
        {
            Id = 1,
            Symbol = "2330",
            Name = "台積電",
            Market = MarketType.Listed,
            Industry = "半導體",
            CurrentPrice = 600m,
            OpenPrice = 595m,
            HighPrice = 605m,
            LowPrice = 590m,
            Volume = 10000000,
            UpdatedAt = DateTime.UtcNow
        };

        _mockStockService.Setup(s => s.SearchStocksAsync(null, null, null))
            .ReturnsAsync(new List<Stock> { stock });

        _mockStockService.Setup(s => s.GetStockAsync(1))
            .ReturnsAsync(stock);

        // Act
        var cut = RenderComponent<StockSearch>();
        var selectButton = cut.Find("button:contains('選取')");
        selectButton.Click();

        // Assert - 驗證顯示即時報價
        Assert.Contains("600", cut.Markup); // 當前價格
        Assert.Contains("595", cut.Markup); // 開盤價
        Assert.Contains("605", cut.Markup); // 最高價
        Assert.Contains("590", cut.Markup); // 最低價
    }

    [Fact]
    public void StockSearchPage_ShouldShowUpdateTime_InQuote()
    {
        // Arrange
        var updateTime = DateTime.UtcNow;
        var stock = new Stock
        {
            Id = 1,
            Symbol = "2330",
            Name = "台積電",
            Market = MarketType.Listed,
            Industry = "半導體",
            CurrentPrice = 600m,
            UpdatedAt = updateTime
        };

        _mockStockService.Setup(s => s.SearchStocksAsync(null, null, null))
            .ReturnsAsync(new List<Stock> { stock });

        _mockStockService.Setup(s => s.GetStockAsync(1))
            .ReturnsAsync(stock);

        // Act
        var cut = RenderComponent<StockSearch>();
        var selectButton = cut.Find("button:contains('選取')");
        selectButton.Click();

        // Assert - 驗證顯示更新時間
        Assert.Contains("更新時間", cut.Markup);
    }

    [Fact]
    public void StockSearchPage_ShouldDisplayEmptyMessage_WhenNoResults()
    {
        // Arrange
        _mockStockService.Setup(s => s.SearchStocksAsync(It.IsAny<string>(), It.IsAny<MarketType?>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<Stock>());

        // Act
        var cut = RenderComponent<StockSearch>();
        var button = cut.Find("button:contains('搜尋')");
        button.Click();

        // Assert
        Assert.Contains("查無股票", cut.Markup);
    }
}
