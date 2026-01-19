# Feature: CB 預警燈功能開發指引

## 功能概述

**CB (可轉換公司債) 預警燈**是 Alpha Squeeze V1.2 的核心功能擴充，旨在透過監控可轉債的「贖回觸發天數」(Days Above Trigger) 與「剩餘餘額」，為投資人提供潛在的強制贖回風險預警。

### 核心策略：CB 誘發劇烈軋空的原理

可轉債 (Convertible Bonds, CB) 在台股市場是極其精準的**軋空先行指標**。其核心驅動力來自「強制贖回條款 (Call Provision)」引發的連鎖反應。

#### 1. 130% 觸發線與 30 日規則

根據台灣 CB 發行合約，當標的股價**連續 30 個營業日**超過轉換價的 **130%** 時：
```
P_stock >= 1.3 × P_conv
```
發行公司有權行使強制贖回。此時債券帳面價值遠高於面額 (100 元)，迫使債券持有人必須在期限內轉成現股。

#### 2. 資產交換 (AS) 下的空頭補回壓力

許多法人（如避險基金、大戶）透過**資產交換 (Asset Swap, AS)** 拆解 CB，並在現貨市場放空股票進行避險 (Hedge)。

- **軋空觸發點**：當公司公告強制贖回，這些避險空頭必須在短時間內「不計代價」從市場買回股票回補
- **連鎖反應**：此回補力量為「非自願性買盤」，往往在股價高檔形成二次噴發，造成劇烈軋空

#### 3. 公司派誘發意圖

公司發行 CB 通常不希望到期還錢（債務壓力），而希望債權人轉為股本。因此在股價接近 130% 門檻時，公司派常具備強烈動機透過利多發佈或拉抬來維持股價，以順利完成「債轉股」。

### 業務價值
- 當 CB 標的股價連續 30 日收盤價超過轉換價的 130%，發行公司有權提前贖回
- 此機制會導致 CB 持有人被迫轉換或賣出，形成潛在的籌碼壓力或軋空動能
- 整合至現有 Squeeze Score 系統，提供更完整的決策支援
- 洞察「強制性買盤」的能力，當其他技術指標失效時提供最終的軋空方向指引

---

## 技術架構設計

### 資料流程架構

```
┌──────────────────────────────────────────────────────────────────────┐
│                        CB Warning Light Pipeline                      │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐           │
│  │  櫃買中心   │────▶│  Python     │────▶│  MSSQL      │           │
│  │  CB 餘額    │     │  Scraper    │     │  Database   │           │
│  │  網站       │     │             │     │             │           │
│  └─────────────┘     └─────────────┘     └──────┬──────┘           │
│                                                  │                   │
│  ┌─────────────┐     ┌─────────────┐            │                   │
│  │  FinMind    │────▶│  Daily      │────────────┤                   │
│  │  股價資料   │     │  Fetch      │            │                   │
│  └─────────────┘     └─────────────┘            │                   │
│                                                  ▼                   │
│                                         ┌─────────────┐             │
│                                         │  Polars     │             │
│                                         │  Engine     │             │
│                                         │             │             │
│                                         │  - Days     │             │
│                                         │    Above    │             │
│                                         │    Trigger  │             │
│                                         │  - CB Score │             │
│                                         └──────┬──────┘             │
│                                                │                     │
│                              ┌─────────────────┼─────────────────┐  │
│                              ▼                 ▼                 ▼  │
│                      ┌─────────────┐   ┌─────────────┐   ┌────────┐│
│                      │  .NET API   │   │  React      │   │  LINE  ││
│                      │  Endpoint   │   │  Dashboard  │   │  Notify││
│                      └─────────────┘   └─────────────┘   └────────┘│
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

### 新增元件清單

| 元件 | 檔案路徑 | 說明 |
|------|----------|------|
| CB Scraper | `python/scrapers/cb_tpex_scraper.py` | 櫃買中心 CB 餘額爬蟲 |
| CB Calculator | `python/engine/cb_calculator.py` | DaysAboveTrigger 計算 |
| CB Repository | `src/AlphaSqueeze.Data/Repositories/CBRepository.cs` | CB 資料存取層 |
| CB Service | `src/AlphaSqueeze.Core/Services/CBWarningService.cs` | CB 預警業務邏輯 |
| CB API Controller | `src/AlphaSqueeze.Api/Controllers/CBController.cs` | REST API Endpoint |
| CB Balance Bar | `web/src/components/charts/CBBalanceBar.tsx` | CB 餘額橫條圖 |
| CB Warning Card | `web/src/components/cb/CBWarningCard.tsx` | CB 預警卡片 |

---

## 開發階段

### Sprint 1: 基礎通訊層 (已完成)
> 注意：如果 Phase 1-3 尚未完成，請先執行對應的開發指令

確認項目：
- [x] .NET gRPC Client 可連接 Python Server
- [x] FinMind API 資料抓取正常
- [x] 資料庫 Schema 已建立

### Sprint 2: CB Engine 開發

#### Task 2.1: 擴充資料庫 Schema

執行 Migration SQL：

```sql
-- Migration: 002_add_cb_tracking_tables.sql

