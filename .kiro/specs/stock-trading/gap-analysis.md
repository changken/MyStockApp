# 差距分析報告：股票交易功能

## 1. 現有系統調查

### 1.1 關鍵檔案與目錄結構

| 類型 | 路徑 | 說明 |
|------|------|------|
| DbContext | `Data/AppDbContext.cs` | EF Core 資料庫上下文，含自動時間戳更新邏輯 |
| 資料模型 | `Data/Models/StockWatchlist.cs` | 股票追蹤清單實體，含驗證屬性 |
| 頁面元件 | `Components/Pages/StockWatchlist.razor` | 股票追蹤 CRUD 頁面，可作為參考範本 |
| 導航選單 | `Components/Layout/NavMenu.razor` | 需擴充交易相關導航項目 |
| 程式進入點 | `Program.cs` | 服務註冊與 DI 配置 |

### 1.2 架構模式與慣例

- **架構風格**：Blazor Server 單體應用，所有邏輯在伺服器端執行
- **資料存取**：`IDbContextFactory<AppDbContext>` 模式（適合 Blazor Server 短生命週期）
- **服務層**：目前無獨立 Service 層，業務邏輯直接在 Razor 元件中實作
- **驗證**：使用 Data Annotations + `EditForm` + `DataAnnotationsValidator`
- **UI 框架**：Bootstrap 5（已整合）
- **資料庫**：PostgreSQL + EF Core 10.0

### 1.3 可重用元件

- **時間戳自動更新**：`AppDbContext` 已實作 `UpdateTimestamps()` 方法
- **表單模式**：`StockWatchlist.razor` 展示標準 CRUD 表單模式（載入狀態、錯誤處理、確認對話框）
- **UI 元件**：載入指示器、訊息提示、Modal 對話框等已有實作範例

---

## 2. 需求可行性分析

### 2.1 技術需求與缺口對照表

| 需求 | 技術需求 | 現有資產 | 缺口狀態 |
|------|----------|----------|----------|
| **R1: 股票下單** | Order Entity、下單表單元件 | 無 | 🔴 Missing |
| **R2: 訂單類型** | OrderType Enum、限價/市價邏輯 | 無 | 🔴 Missing |
| **R3: 訂單管理** | 訂單清單元件、狀態篩選 | 類似模式於 StockWatchlist.razor | 🟡 Partial |
| **R4: 交易紀錄** | Trade Entity、CSV 匯出功能 | 無 | 🔴 Missing |
| **R5: 持股部位** | Portfolio Entity、損益計算邏輯 | 無 | 🔴 Missing |
| **R6: 交易驗證** | 驗證服務、稽核日誌 | Data Annotations 可重用 | 🟡 Partial |
| **R7: 交易成本計算** | 手續費/交易稅計算服務、折扣設定 | 無 | 🔴 Missing |
| **R8: 股票基本資料** | Stock Entity、報價資料、搜尋功能 | StockWatchlist 可參考 | 🟡 Partial |
| **R9: 股價歷史資料** | StockPriceHistory Entity、統計計算 | 無 | 🔴 Missing |

### 2.2 需新建的資料模型

```
Stock (股票基本資料)
├── Id, Symbol, Name
├── Market (Listed/OTC), Industry
├── CurrentPrice, OpenPrice, HighPrice, LowPrice
├── Volume, LastUpdated
└── CreatedAt, UpdatedAt

StockPriceHistory (股價歷史)
├── Id, StockId (FK)
├── Date, OpenPrice, HighPrice, LowPrice, ClosePrice
├── Volume
└── CreatedAt

Order (訂單)
├── Id, StockId (FK)
├── OrderType (Market/Limit)
├── Side (Buy/Sell)
├── Quantity, Price, Status
├── Commission, TransactionTax
└── CreatedAt, UpdatedAt

Trade (成交紀錄)
├── Id, OrderId (FK)
├── StockSymbol, Side
├── Quantity, ExecutedPrice, TotalAmount
├── Commission, TransactionTax, NetAmount
└── ExecutedAt

Portfolio (持股部位)
├── Id, StockId (FK)
├── Quantity, AverageCost
├── TotalCost, RealizedPnL
└── UpdatedAt

UserSettings (使用者設定)
├── Id, UserId
├── CommissionDiscount (電子下單折扣)
├── MaxTradeAmount (單筆上限)
└── UpdatedAt

AuditLog (稽核日誌)
├── Id, Action, EntityType, EntityId
├── OldValue, NewValue, UserId
└── CreatedAt
```

