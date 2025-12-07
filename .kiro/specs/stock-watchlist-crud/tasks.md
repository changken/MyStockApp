# Implementation Plan

## Task Summary
- **Total**: 5 major tasks, 17 sub-tasks
- **Requirements Coverage**: All 6 requirements mapped
- **Parallel Execution**: Enabled (tasks marked with (P))
- **Estimated Duration**: 1-3 hours per sub-task

---

## Tasks

- [x] 1. 建立資料模型與資料庫結構
- [x] 1.1 建立 StockWatchlist Entity
  - 定義 StockWatchlist 類別於 `/Data/Models/` 目錄
  - 設定屬性：Id, StockSymbol, StockName, Notes, CreatedAt, UpdatedAt
  - 加入 DataAnnotations 驗證屬性（Required, RegularExpression, MaxLength）
  - 確保 StockSymbol 使用 4 位數字正則表達式驗證
  - 設定 Nullable Reference Types（Notes 可為 null）
  - _Requirements: 5, 6_

- [x] 1.2 擴充 AppDbContext
  - 於 AppDbContext.cs 新增 `DbSet<StockWatchlist> StockWatchlists` 屬性
  - 在 OnModelCreating 方法配置 StockSymbol 唯一索引
  - 配置 CreatedAt 預設值為 CURRENT_TIMESTAMP（新增時）
  - 實作 UpdatedAt 自動更新機制（於 SaveChangesAsync 前手動設定或使用 Interceptor）
  - _Requirements: 6_

- [x] 1.3 建立與執行 EF Core Migration
  - 執行 `dotnet ef migrations add AddStockWatchlist` 建立 Migration
  - 檢視產生的 Migration 檔案，確認資料表結構正確
  - 驗證唯一索引與預設值設定（StockSymbol UNIQUE, CreatedAt/UpdatedAt DEFAULT）
  - 於測試環境執行 `dotnet ef database update`
  - 驗證資料表建立成功，測試插入重複 StockSymbol 應失敗
  - _Requirements: 6_

---

- [ ] 2. 實作股票追蹤清單頁面基礎結構
- [ ] 2.1 建立 StockWatchlist.razor 頁面骨架
  - 建立 StockWatchlist.razor 檔案於 `/Components/Pages/` 目錄
  - 設定路由指令 `@page "/stocks"`
  - 注入 `IDbContextFactory<AppDbContext>` 依賴
  - 定義頁面級狀態變數（stocks, currentStock, isEditing, showDeleteModal, stockToDelete, errorMessage, successMessage, isLoading）
  - 實作 OnInitializedAsync 方法載入股票清單
  - _Requirements: 2_

- [ ] 2.2 實作清單顯示與載入指示器
  - 設計股票清單 UI 表格或卡片佈局，顯示股票代號、名稱、備註
  - 加入編輯與刪除按鈕於每筆股票項目
  - 實作載入指示器（使用 Bootstrap Spinner），依 isLoading 狀態顯示
  - 處理空清單情境，顯示「目前無追蹤股票」提示訊息
  - 實作依 CreatedAt 倒序排列清單（最新在前）
  - _Requirements: 2_

---

- [ ] 3. 實作新增與編輯股票功能
- [ ] 3.1 建立新增/編輯表單 UI
  - 使用 EditForm 元件綁定 currentStock 模型
  - 加入 DataAnnotationsValidator 與 ValidationSummary 元件
  - 設計表單欄位：股票代號（input text）、股票名稱（input text）、備註（textarea）
  - 根據 isEditing 狀態調整表單標題與按鈕文字（新增 vs 編輯）
  - 股票代號欄位於編輯模式設為唯讀（disabled 或 readonly）
  - 加入「儲存」與「取消」按鈕
  - _Requirements: 1, 3_

- [ ] 3.2 (P) 實作新增股票邏輯
  - 實作「新增股票」按鈕點擊事件，顯示空白表單
  - 實作表單提交邏輯（HandleValidSubmit 方法）
  - 建立 DbContext 執行 Add 與 SaveChangesAsync 操作
  - 捕獲 DbUpdateException 處理唯一約束違反，顯示「股票代號已存在」錯誤
  - 新增成功後顯示成功訊息，重新載入清單，清空表單
  - 手動設定 CreatedAt 與 UpdatedAt 為當前時間（DateTime.UtcNow）
  - _Requirements: 1, 5_

- [ ] 3.3 (P) 實作編輯股票邏輯
  - 實作「編輯」按鈕點擊事件，預填 currentStock 資料並顯示表單
  - 實作表單提交邏輯（與新增共用 HandleValidSubmit，依 isEditing 分支）
  - 建立 DbContext 執行 Update 與 SaveChangesAsync 操作
  - 手動設定 UpdatedAt 為當前時間（DateTime.UtcNow）
  - 更新成功後顯示成功訊息，重新載入清單，關閉表單
  - 實作「取消」按鈕邏輯，關閉表單且不儲存變更
  - _Requirements: 3, 5_

---

- [ ] 4. 實作刪除股票功能
- [ ] 4.1 建立刪除確認 Modal UI
  - 於 StockWatchlist.razor 內嵌 Bootstrap Modal 標記
  - Modal 內容顯示待刪除股票的代號與名稱（從 stockToDelete 狀態讀取）
  - 加入「確認刪除」與「取消」按鈕
  - 實作 Modal 開關狀態控制（透過 showDeleteModal 布林值）
  - _Requirements: 4_

