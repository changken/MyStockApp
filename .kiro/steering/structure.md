# Project Structure

## Organization Philosophy

**Component-First Architecture**：以 Blazor Components 為核心組織架構，遵循 ASP.NET Core Blazor 官方推薦結構，分離頁面、佈局與共用元件。

## Directory Patterns

### Components (`/Components/`)
**Location**: `/MyStockApp/Components/`
**Purpose**: 所有 Blazor 元件（頁面、佈局、共用元件）
**Example**:
- `App.razor`: 應用程式根元件
- `_Imports.razor`: 全域 using 與指令集
- `Routes.razor`: 路由配置

### Pages (`/Components/Pages/`)
**Location**: `/MyStockApp/Components/Pages/`
**Purpose**: 路由頁面元件（具有 `@page` 指令）
**Example**:
- `Home.razor`: 首頁 (`@page "/"`)
- `Counter.razor`: 計數器範例頁面
- `Weather.razor`: 天氣資訊頁面
- `TestPage.razor`: 測試頁面
- `Error.razor`: 錯誤處理頁面
- `NotFound.razor`: 404 頁面

### Layouts (`/Components/Layout/`)
**Location**: `/MyStockApp/Components/Layout/`
**Purpose**: 佈局元件（定義頁面外框與導航結構）
**Example**:
- `MainLayout.razor`: 主要版面配置
- `NavMenu.razor`: 導航選單
- `ReconnectModal.razor`: 斷線重連提示（含 JavaScript 互操作）

### Data Layer (`/Data/`)
**Location**: `/MyStockApp/Data/`
**Purpose**: Entity Framework Core DbContext 與資料模型
**Example**:
- `AppDbContext.cs`: 資料庫上下文定義

### Static Assets (`/wwwroot/`)
**Location**: `/MyStockApp/wwwroot/`
**Purpose**: 靜態資源（CSS、JavaScript、第三方函式庫）
**Example**:
- `/lib/bootstrap/`: Bootstrap 框架檔案

## Naming Conventions

- **Files**: PascalCase for C# files (`AppDbContext.cs`, `Program.cs`)
- **Components**: PascalCase matching file name (`MainLayout.razor` → `MainLayout`)
- **Pages**: PascalCase with descriptive names (`TestPage.razor`, `NotFound.razor`)
- **Namespaces**: 遵循目錄結構 (`MyStockApp.Data`, `MyStockApp.Components`)

## Import Organization

```csharp
// Blazor _Imports.razor (全域 using)
@using System.Net.Http
@using Microsoft.AspNetCore.Components.Routing
@using MyStockApp
@using MyStockApp.Components
```

**Namespace Pattern**:
- Root namespace: `MyStockApp`
- Assembly name 與 root namespace 一致

## Code Organization Principles

### Dependency Injection
- 使用 `IDbContextFactory<AppDbContext>` 注入（Blazor Server 最佳實踐）
- 服務註冊集中於 `Program.cs`
- 支援 Scoped fallback 以相容舊程式碼

### Configuration Management
- 設定檔：`appsettings.json`, `appsettings.Development.json`
- 敏感資訊：User Secrets (開發) + 環境變數 (生產)
- 連線字串動態組建邏輯位於 `Program.cs`

### Component Structure
- **Pages**: 包含路由邏輯與頁面級狀態
- **Layouts**: 提供共用版面與導航結構
- **Reusable components**: （未來可擴充至 `/Components/Shared/`）

### Error Handling Strategy
- 全域錯誤頁面：`/Error`（非開發環境）
- 狀態碼處理：`/not-found` (404)
- ReconnectModal：處理 Blazor Server 連線中斷

---
_Document patterns, not file trees. New files following patterns shouldn't require updates_
