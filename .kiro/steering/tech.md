# Technology Stack

## Architecture

**Blazor Server** 單體應用架構，採用伺服器端渲染與 SignalR 實現即時雙向通訊，所有邏輯運行於伺服器端。

## Core Technologies

- **Language**: C# 10+
- **Framework**: ASP.NET Core 10.0, Blazor Server
- **Runtime**: .NET 10.0
- **Database**: PostgreSQL (透過 Npgsql.EntityFrameworkCore.PostgreSQL)
- **ORM**: Entity Framework Core 10.0

## Key Libraries

- **Microsoft.EntityFrameworkCore.Tools**: 資料庫遷移與工具支援
- **Npgsql.EntityFrameworkCore.PostgreSQL**: PostgreSQL 資料庫提供者
- **Microsoft.VisualStudio.Azure.Containers.Tools.Targets**: Docker 容器化支援

## Development Standards

### Type Safety
- 啟用 Nullable Reference Types (`<Nullable>enable</Nullable>`)
- 使用隱式 Usings (`<ImplicitUsings>enable</ImplicitUsings>`)
- 強型別 DbContext 與 Entity 定義

### Code Quality
- 遵循 C# 命名慣例（PascalCase for classes/methods, camelCase for local variables）
- 使用 User Secrets 管理敏感資訊（ConnectionStrings）
- 避免硬編碼憑證或 API 金鑰

### Database Management
- 採用 `DbContextFactory<AppDbContext>` 模式（適合 Blazor Server 短生命週期需求）
- 支援環境變數 `DATABASE_URL` 動態組建連線字串（Heroku 模式）
- 本地開發使用 User Secrets 儲存連線字串

## Development Environment

### Required Tools
- .NET SDK 10.0+
- PostgreSQL 資料庫（本地或遠端）
- Docker（可選，用於容器化部署）

### Common Commands
```bash
# Dev: dotnet run
# Build: dotnet build
# Database migration: dotnet ef migrations add <MigrationName>
# Database update: dotnet ef database update
# User secrets: dotnet user-secrets set "<Key>" "<Value>"
```

## Key Technical Decisions

### Connection String Management
- **開發環境**：使用 User Secrets 儲存 `ConnectionStrings:DefaultConnection`
- **生產環境**：讀取 `DATABASE_URL` 環境變數並轉換為 Npgsql 格式（含 SSL 設定）
- **安全性**：Heroku 生產環境強制使用 `SSL Mode=Require`

### Blazor Rendering Mode
- 採用 Interactive Server Mode (`AddInteractiveServerRenderMode()`)
- 禁用導航例外拋出 (`<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>`)

### Error Handling
- 非開發環境使用集中式錯誤處理頁面 (`/Error`)
- 啟用 HSTS（HTTP Strict Transport Security）
- 自訂 404 頁面處理 (`UseStatusCodePagesWithReExecute("/not-found")`)

---
_Document standards and patterns, not every dependency_
