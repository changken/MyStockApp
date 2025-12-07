using System.ComponentModel.DataAnnotations;
using MyStockApp.Data.Models;
using Xunit;

namespace MyStockApp.Tests;

public class StockWatchlistEntityTests
{
    [Fact]
    public void StockWatchlist_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var stock = new StockWatchlist
        {
            Id = 1,
            StockSymbol = "2330",
            StockName = "台積電",
            Notes = "測試備註",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(1, stock.Id);
        Assert.Equal("2330", stock.StockSymbol);
        Assert.Equal("台積電", stock.StockName);
        Assert.Equal("測試備註", stock.Notes);
        Assert.NotEqual(default(DateTime), stock.CreatedAt);
        Assert.NotEqual(default(DateTime), stock.UpdatedAt);
    }

    [Theory]
    [InlineData("2330", true)]  // 有效: 4位數字
    [InlineData("0050", true)]  // 有效: 4位數字
    [InlineData("123", false)]  // 無效: 3位數字
    [InlineData("12345", false)] // 無效: 5位數字
    [InlineData("AAPL", false)] // 無效: 英文字母
    [InlineData("", false)]     // 無效: 空字串
    public void StockSymbol_ShouldValidateFormat(string symbol, bool isValid)
    {
        // Arrange
        var stock = new StockWatchlist
        {
            StockSymbol = symbol,
            StockName = "測試股票"
        };

        var context = new ValidationContext(stock);
        var results = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(stock, context, results, true);

        // Assert
        if (isValid)
        {
            Assert.True(actual, $"Symbol '{symbol}' should be valid");
        }
        else
        {
            Assert.False(actual, $"Symbol '{symbol}' should be invalid");
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(StockWatchlist.StockSymbol)));
        }
    }

    [Fact]
    public void StockSymbol_ShouldBeRequired()
    {
        // Arrange
        var stock = new StockWatchlist
        {
            StockSymbol = "",
            StockName = "測試股票"
        };

        var context = new ValidationContext(stock);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(stock, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StockWatchlist.StockSymbol)));
    }

    [Fact]
    public void StockName_ShouldBeRequired()
    {
        // Arrange
        var stock = new StockWatchlist
        {
            StockSymbol = "2330",
            StockName = ""
        };

        var context = new ValidationContext(stock);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(stock, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StockWatchlist.StockName)));
    }

    [Fact]
    public void StockName_ShouldNotExceedMaxLength()
    {
        // Arrange
        var stock = new StockWatchlist
        {
            StockSymbol = "2330",
            StockName = new string('A', 101) // 超過 100 字元
        };

        var context = new ValidationContext(stock);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(stock, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StockWatchlist.StockName)));
    }

    [Fact]
    public void Notes_CanBeNull()
    {
        // Arrange
        var stock = new StockWatchlist
        {
            StockSymbol = "2330",
            StockName = "台積電",
            Notes = null
        };

        var context = new ValidationContext(stock);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(stock, context, results, true);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Notes_ShouldNotExceedMaxLength()
    {
        // Arrange
        var stock = new StockWatchlist
        {
            StockSymbol = "2330",
            StockName = "台積電",
            Notes = new string('A', 501) // 超過 500 字元
        };

        var context = new ValidationContext(stock);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(stock, context, results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StockWatchlist.Notes)));
    }
}