### 2.3 需新建的服務

| 服務 | 職責 | 優先級 |
|------|------|--------|
| **TradingService** | 下單處理、訂單管理、狀態流轉 | P0 |
| **TradingCostService** | 手續費計算、交易稅計算、成本預估 | P0 |
| **PortfolioService** | 持股計算、損益計算、平均成本更新 | P1 |
| **StockService** | 股票查詢、報價管理、歷史資料 | P1 |
| **AuditService** | 稽核日誌記錄 | P2 |

### 2.4 複雜度訊號

| 類型 | 描述 |
|------|------|
| 📊 CRUD 操作 | 訂單、交易紀錄、持股部位、股票資料的基本 CRUD |
| 🧮 業務邏輯 | 手續費計算（含最低收費）、交易稅計算、持股平均成本、損益計算 |
| 📤 匯出功能 | CSV 檔案產生與下載（含交易成本明細） |
| 🔄 即時更新 | 訂單狀態變更、股價更新（已有 SignalR 基礎建設） |
| 📈 統計計算 | 歷史價格統計（最高、最低、平均、漲跌幅） |

### 2.5 待研究項目

- **Research Needed**: 台股市場休市時段判斷邏輯（API 或本地規則）
- **Research Needed**: CSV 匯出在 Blazor Server 的最佳實踐
- **Research Needed**: 股價歷史資料初始匯入方式

---

## 3. 實作方案選項

### Option A: 擴充現有元件

**適用時機**：快速 MVP，最小化新檔案數量

**擴充項目**：
- 在 `AppDbContext` 新增 DbSet（Stock, Order, Trade, Portfolio, StockPriceHistory, UserSettings, AuditLog）
- 在 `Data/Models/` 新增對應 Entity 類別
- 直接在 Razor Page 中實作業務邏輯（類似 StockWatchlist.razor）

**取捨**：
- ✅ 開發快速，遵循現有模式
- ✅ 無需學習新架構
- ❌ 業務邏輯與 UI 耦合
- ❌ 交易成本計算邏輯難以測試
- ❌ 隨功能增長難以維護

### Option B: 建立獨立服務層

**適用時機**：中長期維護性考量，複雜業務邏輯

**新建項目**：
- `Services/ITradingService.cs` + `TradingService.cs`（訂單處理、驗證邏輯）
- `Services/ITradingCostService.cs` + `TradingCostService.cs`（成本計算）
- `Services/IPortfolioService.cs` + `PortfolioService.cs`（持股計算）
- `Services/IStockService.cs` + `StockService.cs`（股票資料管理）
- `Services/IAuditService.cs` + `AuditService.cs`（稽核日誌）
- `Data/Models/` 新增 Entity 類別
- Razor Pages 透過 DI 注入 Service

**取捨**：
- ✅ 關注點分離，可測試性高
- ✅ 交易成本計算可獨立單元測試
- ✅ 業務邏輯集中管理
- ✅ 未來可擴展為 API 端點
- ❌ 初始開發時間較長
- ❌ 更多檔案需管理

### Option C: 混合式方案（推薦）

**適用時機**：平衡開發速度與可維護性

**策略**：
- **Phase 1**：核心交易服務（TradingService + TradingCostService）+ 基礎資料模型
- **Phase 2**：StockService（股票基本資料與報價）+ 頁面元件
- **Phase 3**：PortfolioService + 歷史價格功能 + 統計計算

