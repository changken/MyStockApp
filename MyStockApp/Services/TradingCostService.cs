using MyStockApp.Data.Models;

namespace MyStockApp.Services;

public class TradingCostService : ITradingCostService
{
    public decimal CalculateCommission(decimal amount, decimal discountRate = 0.6m)
    {
        var commission = amount * 0.001425m * discountRate;
        // Standard practice often floors to integer, but spec asks for decimal precision.
        // We will return the exact calculated value, but ensure minimum is 20.
        return commission < 20m ? 20m : commission;
    }

    public decimal CalculateTransactionTax(decimal amount)
    {
        return amount * 0.003m;
    }

    public TradingCost CalculateTotalCost(decimal amount, TradeSide side, decimal discountRate = 0.6m)
    {
        var commission = CalculateCommission(amount, discountRate);
        var tax = side == TradeSide.Sell ? CalculateTransactionTax(amount) : 0m;
        
        return new TradingCost(commission, tax, commission + tax);
    }

    public PnLEstimate EstimatePnL(decimal currentPrice, int quantity, decimal averageCost, decimal discountRate = 0.6m)
    {
        var costBasis = quantity * averageCost;
        var marketValue = quantity * currentPrice;
        
        // Calculate estimated selling cost (Commission + Tax)
        var estimatedSellCost = CalculateTotalCost(marketValue, TradeSide.Sell, discountRate).TotalCost;
        
        // Unrealized PnL = Market Value - Cost Basis - Estimated Sell Cost
        // Requirement 5.2: 當前市值 - 持股成本 - 預估賣出成本
        var unrealizedPnL = marketValue - costBasis - estimatedSellCost;
        
        // Return Rate = UnrealizedPnL / Cost Basis
        var returnRate = costBasis == 0 ? 0 : unrealizedPnL / costBasis;
        
        return new PnLEstimate(marketValue, costBasis, unrealizedPnL, returnRate);
    }
}