-- CB 發行資訊表 (CBIssuance)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CBIssuance')
BEGIN
    CREATE TABLE CBIssuance (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        CBTicker NVARCHAR(10) NOT NULL,              -- CB 代號 (e.g., 23301)
        UnderlyingTicker NVARCHAR(10) NOT NULL,      -- 標的股票代號 (e.g., 2330)
        CBName NVARCHAR(100),                        -- CB 名稱
        IssueDate DATE NOT NULL,                     -- 發行日
        MaturityDate DATE NOT NULL,                  -- 到期日
        InitialConversionPrice DECIMAL(18, 2),       -- 初始轉換價
        CurrentConversionPrice DECIMAL(18, 2),       -- 現行轉換價
        TotalIssueAmount DECIMAL(18, 2),             -- 發行總額 (億)
        OutstandingAmount DECIMAL(18, 2),            -- 流通餘額 (億)
        RedemptionTriggerPct DECIMAL(5, 2) DEFAULT 130.00,  -- 贖回觸發門檻 (%)
        RedemptionTriggerDays INT DEFAULT 30,        -- 連續觸發天數門檻
        IsActive BIT DEFAULT 1,                      -- 是否流通中
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_CBIssuance_Ticker UNIQUE (CBTicker)
    );

    CREATE NONCLUSTERED INDEX IX_CBIssuance_Underlying
    ON CBIssuance(UnderlyingTicker) WHERE IsActive = 1;
END
GO

-- CB 每日追蹤表 (CBDailyTracking)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CBDailyTracking')
BEGIN
    CREATE TABLE CBDailyTracking (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        CBTicker NVARCHAR(10) NOT NULL,              -- CB 代號
        TradeDate DATE NOT NULL,                     -- 交易日期
        UnderlyingClosePrice DECIMAL(18, 2),         -- 標的收盤價
        ConversionPrice DECIMAL(18, 2),              -- 轉換價
        PriceToConversionRatio DECIMAL(8, 4),        -- 股價/轉換價 比率
        IsAboveTrigger BIT,                          -- 是否超過觸發門檻
        ConsecutiveDaysAbove INT DEFAULT 0,          -- 連續超過天數
        OutstandingBalance DECIMAL(18, 4),           -- 剩餘餘額 (億)
        BalanceChangePercent DECIMAL(8, 4),          -- 餘額變化率 (%)
        WarningLevel NVARCHAR(20),                   -- SAFE/CAUTION/WARNING/CRITICAL
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_CBDailyTracking_Ticker_Date UNIQUE (CBTicker, TradeDate)
    );

    -- 時序查詢索引
    CREATE CLUSTERED INDEX IX_CBDailyTracking_Date
    ON CBDailyTracking(TradeDate DESC);

    -- 預警查詢索引
    CREATE NONCLUSTERED INDEX IX_CBDailyTracking_Warning
    ON CBDailyTracking(TradeDate, WarningLevel)
    INCLUDE (CBTicker, ConsecutiveDaysAbove, OutstandingBalance);
END
GO

-- CB 預警訊號表 (CBWarningSignals)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CBWarningSignals')
BEGIN
    CREATE TABLE CBWarningSignals (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        CBTicker NVARCHAR(10) NOT NULL,
        UnderlyingTicker NVARCHAR(10) NOT NULL,
        SignalDate DATE NOT NULL,
        DaysAboveTrigger INT NOT NULL,               -- 累計觸發天數
        DaysRemaining INT,                           -- 距離強贖剩餘天數
        TriggerProgress DECIMAL(5, 2),               -- 觸發進度 (%)
        OutstandingBalance DECIMAL(18, 4),           -- 剩餘餘額
        WarningLevel NVARCHAR(20) NOT NULL,          -- SAFE/CAUTION/WARNING/CRITICAL
        Comment NVARCHAR(500),                       -- 風險提示
        NotificationSent BIT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_CBWarningSignals_Date UNIQUE (CBTicker, SignalDate)
    );

    CREATE NONCLUSTERED INDEX IX_CBWarningSignals_Ranking
    ON CBWarningSignals(SignalDate, DaysAboveTrigger DESC);
END
GO

PRINT 'CB Tracking tables created successfully.';
```

#### Task 2.2: 開發櫃買中心 CB 餘額爬蟲

建立 `python/scrapers/cb_tpex_scraper.py`：

```python
"""
櫃買中心 CB 餘額爬蟲

資料來源: https://www.tpex.org.tw/web/bond/publish/convertible_bond/cb_download.php

抓取項目:
- CB 代號/名稱
- 標的股票代號
- 剩餘餘額
- 轉換價格
"""

import asyncio
import logging
from dataclasses import dataclass
from datetime import datetime
from typing import Optional

from playwright.async_api import Page
import polars as pl

from .base_scraper import BaseScraper, ScrapeResult, ScraperConfig

logger = logging.getLogger(__name__)


@dataclass
class CBBalanceData:
    """CB 餘額資料結構"""
    cb_ticker: str                      # CB 代號
    cb_name: str                        # CB 名稱
    underlying_ticker: str              # 標的代號
    outstanding_balance: float          # 剩餘餘額 (億)
    conversion_price: float             # 轉換價格
    maturity_date: str                  # 到期日
    trade_date: str                     # 資料日期


