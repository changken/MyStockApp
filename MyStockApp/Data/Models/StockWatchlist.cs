using System.ComponentModel.DataAnnotations;

namespace MyStockApp.Data.Models
{
    public class StockWatchlist
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "股票代號為必填欄位")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "股票代號必須為4位數字")]
        [MaxLength(4)]
        public string StockSymbol { get; set; } = string.Empty;

        [Required(ErrorMessage = "股票名稱為必填欄位")]
        [MaxLength(100, ErrorMessage = "股票名稱不可超過100字元")]
        public string StockName { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "備註不可超過500字元")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
