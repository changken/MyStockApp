# 研究與設計決策日誌

---
**Purpose**: 記錄發現階段的研究成果與架構決策依據。

**Usage**:
- 記錄發現階段的研究活動與成果
- 文件化對於 `design.md` 過於詳細的設計決策取捨
- 提供未來審核或重用的參考與證據
---

## Summary
- **Feature**: `stock-trading`
- **Discovery Scope**: Extension（擴充現有 Blazor Server 應用）
- **Key Findings**:
  - Blazor Server CSV 匯出最佳實踐為使用 JavaScript Interop + CsvHelper
  - 服務層模式應採用介面注入，生命週期使用 Scoped
  - 台股交易時間為週一至週五 09:00-13:25（台北時間）

## Research Log

### CSV 匯出實作方式
- **Context**: R4.4 要求 CSV 格式交易紀錄匯出功能
- **Sources Consulted**:
  - [C# Corner: How to Download Data as CSV in Blazor App](https://www.c-sharpcorner.com/article/how-to-download-data-as-csv-in-c-sharp-blazor-app/)
  - [GitHub: BlazorDownloadExportComponent](https://github.com/EdCharbeneau/BlazorDownloadExportComponent)
- **Findings**:
  - Blazor Server 需透過 JavaScript Interop 觸發瀏覽器下載
  - CsvHelper NuGet 套件為 C# CSV 處理的標準選擇
  - 使用 MemoryStream + StreamWriter + CsvWriter 產生 CSV 內容
  - 將 byte[] 轉為 Base64 Data URI 供 JavaScript 下載
- **Implications**:
  - 需新增 `wwwroot/js/fileDownload.js` 輔助腳本
  - 在 `_Host.cshtml` 或 `App.razor` 引入腳本
  - 建立 `ICsvExportService` 封裝匯出邏輯

### 服務層模式與依賴注入
- **Context**: 需決定業務邏輯（交易成本計算、訂單處理）的組織方式
- **Sources Consulted**:
  - [Microsoft Learn: ASP.NET Core Blazor dependency injection](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection)
  - [Telerik: Blazor Basics Dependency Injection Best Practices](https://www.telerik.com/blogs/blazor-basics-dependency-injection-best-practices-use-cases)
  - [Microsoft Learn: Blazor with Entity Framework Core](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core)
- **Findings**:
  - Blazor Server 中 Scoped 服務生命週期等同於 SignalR 連線（Circuit）
  - 避免在元件中直接注入 DbContext，應使用 IDbContextFactory
  - 服務介面注入提升可測試性與可維護性
  - 使用 @inject 指令或建構子注入
- **Implications**:
  - 所有業務服務使用介面定義（ITradingService, ITradingCostService 等）
  - 服務註冊為 Scoped 生命週期
  - 服務內部使用 IDbContextFactory 取得 DbContext

### 台股交易時段
- **Context**: R6.4 要求休市時段顯示提示訊息
- **Sources Consulted**:
  - [Taiwan Stock Exchange: Holiday Schedule](https://www.twse.com.tw/en/trading/holiday.html)
  - [TradingHours.com: TWSE Market Hours](https://www.tradinghours.com/markets/twse)
- **Findings**:
  - 台股交易時間：週一至週五 09:00-13:25（台北時間 GMT+8）
  - 假日依據台灣行政院人事行政總處公告
  - 主要假日：農曆新年、清明節、端午節、中秋節、國慶日等
- **Implications**:
  - 建立 `IMarketHoursService` 判斷開收盤時間
  - 假日資料可先硬編碼 2025 年假日清單，未來可擴充為資料庫或 API

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Vertical Slice | 依功能切分，每個切片包含 UI、Service、Data | 高內聚、易於理解單一功能 | 跨功能重用需額外抽象 | 適合中小型專案 |
| Layered (N-Tier) | 傳統分層：Presentation → Service → Data | 清晰分層、職責明確 | 可能過度工程化 | 符合現有 Blazor 模式 |
| Clean Architecture | Domain 核心、Application、Infrastructure 分離 | 高度可測試、領域驅動 | 複雜度高、學習曲線陡 | 對本專案規模過大 |

**Selected**: Layered (N-Tier) 搭配服務介面

**Rationale**: 符合現有專案結構（Data 層已存在）、團隊熟悉度高、保持架構一致性

## Design Decisions

### Decision: 採用服務層模式封裝業務邏輯
- **Context**: 交易成本計算涉及精確的金額運算，需確保可測試性
- **Alternatives Considered**:
  1. 直接在 Razor 元件實作 — 簡單但難以測試
  2. 建立獨立服務類別 — 可測試、可重用
  3. 使用靜態工具類別 — 無狀態但難以 Mock
- **Selected Approach**: 建立介面導向的服務類別（ITradingCostService）
- **Rationale**: 金額計算需嚴格單元測試驗證，服務介面可輕鬆 Mock
- **Trade-offs**: 增加檔案數量，但提升程式碼品質與可維護性
- **Follow-up**: 建立完整的單元測試覆蓋交易成本計算邏輯

### Decision: CSV 匯出使用 JavaScript Interop
- **Context**: Blazor Server 無法直接觸發瀏覽器檔案下載
- **Alternatives Considered**:
  1. 使用第三方元件庫（Telerik、DevExpress）— 功能強大但需授權費用
  2. JavaScript Interop + CsvHelper — 輕量、免費
  3. 建立 API 端點下載 — 需額外路由配置
- **Selected Approach**: JavaScript Interop + CsvHelper
- **Rationale**: 符合專案預算（免費方案）、CsvHelper 是 .NET 生態系標準
- **Trade-offs**: 需維護少量 JavaScript 程式碼
- **Follow-up**: 建立共用 FileDownloadService 供未來其他匯出功能使用

### Decision: 交易成本使用 decimal 類型
- **Context**: 金額計算需高精度，避免浮點數誤差
- **Alternatives Considered**:
  1. double — 精度不足，可能產生誤差
  2. decimal — 128 位元高精度，適合財務計算
  3. int（以分為單位）— 避免小數但增加轉換複雜度
- **Selected Approach**: decimal 類型
- **Rationale**: C# decimal 專為財務計算設計，符合台股金額精度需求
- **Trade-offs**: 運算效能略低於 double，但金額計算量不大
- **Follow-up**: 確保所有 Entity 金額欄位使用 decimal

## Risks & Mitigations
- **交易成本計算錯誤** — 建立完整單元測試，包含邊界條件（最低手續費、大額交易）
- **CSV 匯出效能** — 大量資料時使用串流寫入，避免記憶體溢出
- **休市判斷不準確** — 提供手動維護假日清單的管理介面

## References
- [Microsoft Learn: ASP.NET Core Blazor dependency injection](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection) — DI 最佳實踐
- [Microsoft Learn: Blazor with Entity Framework Core](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core) — DbContextFactory 使用指南
- [Taiwan Stock Exchange: Holiday Schedule](https://www.twse.com.tw/en/trading/holiday.html) — 官方假日公告
- [CsvHelper Documentation](https://joshclose.github.io/CsvHelper/) — CSV 處理函式庫