**新建項目**：
- 2 個核心服務（TradingService, TradingCostService）
- 7 個 Entity（Stock, StockPriceHistory, Order, Trade, Portfolio, UserSettings, AuditLog）
- 5-6 個新 Razor Pages

**取捨**：
- ✅ 快速起步但保留擴展性
- ✅ 交易成本計算邏輯獨立可測試
- ✅ 可依專案演進調整架構
- ❌ 需注意架構一致性

---

## 4. 交易成本計算規則（R7 詳細分析）

### 4.1 手續費計算規則

```
基礎費率 = 0.1425%
電子下單折扣 = 使用者設定（預設 6 折）
實際費率 = 基礎費率 × 折扣
手續費 = 成交金額 × 實際費率
最終手續費 = MAX(手續費, 20)  // 最低 20 元
```

### 4.2 證券交易稅規則

```
稅率 = 0.3%（僅賣出時收取）
交易稅 = 成交金額 × 0.3%
```

### 4.3 總成本計算

| 交易類型 | 成本組成 |
|----------|----------|
| 買入 | 成交金額 + 手續費 |
| 賣出 | 成交金額 - 手續費 - 交易稅 |

### 4.4 損益計算

```
未實現損益 = 當前市值 - 持股成本 - 預估賣出成本（手續費 + 交易稅）
已實現損益 = 賣出淨額 - 買入成本
報酬率 = 未實現損益 / 持股成本 × 100%
```

---

## 5. 工作量與風險評估

### 工作量評估：**XL（2+ 週）**

**理由**：
- 需新建 7 個資料模型與對應遷移
- 需實作複雜業務邏輯（交易成本計算、損益計算、平均成本更新）
- 需建立 2+ 核心服務
- 需建立 5-6 個新頁面元件
- 股票基本資料與歷史價格功能增加範圍
- CSV 匯出需額外研究

### 風險評估：**中等 (Medium)**

**高風險因素**：
- 交易成本計算規則需確保準確性（涉及金額）
- 股價歷史資料匯入來源未確定

**可控因素**：
- 架構模式清晰，可參考 StockWatchlist 實作
- EF Core 遷移流程熟悉
- Bootstrap UI 元件齊全
- 交易成本計算規則已明確定義於需求

---

## 6. 設計階段建議

### 推薦方案：Option C（混合式方案）

**關鍵決策點**：
1. 優先實作 `TradingCostService`，確保交易成本計算的可測試性與準確性
2. 採用 `TradingService` 封裝訂單處理與驗證邏輯
3. Entity 設計需支援未來擴展（如多用戶、多市場）
4. 優先實作訂單與交易成本功能，股價歷史可先用模擬資料

### 待研究項目（設計階段）

| 項目 | 優先級 | 說明 |
|------|--------|------|
| CSV 匯出實作 | P1 | Blazor Server 檔案下載最佳實踐 |
| 市場休市判斷 | P2 | 台股交易日曆規則 |
| 歷史資料匯入 | P3 | 初始股價資料來源與匯入機制 |

### 資料模型外鍵關係

```
Stock (股票基本資料)
  ├──→ StockPriceHistory (1:N)
  ├──→ Order (1:N)
  └──→ Portfolio (1:N)

Order ─────→ Trade (1:N)
     ↓
Portfolio (由 Trade 計算更新)

UserSettings (獨立，未來可關聯 User)
AuditLog (獨立，記錄所有操作)
```

### 交易成本計算服務介面建議

```csharp
public interface ITradingCostService
{
    /// <summary>計算手續費</summary>
    decimal CalculateCommission(decimal amount, decimal discountRate = 0.6m);

    /// <summary>計算證券交易稅（僅賣出）</summary>
    decimal CalculateTransactionTax(decimal amount);

    /// <summary>計算交易總成本</summary>
    TradingCost CalculateTotalCost(decimal amount, TradeSide side, decimal discountRate = 0.6m);

    /// <summary>預估損益</summary>
    PnLEstimate EstimatePnL(decimal currentPrice, int quantity, decimal averageCost, decimal discountRate = 0.6m);
}
```
