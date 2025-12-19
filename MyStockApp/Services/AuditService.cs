using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;

namespace MyStockApp.Services;

/// <summary>
/// 稽核日誌記錄服務實作
/// </summary>
public class AuditService : IAuditService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuditService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task LogAsync(string action, string entityType, int entityId, object? oldValue = null, object? newValue = null)
    {
        using var context = _contextFactory.CreateDbContext();

        var auditLog = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue != null ? JsonSerializer.Serialize(oldValue, _jsonOptions) : null,
            NewValue = newValue != null ? JsonSerializer.Serialize(newValue, _jsonOptions) : null,
            CreatedAt = DateTime.UtcNow
        };

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();
    }
}