class CBTpexScraper(BaseScraper[list[CBBalanceData]]):
    """
    櫃買中心可轉債資料爬蟲

    爬取流程:
    1. 前往櫃買中心 CB 資訊頁面
    2. 選擇「可轉換公司債」類別
    3. 解析表格數據
    4. 轉換為結構化資料
    """

    BASE_URL = "https://www.tpex.org.tw/web/bond/publish/convertible_bond/cb_download.php"

    def __init__(self, config: Optional[ScraperConfig] = None):
        super().__init__(config or ScraperConfig(timeout=60000))

    async def scrape(self, page: Page, **kwargs) -> list[CBBalanceData]:
        """
        執行爬取邏輯

        Args:
            page: Playwright 頁面實例
            **kwargs: 可選參數
                - trade_date: 指定日期 (YYYY-MM-DD)

        Returns:
            CB 餘額資料列表
        """
        trade_date = kwargs.get('trade_date', datetime.now().strftime('%Y-%m-%d'))

        logger.info(f"Scraping CB balance data for {trade_date}")

        # 導航至目標頁面
        await page.goto(self.BASE_URL, wait_until='networkidle')

        # 等待表格載入
        await page.wait_for_selector('table.grid', timeout=self.config.timeout)

        # 解析表格數據
        rows = await page.query_selector_all('table.grid tbody tr')

        results: list[CBBalanceData] = []

        for row in rows:
            try:
                cells = await row.query_selector_all('td')
                if len(cells) < 8:
                    continue

                cb_ticker = await cells[0].inner_text()
                cb_name = await cells[1].inner_text()

                # 解析標的代號 (從 CB 代號或名稱推斷)
                underlying_ticker = self._extract_underlying_ticker(cb_ticker, cb_name)

                # 解析餘額 (單位: 億)
                balance_text = await cells[4].inner_text()
                outstanding_balance = self._parse_balance(balance_text)

                # 解析轉換價
                conversion_text = await cells[5].inner_text()
                conversion_price = self._parse_price(conversion_text)

                # 解析到期日
                maturity_text = await cells[3].inner_text()
                maturity_date = self._parse_date(maturity_text)

                results.append(CBBalanceData(
                    cb_ticker=cb_ticker.strip(),
                    cb_name=cb_name.strip(),
                    underlying_ticker=underlying_ticker,
                    outstanding_balance=outstanding_balance,
                    conversion_price=conversion_price,
                    maturity_date=maturity_date,
                    trade_date=trade_date
                ))

            except Exception as e:
                logger.warning(f"Failed to parse row: {e}")
                continue

        logger.info(f"Successfully scraped {len(results)} CB records")
        return results

    def _extract_underlying_ticker(self, cb_ticker: str, cb_name: str) -> str:
        """從 CB 代號或名稱推斷標的股票代號"""
        # CB 代號通常是 標的代號 + 序號，如 23301, 23302
        # 標的代號為前 4 碼
        if len(cb_ticker) >= 4 and cb_ticker[:4].isdigit():
            return cb_ticker[:4]
        return ""

    def _parse_balance(self, text: str) -> float:
        """解析餘額文字為數值 (億)"""
        try:
            cleaned = text.replace(',', '').replace(' ', '').strip()
            return float(cleaned) / 10000  # 轉換為億
        except ValueError:
            return 0.0

    def _parse_price(self, text: str) -> float:
        """解析價格文字"""
        try:
            cleaned = text.replace(',', '').replace(' ', '').strip()
            return float(cleaned)
        except ValueError:
            return 0.0

    def _parse_date(self, text: str) -> str:
        """解析日期文字"""
        try:
            # 處理民國年格式 (113/12/31 -> 2024-12-31)
            if '/' in text:
                parts = text.strip().split('/')
                if len(parts) == 3:
                    year = int(parts[0]) + 1911
                    return f"{year}-{parts[1].zfill(2)}-{parts[2].zfill(2)}"
            return text
        except ValueError:
            return ""


async def scrape_cb_balances(trade_date: Optional[str] = None) -> ScrapeResult[list[CBBalanceData]]:
    """
    便捷函數：執行 CB 餘額爬取

    Args:
        trade_date: 指定日期 (預設: 今日)

    Returns:
        ScrapeResult 包含 CB 餘額資料
    """
    async with CBTpexScraper() as scraper:
        return await scraper.execute(trade_date=trade_date)


def cb_data_to_polars(data: list[CBBalanceData]) -> pl.DataFrame:
    """將 CB 資料轉換為 Polars DataFrame"""
    return pl.DataFrame([
        {
            'cb_ticker': d.cb_ticker,
            'cb_name': d.cb_name,
            'underlying_ticker': d.underlying_ticker,
            'outstanding_balance': d.outstanding_balance,
            'conversion_price': d.conversion_price,
            'maturity_date': d.maturity_date,
            'trade_date': d.trade_date
        }
        for d in data
    ])
```

#### Task 2.3: 開發 DaysAboveTrigger 計算引擎

建立 `python/engine/cb_calculator.py`：

```python
"""
CB 預警計算引擎

核心計算:
1. Days Above Trigger (DAT): 股價連續超過轉換價 * 觸發門檻的天數
2. Warning Level: 根據 DAT 進度判定預警等級
3. CB Score: 整合至 Squeeze Score 系統的額外因子
"""

import polars as pl
from dataclasses import dataclass
from enum import Enum
from typing import Optional
import logging

logger = logging.getLogger(__name__)


class WarningLevel(Enum):
    """CB 預警等級"""
    SAFE = "SAFE"           # DAT < 10 天 (0-33%)
    CAUTION = "CAUTION"     # DAT 10-19 天 (33-66%)
    WARNING = "WARNING"     # DAT 20-29 天 (66-99%)
    CRITICAL = "CRITICAL"   # DAT >= 30 天 (已觸發)


@dataclass
class CBWarningConfig:
    """CB 預警配置"""
    trigger_threshold_pct: float = 130.0    # 觸發門檻 (%)
    trigger_days_required: int = 30         # 需要連續天數
    reset_on_below: bool = True             # 跌破門檻是否重置計數


