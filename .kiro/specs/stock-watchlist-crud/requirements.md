# Requirements Document

## Project Description (Input)
我需要實作一個台股的股票追蹤清單 CRUD

## Introduction
本需求文件定義台股股票追蹤清單的完整 CRUD 功能，讓使用者能透過 Blazor Server 界面新增、查詢、修改與刪除股票追蹤項目，並將資料持久化至 PostgreSQL 資料庫。

## Requirements

### Requirement 1: 新增股票追蹤項目
**Objective:** 作為使用者，我想要新增台股股票至追蹤清單，以便持續追蹤特定股票的資訊。

#### Acceptance Criteria
1. When 使用者點擊「新增股票」按鈕, the 股票追蹤系統 shall 顯示新增表單
2. The 股票追蹤系統 shall 要求使用者輸入股票代號（必填）
3. The 股票追蹤系統 shall 要求使用者輸入股票名稱（必填）
4. The 股票追蹤系統 shall 允許使用者輸入備註（選填）
5. When 使用者提交新增表單且所有必填欄位已填寫, the 股票追蹤系統 shall 儲存股票至資料庫
6. When 股票新增成功, the 股票追蹤系統 shall 顯示成功訊息並更新追蹤清單
7. If 使用者提交表單時必填欄位為空, then the 股票追蹤系統 shall 顯示驗證錯誤訊息
8. If 股票代號已存在於追蹤清單中, then the 股票追蹤系統 shall 顯示錯誤訊息並阻止重複新增

### Requirement 2: 查詢股票追蹤清單
**Objective:** 作為使用者，我想要查看所有追蹤中的股票清單，以便掌握追蹤項目的整體概況。

#### Acceptance Criteria
1. When 使用者進入股票追蹤頁面, the 股票追蹤系統 shall 顯示所有追蹤中的股票清單
2. The 股票追蹤系統 shall 顯示每筆股票的代號、名稱與備註
3. If 追蹤清單為空, then the 股票追蹤系統 shall 顯示提示訊息「目前無追蹤股票」
4. The 股票追蹤系統 shall 依新增時間倒序排列股票清單（最新在前）
5. While 資料載入中, the 股票追蹤系統 shall 顯示載入指示器

### Requirement 3: 更新股票追蹤項目
**Objective:** 作為使用者，我想要修改已追蹤股票的資訊，以便更新備註或修正錯誤資訊。

#### Acceptance Criteria
1. When 使用者點擊清單中的「編輯」按鈕, the 股票追蹤系統 shall 顯示編輯表單並預填現有資料
2. The 股票追蹤系統 shall 允許使用者修改股票名稱與備註
3. The 股票追蹤系統 shall 禁止修改股票代號（唯讀欄位）
4. When 使用者提交編輯表單且所有必填欄位已填寫, the 股票追蹤系統 shall 更新資料庫中的股票資訊
5. When 股票更新成功, the 股票追蹤系統 shall 顯示成功訊息並更新追蹤清單顯示
6. If 使用者提交表單時必填欄位為空, then the 股票追蹤系統 shall 顯示驗證錯誤訊息
7. When 使用者點擊「取消」按鈕, the 股票追蹤系統 shall 關閉編輯表單且不儲存變更

### Requirement 4: 刪除股票追蹤項目
**Objective:** 作為使用者，我想要移除不再需要追蹤的股票，以便維持清單的整潔與相關性。

#### Acceptance Criteria
1. When 使用者點擊清單中的「刪除」按鈕, the 股票追蹤系統 shall 顯示確認對話框
2. The 股票追蹤系統 shall 在確認對話框中顯示將被刪除的股票代號與名稱
3. When 使用者確認刪除, the 股票追蹤系統 shall 從資料庫中移除該股票
4. When 股票刪除成功, the 股票追蹤系統 shall 顯示成功訊息並更新追蹤清單
5. When 使用者取消刪除, the 股票追蹤系統 shall 關閉確認對話框且不執行刪除操作
6. If 資料庫刪除操作失敗, then the 股票追蹤系統 shall 顯示錯誤訊息

### Requirement 5: 資料驗證與錯誤處理
**Objective:** 作為使用者，我想要獲得清楚的驗證與錯誤訊息，以便正確操作系統。

#### Acceptance Criteria
1. The 股票追蹤系統 shall 驗證股票代號格式為 4 位數字（台股標準格式）
2. The 股票追蹤系統 shall 驗證股票名稱長度不超過 100 字元
3. The 股票追蹤系統 shall 驗證備註長度不超過 500 字元
4. If 任何資料庫操作失敗, then the 股票追蹤系統 shall 顯示友善的錯誤訊息而非技術細節
5. If Blazor Server 連線中斷, then the 股票追蹤系統 shall 顯示重新連線提示（使用既有的 ReconnectModal）

### Requirement 6: 資料持久化
**Objective:** 作為系統管理員，我想要確保股票追蹤資料安全儲存，以便使用者資料不會遺失。

#### Acceptance Criteria
1. The 股票追蹤系統 shall 使用 Entity Framework Core 10.0 管理資料存取
2. The 股票追蹤系統 shall 透過 AppDbContext 與 PostgreSQL 資料庫互動
3. The 股票追蹤系統 shall 使用 IDbContextFactory<AppDbContext> 注入模式（遵循 Blazor Server 最佳實踐）
4. The 股票追蹤系統 shall 儲存股票代號、名稱、備註、建立時間與更新時間
5. The 股票追蹤系統 shall 為股票代號欄位建立唯一索引以防止重複
6. The 股票追蹤系統 shall 支援本地開發（User Secrets）與生產環境（DATABASE_URL）的連線設定

