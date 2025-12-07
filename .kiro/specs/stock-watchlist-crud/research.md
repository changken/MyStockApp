# Research & Design Decisions Template

---
**Purpose**: Capture discovery findings, architectural investigations, and rationale that inform the technical design.

**Usage**:
- Log research activities and outcomes during the discovery phase.
- Document design decision trade-offs that are too detailed for `design.md`.
- Provide references and evidence for future audits or reuse.
---

## Summary
- **Feature**: `stock-watchlist-crud`
- **Discovery Scope**: Simple Addition（既有 Blazor Server 應用程式的標準 CRUD 功能擴充）
- **Key Findings**:
  - 現有 AppDbContext 已配置 DbContextFactory 模式，可直接擴充 DbSet
  - 現有頁面結構遵循 `/Components/Pages/` 模式，使用 `@page` 指令路由
  - Bootstrap 前端框架已整合，可用於表單與列表 UI
  - 既有錯誤處理機制（Error.razor, NotFound.razor, ReconnectModal）可重用

## Research Log

### 現有資料層架構分析
- **Context**: 確認如何擴充資料模型
- **Sources Consulted**:
  - `MyStockApp/Data/AppContext.cs`
  - `MyStockApp/Program.cs`
- **Findings**:
  - AppDbContext 已註冊為 DbContextFactory 模式（`builder.Services.AddDbContextFactory<AppDbContext>`）
  - 既有 TodoItem 範例（已註解）展示標準 Entity 模式
  - 連線字串管理已實作（支援 User Secrets 與 DATABASE_URL 環境變數）
- **Implications**:
  - 新增 StockWatchlist Entity 可直接加入 AppDbContext.DbSet
  - 需建立 EF Core Migration 以建立資料表
  - 遵循既有 Nullable Reference Types 與強型別模式

### 現有頁面與路由模式
- **Context**: 確認 Blazor 頁面組織方式
- **Sources Consulted**:
  - `MyStockApp/Components/Pages/` 目錄
  - `MyStockApp/Components/_Imports.razor`
- **Findings**:
  - 頁面使用 `@page "/route"` 指令定義路由
  - 命名慣例為 PascalCase（Counter.razor, Weather.razor）
  - 全域 using 已設定於 _Imports.razor
  - 既有頁面範例展示標準 Blazor Server 互動模式
- **Implications**:
  - 新增 `/stocks` 路由頁面，檔名為 StockWatchlist.razor
  - 可重用既有 Bootstrap 樣式與 MainLayout
  - 表單驗證可使用內建 EditForm + DataAnnotations

### 台股股票代號格式標準
- **Context**: 確認台股代號驗證規則
- **Sources Consulted**: 台灣證券交易所公開資訊
- **Findings**:
  - 台股股票代號為 4 位數字（例：2330, 2317）
  - 上市公司代號範圍：1000-9999
  - ETF、債券、特殊股票可能有字母後綴，但基本代號仍為 4 位數字
- **Implications**:
  - 驗證規則：`[RegularExpression(@"^\d{4}$")]`
  - 資料型別：string（保留前導零可能性，雖台股目前無此情況）
  - 唯一索引建立於 StockSymbol 欄位

### 錯誤處理與使用者體驗
- **Context**: 確認如何整合既有錯誤處理機制
- **Sources Consulted**:
  - `MyStockApp/Components/Layout/ReconnectModal.razor`
  - `MyStockApp/Components/Pages/Error.razor`
- **Findings**:
  - ReconnectModal 已處理 Blazor Server 連線中斷
  - Error.razor 提供集中式錯誤處理
  - Bootstrap 已整合，可使用 alert 樣式
- **Implications**:
  - CRUD 操作錯誤使用 Bootstrap alert 顯示
  - 連線中斷由既有 ReconnectModal 處理（無需額外實作）
  - 資料庫操作失敗使用 try-catch 包裝，顯示友善訊息

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Page-Level State | 所有 CRUD 邏輯與狀態直接於 StockWatchlist.razor.cs | 簡單直接，適合單頁 CRUD | 邏輯與 UI 耦合，測試較困難 | 適用於小型功能，符合既有 Counter.razor 模式 |
| Service Layer | 建立 StockWatchlistService 封裝資料存取邏輯 | 邏輯可重用，單元測試友善 | 增加檔案與註冊步驟 | 推薦用於未來擴充（如 API 端點、多頁面共用） |