@dataclass
class CBWarningResult:
    """CB 預警計算結果"""
    cb_ticker: str
    underlying_ticker: str
    trade_date: str
    current_price: float
    conversion_price: float
    price_ratio: float                      # 股價 / 轉換價
    is_above_trigger: bool
    consecutive_days: int                   # 連續超過天數
    days_remaining: int                     # 距離觸發剩餘天數
    trigger_progress: float                 # 觸發進度 (0-100%)
    outstanding_balance: float              # 剩餘餘額
    warning_level: WarningLevel
    comment: str


class CBWarningCalculator:
    """
    CB 預警計算器

    計算邏輯:
    1. 每日檢查標的股價是否超過 轉換價 * 130%
    2. 累計連續超過的天數 (DAT)
    3. 根據 DAT / 30 計算觸發進度
    4. 判定預警等級
    """

    def __init__(self, config: Optional[CBWarningConfig] = None):
        self.config = config or CBWarningConfig()

    def calculate_warning(
        self,
        cb_ticker: str,
        underlying_ticker: str,
        current_price: float,
        conversion_price: float,
        previous_consecutive_days: int,
        outstanding_balance: float,
        trade_date: str
    ) -> CBWarningResult:
        """
        計算單一 CB 的預警狀態

        Args:
            cb_ticker: CB 代號
            underlying_ticker: 標的股票代號
            current_price: 標的當日收盤價
            conversion_price: 轉換價格
            previous_consecutive_days: 前一日累計連續天數
            outstanding_balance: 剩餘餘額 (億)
            trade_date: 交易日期

        Returns:
            CBWarningResult
        """
        # 計算價格比率
        if conversion_price <= 0:
            price_ratio = 0.0
        else:
            price_ratio = (current_price / conversion_price) * 100

        # 判斷是否超過觸發門檻
        trigger_price = conversion_price * (self.config.trigger_threshold_pct / 100)
        is_above_trigger = current_price >= trigger_price

        # 計算連續天數
        if is_above_trigger:
            consecutive_days = previous_consecutive_days + 1
        else:
            consecutive_days = 0 if self.config.reset_on_below else previous_consecutive_days

        # 計算觸發進度與剩餘天數
        trigger_progress = min(100.0, (consecutive_days / self.config.trigger_days_required) * 100)
        days_remaining = max(0, self.config.trigger_days_required - consecutive_days)

        # 判定預警等級
        warning_level = self._determine_warning_level(consecutive_days)

        # 生成預警說明
        comment = self._generate_comment(
            consecutive_days=consecutive_days,
            days_remaining=days_remaining,
            price_ratio=price_ratio,
            outstanding_balance=outstanding_balance,
            warning_level=warning_level
        )

        return CBWarningResult(
            cb_ticker=cb_ticker,
            underlying_ticker=underlying_ticker,
            trade_date=trade_date,
            current_price=current_price,
            conversion_price=conversion_price,
            price_ratio=round(price_ratio, 2),
            is_above_trigger=is_above_trigger,
            consecutive_days=consecutive_days,
            days_remaining=days_remaining,
            trigger_progress=round(trigger_progress, 2),
            outstanding_balance=outstanding_balance,
            warning_level=warning_level,
            comment=comment
        )

    def _determine_warning_level(self, consecutive_days: int) -> WarningLevel:
        """根據連續天數判定預警等級"""
        threshold_33 = self.config.trigger_days_required * 0.33  # ~10 天
        threshold_66 = self.config.trigger_days_required * 0.66  # ~20 天

        if consecutive_days >= self.config.trigger_days_required:
            return WarningLevel.CRITICAL
        elif consecutive_days >= threshold_66:
            return WarningLevel.WARNING
        elif consecutive_days >= threshold_33:
            return WarningLevel.CAUTION
        else:
            return WarningLevel.SAFE

    def _generate_comment(
        self,
        consecutive_days: int,
        days_remaining: int,
        price_ratio: float,
        outstanding_balance: float,
        warning_level: WarningLevel
    ) -> str:
        """生成預警說明文字"""
        if warning_level == WarningLevel.CRITICAL:
            return f"已達強贖門檻！連續 {consecutive_days} 日超過轉換價 130%，剩餘 {outstanding_balance:.2f} 億可能面臨轉換壓力"
        elif warning_level == WarningLevel.WARNING:
            return f"高度警戒：已連續 {consecutive_days} 日，僅剩 {days_remaining} 日即觸發強贖，餘額 {outstanding_balance:.2f} 億"
        elif warning_level == WarningLevel.CAUTION:
            return f"注意追蹤：連續 {consecutive_days} 日超標，股價/轉換價 = {price_ratio:.1f}%"
        else:
            return f"安全範圍：股價/轉換價 = {price_ratio:.1f}%，無近期強贖風險"


