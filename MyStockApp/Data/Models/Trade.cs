using System.ComponentModel.DataAnnotations;

namespace MyStockApp.Data.Models
{
    public class Trade
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order Order { get; set; } = default!;

        [Required(ErrorMessage = "股票代號為必填")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "股票代號必須為 4 位數字")]
        [MaxLength(4, ErrorMessage = "股票代號不可超過 4 字元")]
        public string StockSymbol { get; set; } = string.Empty;

        public TradeSide Side { get; set; }

        public int Quantity { get; set; }

        public decimal ExecutedPrice { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal Commission { get; set; }

        public decimal TransactionTax { get; set; }

        public decimal NetAmount { get; set; }

        public DateTime ExecutedAt { get; set; }
    }
}