**Selected**: Page-Level State（考量功能規模與既有範例模式，後續可重構為 Service）

## Design Decisions

### Decision: Entity 資料模型設計
- **Context**: 定義股票追蹤項目的資料結構
- **Alternatives Considered**:
  1. 僅儲存股票代號與名稱（最小化）
  2. 包含建立時間與更新時間（審計追蹤）
  3. 額外儲存股價、市值等即時資訊（擴充模式）
- **Selected Approach**: 選項 2 - 股票代號、名稱、備註、建立時間、更新時間
- **Rationale**:
  - 符合需求 6.4（儲存建立時間與更新時間）
  - 審計追蹤為資料管理最佳實踐
  - 不儲存即時股價資訊（避免資料同步複雜度，未來可透過外部 API 即時查詢）
- **Trade-offs**: 增加兩個欄位的儲存與維護成本，但提供完整追蹤記錄
- **Follow-up**: 建立 Migration 時確認 CreatedAt/UpdatedAt 自動更新機制

### Decision: 表單模式（新增 vs 編輯）
- **Context**: 決定新增與編輯是否使用同一表單
- **Alternatives Considered**:
  1. 分離的新增與編輯表單（兩個獨立元件）
  2. 共用表單元件，透過參數切換模式
  3. 單一頁面內使用條件渲染切換表單
- **Selected Approach**: 選項 3 - 單一頁面條件渲染
- **Rationale**:
  - 符合 Blazor Server 單頁應用模式
  - 減少元件數量與導航複雜度
  - 使用者體驗流暢（無頁面跳轉）
- **Trade-offs**: 頁面邏輯稍複雜，但整體程式碼量較少
- **Follow-up**: 使用布林旗標（IsEditing）控制表單顯示與按鈕文字

### Decision: 刪除確認機制
- **Context**: 防止使用者誤刪資料
- **Alternatives Considered**:
  1. 直接刪除（無確認）
  2. JavaScript confirm 對話框
  3. Bootstrap Modal 確認對話框
  4. 軟刪除（標記為已刪除）
- **Selected Approach**: 選項 3 - Bootstrap Modal 確認對話框
- **Rationale**:
  - 符合需求 4.1-4.2（顯示確認對話框與股票資訊）
  - Bootstrap 已整合，可直接使用
  - 提供更好的使用者體驗（顯示刪除目標詳細資訊）
- **Trade-offs**: 需額外實作 Modal 元件，但安全性與 UX 提升
- **Follow-up**: Modal 狀態使用 Page-Level State 管理

## Risks & Mitigations
- **Risk 1**: 股票代號重複新增導致資料庫約束錯誤 — **Mitigation**: 資料庫唯一索引 + 前端驗證 + DbUpdateException 錯誤處理
- **Risk 2**: 使用者輸入惡意 SQL 注入 — **Mitigation**: EF Core 參數化查詢（內建防護）+ 輸入長度限制
- **Risk 3**: 並行編輯導致資料覆蓋 — **Mitigation**: 目前為單使用者場景，未來可加入 RowVersion 欄位實作樂觀鎖定
- **Risk 4**: Migration 失敗導致資料庫不一致 — **Mitigation**: 遵循 EF Core Migration 最佳實踐，測試環境先驗證

## References
- [ASP.NET Core Blazor forms and input components](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/) — 表單驗證與 EditForm 使用
- [Entity Framework Core DbContext Factory](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#using-a-dbcontext-factory) — Blazor Server DbContextFactory 模式
- [Data Annotations](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations) — C# 驗證屬性
- [Bootstrap 5 Components](https://getbootstrap.com/docs/5.0/components/) — Modal, Alert 元件使用