def batch_calculate_cb_warnings(
    cb_issuance_df: pl.DataFrame,
    stock_prices_df: pl.DataFrame,
    previous_tracking_df: pl.DataFrame,
    trade_date: str,
    config: Optional[CBWarningConfig] = None
) -> pl.DataFrame:
    """
    批量計算所有 CB 的預警狀態

    使用 Polars 進行高效能批量計算

    Args:
        cb_issuance_df: CB 發行資訊 (cb_ticker, underlying_ticker, conversion_price, outstanding_balance)
        stock_prices_df: 股價資料 (ticker, trade_date, close_price)
        previous_tracking_df: 前一日追蹤資料 (cb_ticker, consecutive_days)
        trade_date: 計算日期
        config: 計算配置

    Returns:
        Polars DataFrame 包含所有 CB 預警結果
    """
    calculator = CBWarningCalculator(config)

    # Join CB 資訊與股價
    combined = (
        cb_issuance_df
        .join(
            stock_prices_df.filter(pl.col('trade_date') == trade_date),
            left_on='underlying_ticker',
            right_on='ticker',
            how='left'
        )
        .join(
            previous_tracking_df.select(['cb_ticker', 'consecutive_days_above']),
            on='cb_ticker',
            how='left'
        )
        .with_columns([
            pl.col('consecutive_days_above').fill_null(0).alias('prev_consecutive')
        ])
    )

    # 計算各項指標
    results = []
    for row in combined.iter_rows(named=True):
        result = calculator.calculate_warning(
            cb_ticker=row['cb_ticker'],
            underlying_ticker=row['underlying_ticker'],
            current_price=row.get('close_price', 0) or 0,
            conversion_price=row.get('current_conversion_price', 0) or 0,
            previous_consecutive_days=row.get('prev_consecutive', 0) or 0,
            outstanding_balance=row.get('outstanding_amount', 0) or 0,
            trade_date=trade_date
        )
        results.append({
            'cb_ticker': result.cb_ticker,
            'underlying_ticker': result.underlying_ticker,
            'trade_date': result.trade_date,
            'current_price': result.current_price,
            'conversion_price': result.conversion_price,
            'price_ratio': result.price_ratio,
            'is_above_trigger': result.is_above_trigger,
            'consecutive_days': result.consecutive_days,
            'days_remaining': result.days_remaining,
            'trigger_progress': result.trigger_progress,
            'outstanding_balance': result.outstanding_balance,
            'warning_level': result.warning_level.value,
            'comment': result.comment
        })

    return pl.DataFrame(results)
```

#### Task 2.4: 擴充 gRPC Proto 定義

修改 `proto/squeeze.proto`，新增 CB 相關服務：

```protobuf
// 在現有 proto 底部新增

// CB Warning Light Service
service CBWarningEngine {
  // 取得單一 CB 預警狀態
  rpc GetCBWarning (CBWarningRequest) returns (CBWarningResponse);

  // 取得所有 CB 預警清單
  rpc GetAllCBWarnings (AllCBWarningsRequest) returns (AllCBWarningsResponse);

  // 取得高風險 CB 清單
  rpc GetCriticalCBs (CriticalCBsRequest) returns (CriticalCBsResponse);
}

message CBWarningRequest {
  string cb_ticker = 1;
  string trade_date = 2;
}

message CBWarningResponse {
  string cb_ticker = 1;
  string underlying_ticker = 2;
  string trade_date = 3;
  double current_price = 4;
  double conversion_price = 5;
  double price_ratio = 6;
  bool is_above_trigger = 7;
  int32 consecutive_days = 8;
  int32 days_remaining = 9;
  double trigger_progress = 10;
  double outstanding_balance = 11;
  string warning_level = 12;  // SAFE/CAUTION/WARNING/CRITICAL
  string comment = 13;
}

message AllCBWarningsRequest {
  string trade_date = 1;
  string min_warning_level = 2;  // 最低預警等級篩選
}

message AllCBWarningsResponse {
  repeated CBWarningResponse warnings = 1;
  string analysis_date = 2;
  int32 total_count = 3;
  int32 critical_count = 4;
  int32 warning_count = 5;
}

message CriticalCBsRequest {
  string trade_date = 1;
  int32 limit = 2;
  int32 min_days_above = 3;  // 最低連續天數
}