- [ ] 4.2 (P) 實作刪除股票邏輯
  - 實作「刪除」按鈕點擊事件，設定 stockToDelete 並顯示 Modal
  - 實作「確認刪除」按鈕邏輯，建立 DbContext 執行 Remove 與 SaveChangesAsync
  - 捕獲 Exception 處理刪除失敗，顯示錯誤訊息
  - 刪除成功後顯示成功訊息，重新載入清單，關閉 Modal
  - 實作「取消」按鈕邏輯，關閉 Modal 且不執行刪除
  - _Requirements: 4, 5_

---

- [ ] 5. 整合導航、錯誤處理與測試
- [ ] 5.1 整合導航選單
  - 於 NavMenu.razor 新增「股票追蹤」連結，路由至 `/stocks`
  - 確保連結文字與圖示符合既有 NavMenu 樣式慣例
  - 測試導航連結點擊後正確跳轉至股票追蹤頁面
  - _Requirements: 2_

- [ ] 5.2 實作錯誤訊息與成功訊息顯示
  - 使用 Bootstrap Alert 元件顯示 errorMessage（紅色警示框）
  - 使用 Bootstrap Alert 元件顯示 successMessage（綠色警示框）
  - 實作自動清除訊息機制（操作成功或失敗後顯示，下次操作前清空）
  - 確保所有資料庫操作異常被 try-catch 包裝，轉換為友善訊息
  - 記錄例外至 Console.Error（`Console.Error.WriteLine(ex.Message)`）
  - _Requirements: 5_

- [ ] 5.3 驗證 Blazor Server 連線中斷處理
  - 確認既有 ReconnectModal.razor 仍正常運作
  - 測試 SignalR 連線中斷情境（模擬網路中斷），驗證重連提示顯示
  - 無需額外實作，僅驗證既有機制涵蓋股票追蹤頁面
  - _Requirements: 5_

- [ ] 5.4 端對端功能測試
  - 測試完整新增流程：填寫表單 → 提交 → 驗證清單更新 → 驗證成功訊息
  - 測試完整編輯流程：點擊編輯 → 預填表單 → 修改 → 提交 → 驗證更新
  - 測試完整刪除流程：點擊刪除 → 確認 Modal → 確認刪除 → 驗證清單更新
  - 測試驗證錯誤顯示：提交空表單 → 驗證錯誤訊息顯示於表單下方
  - 測試重複股票代號處理：新增既有代號 → 驗證錯誤 Alert 顯示
  - 測試空清單顯示：刪除所有股票 → 驗證「目前無追蹤股票」提示
  - 測試清單排序：新增多筆股票 → 驗證依建立時間倒序排列
  - _Requirements: 1, 2, 3, 4, 5_

- [ ]* 5.5 單元測試與整合測試（可選）
  - 撰寫 StockWatchlist Entity 驗證規則單元測試（Required, RegularExpression, MaxLength）
  - 撰寫 StockSymbol 格式驗證測試（有效：1234, 2330；無效：123, 12345, AAPL）
  - 撰寫 CRUD 操作整合測試（使用測試資料庫或 InMemory Database）
  - 撰寫唯一約束測試（重複 StockSymbol 應拋出 DbUpdateException）
  - 撰寫 UpdatedAt 自動更新測試（驗證編輯後時間戳更新）
  - _Requirements: 1, 2, 3, 4, 5, 6_

---

## Requirements Coverage Matrix

| Requirement | Tasks |
|-------------|-------|
| 1 (新增股票追蹤項目) | 3.1, 3.2, 5.4 |
| 2 (查詢股票追蹤清單) | 2.1, 2.2, 5.1, 5.4 |
| 3 (更新股票追蹤項目) | 3.1, 3.3, 5.4 |
| 4 (刪除股票追蹤項目) | 4.1, 4.2, 5.4 |
| 5 (資料驗證與錯誤處理) | 1.1, 3.2, 3.3, 4.2, 5.2, 5.3, 5.4, 5.5 |
| 6 (資料持久化) | 1.1, 1.2, 1.3, 5.5 |

## Parallel Execution Strategy

**Tasks marked with (P) can be executed in parallel**:
- Task 3.2 (新增邏輯) 與 Task 3.3 (編輯邏輯) 可並行實作（共用表單 UI，邏輯獨立）
- Task 4.2 (刪除邏輯) 可與 Task 3.2/3.3 並行（操作不同資料流程）

**Sequential Dependencies**:
- Task 1.x 必須依序完成（Entity → DbContext → Migration）
- Task 2.1 必須於 Task 1.3 完成後（需資料庫結構）
- Task 3.x, 4.x 必須於 Task 2.1 完成後（需頁面骨架）
- Task 5.x 必須於所有功能實作完成後（整合與測試）

## Implementation Notes

- **UpdatedAt 自動更新**: 於 AppDbContext 的 SaveChangesAsync 方法 override 中實作，或於 CRUD 方法中手動設定
- **StockForm 與 DeleteModal**: 採用內嵌於 StockWatchlist.razor 的 Razor 標記區塊，非獨立元件檔案
- **錯誤處理**: 所有資料庫操作使用 try-catch 包裝，DbUpdateException 特別處理唯一約束違反
- **測試策略**: Task 5.5 為可選測試任務，核心驗收透過 Task 5.4 端對端測試完成
