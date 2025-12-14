using MyStockApp.Services;
using MyStockApp.Data.Models;
using Xunit;

namespace MyStockApp.Tests;

public class TradingCostServiceTests
{
    private readonly ITradingCostService _service;

    public TradingCostServiceTests()
    {
        _service = new TradingCostService();
    }

    [Fact]
    public void CalculateCommission_ShouldReturnCorrectAmount_ForRegularTrade()
    {
        // 100,000 * 0.001425 * 0.6 = 85.5
        decimal amount = 100000m;
        decimal discount = 0.6m;
        decimal expected = 85.5m;

        var result = _service.CalculateCommission(amount, discount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateCommission_ShouldReturnMinFee_WhenCalculatedFeeIsLow()
    {
        // 1000 * 0.001425 * 0.6 = 0.855 < 20
        decimal amount = 1000m;
        decimal expected = 20m;

        var result = _service.CalculateCommission(amount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateTransactionTax_ShouldReturnCorrectTax()
    {
        // 100,000 * 0.003 = 300
        decimal amount = 100000m;
        decimal expected = 300m;

        var result = _service.CalculateTransactionTax(amount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateTotalCost_ShouldIncludeTax_WhenSelling()
    {
        // Sell 100,000
        // Comm: 85.5
        // Tax: 300
        // Total: 385.5
        decimal amount = 100000m;
        TradeSide side = TradeSide.Sell;
        
        var result = _service.CalculateTotalCost(amount, side);
        
        Assert.Equal(85.5m, result.Commission);
        Assert.Equal(300m, result.TransactionTax);
        Assert.Equal(385.5m, result.TotalCost);
    }

    [Fact]
    public void CalculateTotalCost_ShouldNotIncludeTax_WhenBuying()
    {
        // Buy 100,000
        // Comm: 85.5
        // Tax: 0
        // Total: 85.5
        decimal amount = 100000m;
        TradeSide side = TradeSide.Buy;
        
        var result = _service.CalculateTotalCost(amount, side);
        
        Assert.Equal(85.5m, result.Commission);
        Assert.Equal(0m, result.TransactionTax);
        Assert.Equal(85.5m, result.TotalCost);
    }

    [Fact]
    public void EstimatePnL_ShouldReturnCorrectEstimates()
    {
        int quantity = 1000;
        decimal averageCost = 100m;
        decimal currentPrice = 110m;
        decimal discount = 0.6m;
        
        // Cost Basis = 100,000
        // Market Value = 110,000
        // Sell Comm = 110,000 * 0.001425 * 0.6 = 94.05
        // Sell Tax = 110,000 * 0.003 = 330
        // Total Sell Cost = 424.05
        // Unrealized PnL = 110,000 - 424.05 - 100,000 = 9575.95
        // Return Rate = 9575.95 / 100,000 = 0.0957595
        
        var result = _service.EstimatePnL(currentPrice, quantity, averageCost, discount);
        
        Assert.Equal(110000m, result.MarketValue);
        Assert.Equal(100000m, result.TotalCost);
        Assert.Equal(9575.95m, result.UnrealizedPnL);
        Assert.Equal(0.0957595m, result.ReturnRate);
    }
}
