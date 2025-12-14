# Requirements Document

## Introduction

本文件定義 MyStockApp 股票交易功能的需求規格。此功能將使用戶能夠在應用程式中進行股票買賣操作，包括下單、訂單管理、交易紀錄查詢、交易成本計算等核心功能。系統基於現有的 Blazor Server 架構，整合 PostgreSQL 資料庫進行交易資料持久化，並建立股票基本資料表與歷史價格表作為報價資料來源。

## Glossary

- **Trading Service**: 股票交易服務，負責處理下單、訂單管理、交易紀錄與成本計算等交易相關邏輯
- **Stock Service**: 股票資料服務，負責管理股票基本資料、即時報價與歷史價格資料
- **手續費**: 券商收取的交易服務費，基礎費率為成交金額的 0.1425%
- **電子下單折扣**: 透過電子平台下單可享有的手續費折扣，通常為 6 折
- **證券交易稅**: 政府對股票賣出交易課徵的稅金，費率為成交金額的 0.3%
- **市價單（Market Order）**: 以當前市場價格立即成交的訂單類型
- **限價單（Limit Order）**: 指定價格，待市場價格達到指定價位時才成交的訂單類型
- **未實現損益**: 持有股票的帳面損益，尚未透過賣出實現
- **已實現損益**: 透過實際賣出交易所產生的損益

## Requirements

### Requirement 1: 股票下單功能

**Objective:** As a 投資者, I want 提交股票買入或賣出訂單, so that 能夠執行股票交易操作

#### Acceptance Criteria
1. When 使用者選擇股票並輸入買入數量後點擊「買入」按鈕, the Trading Service shall 建立買入訂單並儲存至資料庫
2. When 使用者選擇股票並輸入賣出數量後點擊「賣出」按鈕, the Trading Service shall 建立賣出訂單並儲存至資料庫
3. If 下單數量為零或負數, then the Trading Service shall 顯示錯誤訊息「請輸入有效的交易數量」
4. If 賣出數量超過持有股數, then the Trading Service shall 顯示錯誤訊息「賣出數量不可超過持有股數」
5. While 訂單正在處理中, the Trading Service shall 顯示載入指示器並禁用提交按鈕

### Requirement 2: 訂單類型支援

**Objective:** As a 投資者, I want 選擇不同的訂單類型, so that 能夠根據市場情況採用合適的交易策略

#### Acceptance Criteria
1. The Trading Service shall 支援市價單（Market Order）類型
2. The Trading Service shall 支援限價單（Limit Order）類型
3. When 使用者選擇限價單類型, the Trading Service shall 顯示價格輸入欄位
4. If 限價單未輸入價格, then the Trading Service shall 顯示錯誤訊息「限價單必須指定價格」
5. When 使用者選擇市價單類型, the Trading Service shall 隱藏價格輸入欄位並使用當前市價

### Requirement 3: 訂單管理

**Objective:** As a 投資者, I want 查看並管理我的訂單, so that 能夠追蹤訂單狀態並在必要時取消訂單

#### Acceptance Criteria
1. The Trading Service shall 顯示所有未成交訂單清單，包含股票代號、訂單類型、數量、價格與狀態
2. When 使用者點擊「取消訂單」按鈕, the Trading Service shall 將訂單狀態更新為「已取消」
3. If 訂單已成交或已取消, then the Trading Service shall 禁用該訂單的取消按鈕
4. When 訂單狀態變更時, the Trading Service shall 即時更新畫面顯示（透過 SignalR）
5. The Trading Service shall 支援依訂單狀態篩選（全部、待成交、已成交、已取消）

### Requirement 4: 交易紀錄

**Objective:** As a 投資者, I want 查看歷史交易紀錄, so that 能夠檢視過去的交易活動與績效

#### Acceptance Criteria
1. The Trading Service shall 顯示已完成交易的完整紀錄，包含交易日期、股票代號、交易類型、數量、成交價格、手續費、交易稅與淨金額
2. The Trading Service shall 支援依日期範圍篩選交易紀錄
3. The Trading Service shall 支援依股票代號搜尋交易紀錄
4. When 使用者點擊匯出按鈕, the Trading Service shall 產生 CSV 格式的交易紀錄檔案供下載（包含交易成本明細）
5. The Trading Service shall 依交易日期降序排列紀錄（最新交易在最前）

