# Alpha Squeeze - 戰術級量化決策支援平台

## 專案概述
本系統為針對台股 (TWSE/TPEx) 的量化交易決策支援平台，透過盤後籌碼大數據與衍生性商品市場訊號，找出具備「高壓縮、高爆發」軋空潛力的標的。

## 技術架構

### 異構雙核設計
- **運算核心**: Python (Polars, gRPC Server)
- **業務核心**: .NET 8 (ASP.NET Core, Dapper)
- **前端**: React + TypeScript
- **資料庫**: MSSQL (SQL Server)

### 核心技術棧
| 層級 | 技術 |
|------|------|
| Data Access | Dapper (MSSQL) |
| Data Processing | Polars (Python) |
| Cross-Language Communication | gRPC (squeeze.proto) |
| Web Scraping | Playwright (Python) |
| External API | FinMind API |
| Notification | LINE Notify |

## 資料庫 Schema

### DailyStockMetrics (股票日指標表)
- `Ticker`: 股票代號
- `TradeDate`: 交易日期
- `ClosePrice`: 收盤價
- `BorrowingBalanceChange`: 借券賣出餘額增減 (核心指標)
- `MarginRatio`: 券資比 (%)
- `HistoricalVolatility20D`: 20日歷史波動率 (HV)
- `Volume`: 成交量

### WarrantMarketData (權證實時數據表)
- `UnderlyingTicker`: 標的代號
- `WarrantTicker`: 權證代號
- `ImpliedVolatility`: 隱含波動率 (IV)
- `EffectiveLeverage`: 實質槓桿
- `SpreadRatio`: 差槓比
- `ExpiryDate`: 到期日

## 核心演算法

### Squeeze Score 計算公式
```
S = (W_B × F_B) + (W_G × F_G) + (W_M × F_M) + (W_V × F_V)
```

| 維度 | 參數 | 權重 | 判定邏輯 |
|------|------|------|----------|
| 法人空頭 (B) | 借券賣出餘額變化 | 35% | 負值（回補）越多，得分越高 |
| Gamma 效應 (G) | IV - HV 乖離 | 25% | 若 IV < HV，得分越高 |
| 散戶燃料 (M) | 券資比 | 20% | 數值越高（空單擁擠），得分越高 |
| 價量動能 (V) | 價格與量能組合 | 20% | 帶量突破壓力位，得分越高 |

## 系統流程 (Pipeline)

### Step 1: 盤後數據擷取 (Python Worker)
- **時間**: 每日 18:30
- **FinMind API**: 抓取全台股「借券賣出餘額」與「券資比」
- **Playwright Scraper**: 掃描權證發行商網站，提取權證 IV

### Step 2: 訊號分析 (Python gRPC Server)
- 計算 20 日滾動歷史波動率 (HV)
- 執行 Squeeze Score 公式
- 標註「低/中/高」軋空潛力

### Step 3: 前端呈現與通知 (.NET + React)
- .NET API 暴露 Endpoint
- LINE Notify: 每晚 20:00 推播「明日高勝率軋空清單」
- React Dashboard: 視覺化 IV/HV 走勢圖

## 開發規範

### Dapper 效能優化
- 使用 `ExecuteBatch` 或 MSSQL BulkCopy 處理大量寫入
- 避免 N+1 查詢問題

### Scraper 穩定性
- Playwright 加入 Retry 機制
- 爬取失敗記錄到 Log，避免資料缺口

### gRPC 異常處理
- Python 引擎未啟動時，.NET 客戶端執行降級處理 (Degradation)
- 先顯示基本籌碼數據

## 目錄結構
```
Alpha-Squeeze/
├── src/
│   ├── AlphaSqueeze.Api/          # .NET Web API
│   ├── AlphaSqueeze.Core/         # 核心業務邏輯
│   ├── AlphaSqueeze.Data/         # Dapper 資料存取層
│   └── AlphaSqueeze.Shared/       # 共用類別與 gRPC 定義
├── python/
│   ├── engine/                    # gRPC Server & Quant 計算
│   ├── scrapers/                  # Playwright 爬蟲
│   └── workers/                   # 排程任務
├── web/                           # React 前端
├── proto/                         # gRPC Proto 定義
├── database/                      # SQL Scripts
└── tests/                         # 測試專案
```

## 常用指令

### .NET
```bash
dotnet build                       # 編譯專案
dotnet run --project src/AlphaSqueeze.Api  # 啟動 API
dotnet test                        # 執行測試
```

### Python
```bash
python -m engine.server            # 啟動 gRPC Server
python -m workers.daily_fetch      # 執行每日數據擷取
python -m scrapers.warrant         # 執行權證爬蟲
```

### Database
```bash
sqlcmd -S localhost -d AlphaSqueeze -i database/schema.sql
```
