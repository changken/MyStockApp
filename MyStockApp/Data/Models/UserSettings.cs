using System.ComponentModel.DataAnnotations;

namespace MyStockApp.Data.Models
{
    public class UserSettings
    {
        public int Id { get; set; }

        [Range(0.1, 1.0, ErrorMessage = "折扣比例需介於 0.1 至 1.0")]
        public decimal CommissionDiscount { get; set; }

        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "單筆交易上限不可為負數")]
        public decimal MaxTradeAmount { get; set; }
    }
}