message CriticalCBsResponse {
  repeated CBWarningResponse critical_cbs = 1;
  string analysis_date = 2;
}
```

#### Task 2.5: .NET API 實作

建立 `src/AlphaSqueeze.Api/Controllers/CBController.cs`：

```csharp
using Microsoft.AspNetCore.Mvc;
using AlphaSqueeze.Core.Services;
using AlphaSqueeze.Shared.DTOs;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// CB 可轉債預警燈 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CBController : ControllerBase
{
    private readonly ICBWarningService _cbWarningService;
    private readonly ILogger<CBController> _logger;

    public CBController(
        ICBWarningService cbWarningService,
        ILogger<CBController> logger)
    {
        _cbWarningService = cbWarningService;
        _logger = logger;
    }

    /// <summary>
    /// 取得所有 CB 預警清單
    /// </summary>
    /// <param name="date">查詢日期 (預設: 今日)</param>
    /// <param name="minLevel">最低預警等級 (SAFE/CAUTION/WARNING/CRITICAL)</param>
    [HttpGet("warnings")]
    [ProducesResponseType(typeof(CBWarningListResponse), 200)]
    public async Task<IActionResult> GetAllWarnings(
        [FromQuery] string? date = null,
        [FromQuery] string minLevel = "CAUTION")
    {
        var tradeDate = string.IsNullOrEmpty(date)
            ? DateTime.Today
            : DateTime.Parse(date);

        var result = await _cbWarningService.GetAllWarningsAsync(tradeDate, minLevel);
        return Ok(result);
    }

    /// <summary>
    /// 取得單一 CB 預警狀態
    /// </summary>
    /// <param name="cbTicker">CB 代號</param>
    [HttpGet("{cbTicker}")]
    [ProducesResponseType(typeof(CBWarningDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetWarning(string cbTicker)
    {
        var result = await _cbWarningService.GetWarningAsync(cbTicker);

        if (result == null)
            return NotFound(new { message = $"CB {cbTicker} not found" });

        return Ok(result);
    }

    /// <summary>
    /// 取得高風險 CB 排行
    /// </summary>
    /// <param name="limit">返回數量</param>
    /// <param name="minDays">最低連續天數</param>
    [HttpGet("critical")]
    [ProducesResponseType(typeof(List<CBWarningDto>), 200)]
    public async Task<IActionResult> GetCriticalCBs(
        [FromQuery] int limit = 10,
        [FromQuery] int minDays = 15)
    {
        var result = await _cbWarningService.GetCriticalCBsAsync(limit, minDays);
        return Ok(result);
    }

    /// <summary>
    /// 取得特定標的的所有 CB 狀態
    /// </summary>
    /// <param name="ticker">標的股票代號</param>
    [HttpGet("by-underlying/{ticker}")]
    [ProducesResponseType(typeof(List<CBWarningDto>), 200)]
    public async Task<IActionResult> GetByUnderlying(string ticker)
    {
        var result = await _cbWarningService.GetByUnderlyingAsync(ticker);
        return Ok(result);
    }
}
```

#### Task 2.6: React 前端 CB 餘額橫條圖

建立 `web/src/components/charts/CBBalanceBar.tsx`：

```tsx
import { useMemo } from 'react';
import { clsx } from 'clsx';

interface CBBalanceBarProps {
  cbTicker: string;
  underlyingTicker: string;
  outstandingBalance: number;    // 剩餘餘額 (億)
  initialBalance: number;        // 初始餘額 (億)
  daysAboveTrigger: number;      // 連續觸發天數
  triggerDaysRequired: number;   // 觸發所需天數 (預設 30)
  warningLevel: 'SAFE' | 'CAUTION' | 'WARNING' | 'CRITICAL';
}

export function CBBalanceBar({
  cbTicker,
  underlyingTicker,
  outstandingBalance,
  initialBalance,
  daysAboveTrigger,
  triggerDaysRequired = 30,
  warningLevel
}: CBBalanceBarProps) {
  // 計算餘額百分比
  const balancePercent = useMemo(() => {
    if (initialBalance <= 0) return 0;
    return Math.min(100, (outstandingBalance / initialBalance) * 100);
  }, [outstandingBalance, initialBalance]);

  // 計算觸發進度百分比
  const triggerPercent = useMemo(() => {
    return Math.min(100, (daysAboveTrigger / triggerDaysRequired) * 100);
  }, [daysAboveTrigger, triggerDaysRequired]);

  // 預警等級樣式
  const warningStyles = {
    SAFE: {
      bar: 'bg-green-500',
      text: 'text-green-600',
      border: 'border-green-500',
    },
    CAUTION: {
      bar: 'bg-yellow-500',
      text: 'text-yellow-600',
      border: 'border-yellow-500',
    },
    WARNING: {
      bar: 'bg-orange-500',
      text: 'text-orange-600',
      border: 'border-orange-500',
    },
    CRITICAL: {
      bar: 'bg-red-500',
      text: 'text-red-600',
      border: 'border-red-500',
    },
  };

  const styles = warningStyles[warningLevel];

  return (
    <div className={clsx(
      'p-4 rounded-lg border-l-4 bg-white shadow-sm',
      styles.border
    )}>
      {/* Header */}
      <div className="flex justify-between items-start mb-3">
        <div>
          <h4 className="font-semibold text-gray-900">{cbTicker}</h4>
          <span className="text-sm text-gray-500">標的: {underlyingTicker}</span>
        </div>
        <span className={clsx('px-2 py-1 rounded text-xs font-medium', styles.text, `bg-${warningLevel.toLowerCase()}-100`)}>
          {warningLevel === 'CRITICAL' ? '已觸發' :
           warningLevel === 'WARNING' ? '高度警戒' :
           warningLevel === 'CAUTION' ? '注意追蹤' : '安全'}
        </span>
      </div>

      {/* 餘額橫條圖 */}
      <div className="mb-3">
        <div className="flex justify-between text-sm mb-1">
          <span className="text-gray-600">剩餘餘額</span>
          <span className="font-medium">{outstandingBalance.toFixed(2)} 億 / {initialBalance.toFixed(2)} 億</span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-3">
          <div
            className={clsx('h-3 rounded-full transition-all duration-500', styles.bar)}
            style={{ width: `${balancePercent}%` }}
          />
        </div>
        <div className="text-right text-xs text-gray-500 mt-1">
          {balancePercent.toFixed(1)}% 尚未轉換
        </div>
      </div>

      {/* 觸發進度條 */}
      <div>
        <div className="flex justify-between text-sm mb-1">
          <span className="text-gray-600">強贖觸發進度</span>
          <span className="font-medium">
            {daysAboveTrigger} / {triggerDaysRequired} 天
          </span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-2">
          <div
            className={clsx(
              'h-2 rounded-full transition-all duration-500',
              triggerPercent >= 100 ? 'bg-red-600 animate-pulse' :
              triggerPercent >= 66 ? 'bg-orange-500' :
              triggerPercent >= 33 ? 'bg-yellow-500' : 'bg-green-500'
            )}
            style={{ width: `${triggerPercent}%` }}
          />
        </div>
        {warningLevel === 'CRITICAL' && (
          <div className="mt-2 text-xs text-red-600 font-medium animate-pulse">
            已達強制贖回門檻
          </div>
        )}
      </div>
    </div>
  );
}
```

建立 `web/src/components/cb/CBWarningCard.tsx`：

```tsx
import { CBBalanceBar } from '../charts/CBBalanceBar';
import type { CBWarningDto } from '../../types';

interface CBWarningCardProps {
  warning: CBWarningDto;
  onClick?: () => void;
}

export function CBWarningCard({ warning, onClick }: CBWarningCardProps) {
  return (
    <div
      className="cursor-pointer hover:shadow-md transition-shadow"
      onClick={onClick}
    >
      <CBBalanceBar
        cbTicker={warning.cbTicker}
        underlyingTicker={warning.underlyingTicker}
        outstandingBalance={warning.outstandingBalance}
        initialBalance={warning.initialBalance || warning.outstandingBalance * 1.5}
        daysAboveTrigger={warning.consecutiveDays}
        triggerDaysRequired={30}
        warningLevel={warning.warningLevel}
      />
      <div className="px-4 pb-4 bg-white rounded-b-lg border-t border-gray-100">
        <p className="text-sm text-gray-600 mt-2">{warning.comment}</p>
        <div className="flex justify-between items-center mt-2 text-xs text-gray-500">
          <span>轉換價: ${warning.conversionPrice.toFixed(2)}</span>
          <span>股價比: {warning.priceRatio.toFixed(1)}%</span>
        </div>
      </div>
    </div>
  );
}
```

#### Task 2.7: 新增 TypeScript 型別

擴充 `web/src/types/index.ts`：

```typescript
// 在現有型別後面新增

// ========== CB 預警燈相關型別 ==========

export type CBWarningLevel = 'SAFE' | 'CAUTION' | 'WARNING' | 'CRITICAL';

export interface CBWarningDto {
  cbTicker: string;
  underlyingTicker: string;
  tradeDate: string;
  currentPrice: number;
  conversionPrice: number;
  priceRatio: number;
  isAboveTrigger: boolean;
  consecutiveDays: number;
  daysRemaining: number;
  triggerProgress: number;
  outstandingBalance: number;
  initialBalance?: number;
  warningLevel: CBWarningLevel;
  comment: string;
}

export interface CBWarningListResponse {
  warnings: CBWarningDto[];
  analysisDate: string;
  totalCount: number;
  criticalCount: number;
  warningCount: number;
}

export interface CBIssuanceDto {
  cbTicker: string;
  underlyingTicker: string;
  cbName: string;
  issueDate: string;
  maturityDate: string;
  currentConversionPrice: number;
  totalIssueAmount: number;
  outstandingAmount: number;
  redemptionTriggerPct: number;
  redemptionTriggerDays: number;
}
```

#### Task 2.8: 新增 API 服務

擴充 `web/src/services/api.ts`：

```typescript
// 在現有 API 後面新增

export const cbApi = {
  /**
   * 取得所有 CB 預警清單
   */
  getWarnings: async (date?: string, minLevel = 'CAUTION'): Promise<CBWarningListResponse> => {
    const { data } = await api.get('/cb/warnings', {
      params: { date, minLevel },
    });
    return data;
  },

  /**
   * 取得單一 CB 預警狀態
   */
  getWarning: async (cbTicker: string): Promise<CBWarningDto> => {
    const { data } = await api.get(`/cb/${cbTicker}`);
    return data;
  },

  /**
   * 取得高風險 CB 排行
   */
  getCriticalCBs: async (limit = 10, minDays = 15): Promise<CBWarningDto[]> => {
    const { data } = await api.get('/cb/critical', {
      params: { limit, minDays },
    });
    return data;
  },

  /**
   * 依標的股票取得相關 CB
   */
  getByUnderlying: async (ticker: string): Promise<CBWarningDto[]> => {
    const { data } = await api.get(`/cb/by-underlying/${ticker}`);
    return data;
  },
};
```

#### Task 2.9: 新增 React Query Hooks

建立 `web/src/hooks/useCBWarnings.ts`：

```typescript
import { useQuery } from '@tanstack/react-query';
import { cbApi } from '../services/api';

export function useCBWarnings(date?: string, minLevel = 'CAUTION') {
  return useQuery({
    queryKey: ['cbWarnings', date, minLevel],
    queryFn: () => cbApi.getWarnings(date, minLevel),
    refetchInterval: 60000,
    staleTime: 30000,
  });
}

export function useCBWarning(cbTicker: string) {
  return useQuery({
    queryKey: ['cbWarning', cbTicker],
    queryFn: () => cbApi.getWarning(cbTicker),
    enabled: !!cbTicker,
  });
}

export function useCriticalCBs(limit = 10, minDays = 15) {
  return useQuery({
    queryKey: ['criticalCBs', limit, minDays],
    queryFn: () => cbApi.getCriticalCBs(limit, minDays),
    refetchInterval: 60000,
  });
}

export function useCBsByUnderlying(ticker: string) {
  return useQuery({
    queryKey: ['cbsByUnderlying', ticker],
    queryFn: () => cbApi.getByUnderlying(ticker),
    enabled: !!ticker,
  });
}
```

---

## 驗收標準

### 功能驗收
- [ ] Python 爬蟲可成功抓取櫃買中心 CB 餘額資料
- [ ] DaysAboveTrigger 計算結果正確
- [ ] gRPC 服務可正常回應 CB 預警請求
- [ ] .NET API Endpoint 可正確返回 CB 資料
- [ ] React Dashboard 正確顯示 CB 餘額橫條圖
- [ ] 預警等級判定與顏色顯示正確

### 效能驗收
- [ ] 全量 CB 計算 < 2 秒 (約 100 檔)
- [ ] API 回應時間 < 200ms
- [ ] 爬蟲單次執行 < 30 秒

### 測試驗收
- [ ] Python 單元測試覆蓋率 > 80%
- [ ] .NET 單元測試通過
- [ ] React 元件測試通過
- [ ] 整合測試通過

---

## 執行指令

### 完整開發指令

```bash
/ralph-loop:ralph-loop --max-iterations 50 請根據 .claude/commands/feature-cb-warning-light.md 執行 CB 預警燈功能開發
```

### 分段開發指令

```bash
# Step 1: 資料庫 Schema
/ralph-loop:ralph-loop --max-iterations 10 請執行 .claude/commands/feature-cb-warning-light.md 的 Task 2.1 資料庫擴充

# Step 2: Python 爬蟲
/ralph-loop:ralph-loop --max-iterations 15 請執行 .claude/commands/feature-cb-warning-light.md 的 Task 2.2 CB 爬蟲開發

# Step 3: Python 計算引擎
/ralph-loop:ralph-loop --max-iterations 15 請執行 .claude/commands/feature-cb-warning-light.md 的 Task 2.3 CB 計算引擎開發

# Step 4: .NET API
/ralph-loop:ralph-loop --max-iterations 20 請執行 .claude/commands/feature-cb-warning-light.md 的 Task 2.4-2.5 gRPC 與 API 開發

# Step 5: React 前端
/ralph-loop:ralph-loop --max-iterations 20 請執行 .claude/commands/feature-cb-warning-light.md 的 Task 2.6-2.9 前端開發
```

---

---

## 演算法升級：Squeeze Score 整合 CB 因子

### 調整後的權重公式

將 CB 因子納入 Squeeze Score 計算模型：

```
S = (W_B × F_B) + (W_G × F_G) + (W_M × F_M) + (W_CB × F_CB)
```

| 維度 | 參數 | 新權重 | 原權重 | 判定邏輯 |
|------|------|--------|--------|----------|
| 法人空頭 (F_B) | 借券賣出餘額變化 | 30% | 35% | 負值（回補）越多，得分越高 |
| Gamma 效應 (F_G) | IV - HV 乖離 | 25% | 25% | 若 IV < HV，得分越高 |
| 散戶燃料 (F_M) | 券資比 | 15% | 20% | 數值越高（空單擁擠），得分越高 |
| **CB 軋空 (F_CB)** | CB 觸發進度 | **15%** | N/A | 剩餘餘額佔比 + 達標天數 |
| 價量動能 (F_V) | 價格與量能組合 | 15% | 20% | 帶量突破壓力位，得分越高 |

### CB 因子計算邏輯 (F_CB)

```python
def calculate_cb_score(
    premium_rate: float,          # 轉換溢價率
    remaining_ratio: float,       # 剩餘餘額佔總發行比例
    days_above_trigger: int,      # 連續達標天數
    redemption_called: bool       # 是否已公告強贖
) -> float:
    """
    CB 軋空因子計算

    評分邏輯:
    1. 溢價率 < 0 (折價) = 轉換誘因極大 = 高分
    2. 剩餘餘額佔比 > 50% + 達標天數 > 15 = 極高軋空潛力
    3. 已公告強贖 = 滿分
    """
    if redemption_called:
        return 100  # 已觸發強贖，最高分

    score = 0

    # 溢價率評分 (0-40分)
    if premium_rate < 0:
        # 折價情況，轉換誘因高
        score += min(40, abs(premium_rate) * 200)
    elif premium_rate < 10:
        score += 30 - premium_rate * 2
    else:
        score += max(0, 20 - premium_rate)

    # 剩餘餘額評分 (0-30分)
    if remaining_ratio > 0.7:
        score += 30
    elif remaining_ratio > 0.5:
        score += 25
    elif remaining_ratio > 0.3:
        score += 15
    else:
        score += 5

    # 達標天數評分 (0-30分)
    if days_above_trigger >= 25:
        score += 30  # 即將觸發
    elif days_above_trigger >= 15:
        score += 25  # 高度警戒
    elif days_above_trigger >= 10:
        score += 15  # 注意追蹤
    elif days_above_trigger >= 5:
        score += 8
    else:
        score += 0

    return min(100, score)
```

---

## 監控頻次設計

### 盤後處理 (每日 18:30)

由 Python Worker 執行：
1. 抓取櫃買中心最新 CB 餘額數據
2. 更新 `DaysAboveTrigger` 累計數值
3. 計算 CB 預警等級
4. 觸發 LINE Notify 推播高風險標的

### 盤中預警 (每 5 分鐘)

由 .NET Background Service 執行：
1. 針對已在「警告區」(DaysAboveTrigger >= 15) 的標的
2. 比對即時股價是否觸及 `1.3 × ConversionPrice`
3. 若觸及門檻立即發送即時通知

---

## 版本資訊

- **文件版本**: 1.0.0
- **建立日期**: 2026-01-19
- **作者**: 資深微軟系統架構師
- **適用版本**: Alpha Squeeze V1.2

---

## 附錄

### A. CB 強制贖回機制說明

當可轉換公司債 (CB) 的標的股票價格連續 30 個交易日收盤價超過當時轉換價格的 130% 時，發行公司有權提前以面額加計利息贖回所有流通在外的 CB。

此機制對市場的影響：
1. **CB 持有人**: 必須在贖回前轉換成股票或賣出
2. **標的股票**: 可能面臨轉換後的籌碼壓力
3. **軋空機會**: 若空單擁擠，轉換壓力可能形成反向軋空

### B. 參考資料來源

- 櫃買中心可轉債資訊: https://www.tpex.org.tw/web/bond/
- 證交所可轉債專區: https://www.twse.com.tw/zh/products/securities/cb/
