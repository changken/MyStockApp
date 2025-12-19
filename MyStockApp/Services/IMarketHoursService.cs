namespace MyStockApp.Services;

/// <summary>
/// 台股市場交易時段判斷服務
/// </summary>
public interface IMarketHoursService
{
    /// <summary>
    /// 判斷指定時間是否為交易時段（週一至週五 09:00-13:25 台北時間，排除假日）
    /// </summary>
    /// <param name="dateTime">指定時間（UTC），null 表示當前時間</param>
    /// <returns>是否為交易時段</returns>
    bool IsMarketOpen(DateTime? dateTime = null);

    /// <summary>
    /// 計算下一個開盤時間
    /// </summary>
    /// <param name="from">起始時間（UTC），null 表示當前時間</param>
    /// <returns>下一個開盤時間（UTC）</returns>
    DateTime? GetNextMarketOpen(DateTime? from = null);

    /// <summary>
    /// 判斷指定日期是否為假日
    /// </summary>
    /// <param name="date">指定日期</param>
    /// <returns>是否為假日</returns>
    bool IsHoliday(DateOnly date);
}
