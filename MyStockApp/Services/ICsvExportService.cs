using MyStockApp.Data.Models;

namespace MyStockApp.Services
{
    /// <summary>
    /// CSV 匯出服務介面
    /// </summary>
    public interface ICsvExportService
    {
        /// <summary>
        /// 匯出交易紀錄為 CSV 檔案
        /// </summary>
        /// <param name="trades">交易紀錄清單</param>
        /// <param name="fileName">檔案名稱（預設為 trades.csv）</param>
        Task ExportTradesAsync(IEnumerable<Trade> trades, string fileName = "trades.csv");
    }
}