### Requirement 5: 持股部位顯示

**Objective:** As a 投資者, I want 查看當前持股部位, so that 能夠了解目前的投資組合配置

#### Acceptance Criteria
1. The Trading Service shall 顯示當前持有的所有股票，包含股票代號、持有數量、平均成本（含買入手續費）與當前市值
2. The Trading Service shall 計算並顯示每檔股票的未實現損益（當前市值 - 持股成本 - 預估賣出成本）
3. The Trading Service shall 計算並顯示每檔股票的報酬率百分比（考慮交易成本後的淨報酬率）
4. When 股價更新時, the Trading Service shall 即時更新市值與損益數據
5. The Trading Service shall 顯示投資組合總市值與總損益（含已實現與未實現損益）

### Requirement 6: 交易驗證與風控

**Objective:** As a 系統管理員, I want 系統執行基本的交易驗證, so that 能夠防止無效或高風險的交易操作

#### Acceptance Criteria
1. If 股票代號不存在或無效, then the Trading Service shall 拒絕訂單並顯示錯誤訊息
2. If 單筆交易金額超過設定上限, then the Trading Service shall 顯示警告訊息並要求使用者確認
3. The Trading Service shall 記錄所有交易操作至稽核日誌
4. While 市場休市時段, the Trading Service shall 顯示提示訊息「目前為休市時段，訂單將於開盤後處理」
5. The Trading Service shall 防止重複提交相同訂單（防呆機制）

### Requirement 7: 交易成本計算

**Objective:** As a 投資者, I want 系統自動計算交易手續費與稅金, so that 能夠準確掌握實際交易成本與淨損益

#### Acceptance Criteria
1. The Trading Service shall 依據成交金額計算手續費，基礎費率為 0.1425%
2. The Trading Service shall 支援電子下單折扣設定，預設折扣為 6 折（實際費率 0.0855%）
3. When 使用者執行賣出交易, the Trading Service shall 計算證券交易稅，費率為成交金額的 0.3%
4. When 使用者執行買入交易, the Trading Service shall 僅計算手續費，不計算交易稅
5. If 計算後手續費低於 20 元, then the Trading Service shall 以 20 元作為最低手續費
6. When 使用者輸入下單資訊後, the Trading Service shall 即時顯示預估交易成本明細（手續費、交易稅、總成本）
7. The Trading Service shall 在交易紀錄中顯示實際扣除的手續費與交易稅金額
8. The Trading Service shall 支援使用者自訂電子下單折扣比例（範圍 1 折至 10 折）

### Requirement 8: 股票基本資料管理

**Objective:** As a 投資者, I want 系統維護股票基本資料與即時報價, so that 能夠查詢股票資訊並取得交易所需的價格數據

#### Acceptance Criteria
1. The Stock Service shall 儲存股票基本資料，包含股票代號、股票名稱、市場別（上市/上櫃）與產業類別
2. The Stock Service shall 儲存股票即時報價，包含當前價格、開盤價、最高價、最低價、成交量與更新時間
3. When 使用者搜尋股票代號或名稱, the Stock Service shall 回傳符合條件的股票清單
4. The Stock Service shall 支援依市場別或產業類別篩選股票
5. When 股票報價資料更新時, the Stock Service shall 記錄更新時間戳記
6. The Stock Service shall 提供股票報價查詢介面供交易功能使用

### Requirement 9: 股價歷史資料

**Objective:** As a 投資者, I want 查看股票歷史價格走勢, so that 能夠分析股票過去表現並輔助投資決策

#### Acceptance Criteria
1. The Stock Service shall 儲存股票每日收盤資料，包含日期、開盤價、最高價、最低價、收盤價與成交量
2. The Stock Service shall 支援查詢指定日期範圍的歷史價格資料
3. When 使用者選擇股票並指定日期範圍, the Stock Service shall 回傳該期間的歷史價格清單
4. The Stock Service shall 支援計算指定期間的價格統計（最高、最低、平均、漲跌幅）
5. The Stock Service shall 保留至少 1 年的歷史價格資料
6. When 每個交易日結束後, the Stock Service shall 將當日收盤資料寫入歷史紀錄
