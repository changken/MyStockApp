using MyStockApp.Services;
using MyStockApp.Data.Models;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using System.Globalization;
using System.Text;

namespace MyStockApp.Tests;

public class CsvExportServiceTests
{
    private readonly Mock<IJSRuntime> _mockJsRuntime;
    private readonly ICsvExportService _service;

    public CsvExportServiceTests()
    {
        _mockJsRuntime = new Mock<IJSRuntime>();
        _service = new CsvExportService(_mockJsRuntime.Object);
    }

    [Fact]
    public async Task ExportTradesAsync_ShouldGenerateCsvWithCorrectHeaders()
    {
        // Arrange
        var trades = new List<Trade>();
        string? capturedBase64 = null;
        string? capturedFileName = null;

        _mockJsRuntime
            .Setup(x => x.InvokeAsync<object>(
                "downloadFileFromBase64",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                capturedFileName = args[0]?.ToString();
                capturedBase64 = args[2]?.ToString();
            })
            .ReturnsAsync(new object());

        // Act
        await _service.ExportTradesAsync(trades);

        // Assert
        Assert.NotNull(capturedBase64);
        var csvContent = Encoding.UTF8.GetString(Convert.FromBase64String(capturedBase64));

        // 驗證 UTF-8 BOM
        Assert.StartsWith("\uFEFF", csvContent);

        // 驗證標題列
        var lines = csvContent.Split('\n');
        Assert.Contains("交易日期", lines[0]);
        Assert.Contains("股票代號", lines[0]);
        Assert.Contains("交易類型", lines[0]);
        Assert.Contains("數量", lines[0]);
        Assert.Contains("成交價格", lines[0]);
        Assert.Contains("手續費", lines[0]);
        Assert.Contains("交易稅", lines[0]);
        Assert.Contains("淨金額", lines[0]);
    }

    [Fact]
    public async Task ExportTradesAsync_ShouldIncludeTradeData()
    {
        // Arrange
        var trades = new List<Trade>
        {
            new Trade
            {
                Id = 1,
                OrderId = 1,
                StockSymbol = "2330",
                Side = TradeSide.Buy,
                Quantity = 1000,
                ExecutedPrice = 500.5m,
                TotalAmount = 500500m,
                Commission = 85.5m,
                TransactionTax = 0m,
                NetAmount = 500585.5m,
                ExecutedAt = new DateTime(2025, 1, 15, 10, 30, 0)
            }
        };

        string? capturedBase64 = null;

        _mockJsRuntime
            .Setup(x => x.InvokeAsync<object>(
                "downloadFileFromBase64",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                capturedBase64 = args[2]?.ToString();
            })
            .ReturnsAsync(new object());

        // Act
        await _service.ExportTradesAsync(trades);

        // Assert
        Assert.NotNull(capturedBase64);
        var csvContent = Encoding.UTF8.GetString(Convert.FromBase64String(capturedBase64));

        Assert.Contains("2330", csvContent);
        Assert.Contains("買入", csvContent);
        Assert.Contains("1000", csvContent);
        Assert.Contains("500.5", csvContent);
    }

    [Fact]
    public async Task ExportTradesAsync_ShouldUseCorrectMimeType()
    {
        // Arrange
        var trades = new List<Trade>();
        string? capturedMimeType = null;

        _mockJsRuntime
            .Setup(x => x.InvokeAsync<object>(
                "downloadFileFromBase64",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                capturedMimeType = args[1]?.ToString();
            })
            .ReturnsAsync(new object());

        // Act
        await _service.ExportTradesAsync(trades);

        // Assert
        Assert.Equal("text/csv", capturedMimeType);
    }

    [Fact]
    public async Task ExportTradesAsync_ShouldUseDefaultFileName_WhenNotProvided()
    {
        // Arrange
        var trades = new List<Trade>();
        string? capturedFileName = null;

        _mockJsRuntime
            .Setup(x => x.InvokeAsync<object>(
                "downloadFileFromBase64",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                capturedFileName = args[0]?.ToString();
            })
            .ReturnsAsync(new object());

        // Act
        await _service.ExportTradesAsync(trades);

        // Assert
        Assert.Equal("trades.csv", capturedFileName);
    }

    [Fact]
    public async Task ExportTradesAsync_ShouldUseCustomFileName_WhenProvided()
    {
        // Arrange
        var trades = new List<Trade>();
        string customFileName = "my-trades-2025.csv";
        string? capturedFileName = null;

        _mockJsRuntime
            .Setup(x => x.InvokeAsync<object>(
                "downloadFileFromBase64",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                capturedFileName = args[0]?.ToString();
            })
            .ReturnsAsync(new object());

        // Act
        await _service.ExportTradesAsync(trades, customFileName);

        // Assert
        Assert.Equal(customFileName, capturedFileName);
    }

    [Fact]
    public async Task ExportTradesAsync_ShouldHandleMultipleTrades()
    {
        // Arrange
        var trades = new List<Trade>
        {
            new Trade
            {
                Id = 1,
                OrderId = 1,
                StockSymbol = "2330",
                Side = TradeSide.Buy,
                Quantity = 1000,
                ExecutedPrice = 500m,
                TotalAmount = 500000m,
                Commission = 85.5m,
                TransactionTax = 0m,
                NetAmount = 500085.5m,
                ExecutedAt = new DateTime(2025, 1, 15, 10, 0, 0)
            },
            new Trade
            {
                Id = 2,
                OrderId = 2,
                StockSymbol = "2317",
                Side = TradeSide.Sell,
                Quantity = 500,
                ExecutedPrice = 100m,
                TotalAmount = 50000m,
                Commission = 42.75m,
                TransactionTax = 150m,
                NetAmount = 49807.25m,
                ExecutedAt = new DateTime(2025, 1, 16, 14, 30, 0)
            }
        };

        string? capturedBase64 = null;

        _mockJsRuntime
            .Setup(x => x.InvokeAsync<object>(
                "downloadFileFromBase64",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                capturedBase64 = args[2]?.ToString();
            })
            .ReturnsAsync(new object());

        // Act
        await _service.ExportTradesAsync(trades);

        // Assert
        Assert.NotNull(capturedBase64);
        var csvContent = Encoding.UTF8.GetString(Convert.FromBase64String(capturedBase64));

        // 應包含標題列 + 2 筆資料 = 3 行（可能有空行）
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3);

        Assert.Contains("2330", csvContent);
        Assert.Contains("2317", csvContent);
        Assert.Contains("買入", csvContent);
        Assert.Contains("賣出", csvContent);
    }

    [Fact]
    public async Task ExportTradesAsync_ShouldHandleEmptyList()
    {
        // Arrange
        var trades = new List<Trade>();
        string? capturedBase64 = null;

        _mockJsRuntime
            .Setup(x => x.InvokeAsync<object>(
                "downloadFileFromBase64",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                capturedBase64 = args[2]?.ToString();
            })
            .ReturnsAsync(new object());

        // Act
        await _service.ExportTradesAsync(trades);

        // Assert
        Assert.NotNull(capturedBase64);
        var csvContent = Encoding.UTF8.GetString(Convert.FromBase64String(capturedBase64));

        // 空清單應該只有標題列
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines); // 只有標題列
    }
}
