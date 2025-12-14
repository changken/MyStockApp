using System.ComponentModel.DataAnnotations;

namespace MyStockApp.Data.Models
{
    public class Stock
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "股票代號為必填")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "股票代號必須為 4 位數字")]
        [MaxLength(4, ErrorMessage = "股票代號不可超過 4 字元")]
        public string Symbol { get; set; } = string.Empty;

        [Required(ErrorMessage = "股票名稱為必填")]
        [MaxLength(100, ErrorMessage = "股票名稱不可超過 100 字元")]
        public string Name { get; set; } = string.Empty;

        public MarketType Market { get; set; }

        [MaxLength(100, ErrorMessage = "產業類別不可超過 100 字元")]
        public string Industry { get; set; } = string.Empty;

        public decimal CurrentPrice { get; set; }

        public decimal OpenPrice { get; set; }

        public decimal HighPrice { get; set; }

        public decimal LowPrice { get; set; }

        public long Volume { get; set; }

        public DateTime LastUpdated { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();

        public Portfolio? Portfolio { get; set; }

        public ICollection<StockPriceHistory> PriceHistories { get; set; } = new List<StockPriceHistory>();
    }
}
