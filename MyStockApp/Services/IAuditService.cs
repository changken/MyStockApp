namespace MyStockApp.Services;

/// <summary>
/// 稽核日誌記錄服務
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// 記錄稽核日誌
    /// </summary>
    /// <param name="action">操作類型（如 Create, Update, Delete, Cancel）</param>
    /// <param name="entityType">實體類型（如 Order, Trade, Portfolio）</param>
    /// <param name="entityId">實體識別碼</param>
    /// <param name="oldValue">變更前的值（選填，會序列化為 JSON）</param>
    /// <param name="newValue">變更後的值（選填，會序列化為 JSON）</param>
    /// <returns>非同步任務</returns>
    Task LogAsync(string action, string entityType, int entityId, object? oldValue = null, object? newValue = null);
}
