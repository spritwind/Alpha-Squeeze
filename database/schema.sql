-- Alpha Squeeze Database Schema
-- Version: 1.0
-- Database: AlphaSqueeze (MSSQL)

-- =============================================
-- Create Database (run separately if needed)
-- =============================================
-- CREATE DATABASE AlphaSqueeze;
-- GO
-- USE AlphaSqueeze;
-- GO

-- =============================================
-- 股票日指標表 (DailyStockMetrics)
-- 存儲 FinMind 抓取的籌碼與價格數據
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailyStockMetrics')
BEGIN
    CREATE TABLE DailyStockMetrics (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        Ticker NVARCHAR(10) NOT NULL,                    -- 股票代號
        TradeDate DATE NOT NULL,                         -- 交易日期
        ClosePrice DECIMAL(18, 2),                       -- 收盤價
        OpenPrice DECIMAL(18, 2),                        -- 開盤價
        HighPrice DECIMAL(18, 2),                        -- 最高價
        LowPrice DECIMAL(18, 2),                         -- 最低價
        BorrowingBalance BIGINT,                         -- 借券賣出餘額
        BorrowingBalanceChange INT,                      -- 借券賣出餘額增減 (核心指標)
        MarginBalance BIGINT,                            -- 融資餘額
        ShortBalance BIGINT,                             -- 融券餘額
        MarginRatio DECIMAL(18, 4),                      -- 券資比 (%)
        HistoricalVolatility20D DECIMAL(18, 6),          -- 20日歷史波動率 (HV)
        Volume BIGINT,                                   -- 成交量
        Turnover BIGINT,                                 -- 成交金額
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_DailyStockMetrics_Ticker_Date UNIQUE (Ticker, TradeDate)
    );

    -- Clustered index on TradeDate for time-series queries
    CREATE CLUSTERED INDEX IX_DailyStockMetrics_TradeDate
    ON DailyStockMetrics(TradeDate);

    -- Non-clustered index for ticker lookups
    CREATE NONCLUSTERED INDEX IX_DailyStockMetrics_Ticker
    ON DailyStockMetrics(Ticker) INCLUDE (ClosePrice, BorrowingBalanceChange, MarginRatio);

    -- Index for squeeze screening queries
    CREATE NONCLUSTERED INDEX IX_DailyStockMetrics_Squeeze
    ON DailyStockMetrics(TradeDate, MarginRatio DESC)
    INCLUDE (Ticker, BorrowingBalanceChange, HistoricalVolatility20D);
END
GO

-- =============================================
-- 權證實時數據表 (WarrantMarketData)
-- 存儲 Scraper 抓取的權證端數據
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WarrantMarketData')
BEGIN
    CREATE TABLE WarrantMarketData (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        UnderlyingTicker NVARCHAR(10) NOT NULL,          -- 標的代號
        WarrantTicker NVARCHAR(10) NOT NULL,             -- 權證代號
        WarrantName NVARCHAR(50),                        -- 權證名稱
        Issuer NVARCHAR(20),                             -- 發行商 (元大, 統一, etc.)
        WarrantType NVARCHAR(10),                        -- 權證類型 (Call/Put)
        ImpliedVolatility DECIMAL(18, 6),                -- 隱含波動率 (IV)
        EffectiveLeverage DECIMAL(18, 4),                -- 實質槓桿
        SpreadRatio DECIMAL(18, 6),                      -- 差槓比
        StrikePrice DECIMAL(18, 2),                      -- 履約價
        ExpiryDate DATE,                                 -- 到期日
        DaysToExpiry INT,                                -- 剩餘天數
        Delta DECIMAL(18, 6),                            -- Delta 值
        Gamma DECIMAL(18, 6),                            -- Gamma 值
        Theta DECIMAL(18, 6),                            -- Theta 值
        Vega DECIMAL(18, 6),                             -- Vega 值
        TradeDate DATE NOT NULL,                         -- 資料日期
        LastUpdate DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_WarrantMarketData_Warrant_Date UNIQUE (WarrantTicker, TradeDate)
    );

    -- Index for underlying ticker lookups
    CREATE NONCLUSTERED INDEX IX_WarrantMarketData_Underlying
    ON WarrantMarketData(UnderlyingTicker, TradeDate)
    INCLUDE (ImpliedVolatility, EffectiveLeverage);

    -- Index for IV analysis
    CREATE NONCLUSTERED INDEX IX_WarrantMarketData_IV
    ON WarrantMarketData(TradeDate, WarrantType)
    INCLUDE (UnderlyingTicker, ImpliedVolatility);
END
GO

-- =============================================
-- 軋空訊號表 (SqueezeSignals)
-- 存儲每日計算的軋空分數
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SqueezeSignals')
BEGIN
    CREATE TABLE SqueezeSignals (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        Ticker NVARCHAR(10) NOT NULL,                    -- 股票代號
        SignalDate DATE NOT NULL,                        -- 訊號日期
        SqueezeScore INT NOT NULL,                       -- 總分 (0-100)
        BorrowScore DECIMAL(18, 4),                      -- 法人空頭分數 (F_B)
        GammaScore DECIMAL(18, 4),                       -- Gamma效應分數 (F_G)
        MarginScore DECIMAL(18, 4),                      -- 散戶燃料分數 (F_M)
        MomentumScore DECIMAL(18, 4),                    -- 價量動能分數 (F_V)
        Trend NVARCHAR(20),                              -- BULLISH/NEUTRAL/BEARISH
        Comment NVARCHAR(500),                           -- 戰術建議
        NotificationSent BIT DEFAULT 0,                  -- 是否已發送通知
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_SqueezeSignals_Ticker_Date UNIQUE (Ticker, SignalDate)
    );

    -- Index for daily ranking queries
    CREATE NONCLUSTERED INDEX IX_SqueezeSignals_Ranking
    ON SqueezeSignals(SignalDate, SqueezeScore DESC)
    INCLUDE (Ticker, Trend);
END
GO

-- =============================================
-- CB 可轉債數據表 (CBMarketData) - 預留
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CBMarketData')
BEGIN
    CREATE TABLE CBMarketData (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        CBTicker NVARCHAR(10) NOT NULL,                  -- CB 代號
        UnderlyingTicker NVARCHAR(10) NOT NULL,          -- 標的代號
        CBName NVARCHAR(50),                             -- CB 名稱
        CBPrice DECIMAL(18, 4),                          -- CB 價格
        ConversionPrice DECIMAL(18, 2),                  -- 轉換價格
        ConversionPremium DECIMAL(18, 6),                -- 轉換溢價率 (%)
        YieldToMaturity DECIMAL(18, 6),                  -- 殖利率
        MaturityDate DATE,                               -- 到期日
        TradeDate DATE NOT NULL,                         -- 資料日期
        LastUpdate DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_CBMarketData_CB_Date UNIQUE (CBTicker, TradeDate)
    );

    CREATE NONCLUSTERED INDEX IX_CBMarketData_Underlying
    ON CBMarketData(UnderlyingTicker, TradeDate);
END
GO

-- =============================================
-- 系統日誌表 (SystemLogs)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemLogs')
BEGIN
    CREATE TABLE SystemLogs (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        LogLevel NVARCHAR(20) NOT NULL,                  -- INFO/WARNING/ERROR
        Source NVARCHAR(100),                            -- 來源模組
        Message NVARCHAR(MAX),                           -- 日誌訊息
        Exception NVARCHAR(MAX),                         -- 例外詳情
        Ticker NVARCHAR(10),                             -- 相關股票代號
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    CREATE NONCLUSTERED INDEX IX_SystemLogs_Date
    ON SystemLogs(CreatedAt DESC);

    CREATE NONCLUSTERED INDEX IX_SystemLogs_Level
    ON SystemLogs(LogLevel, CreatedAt DESC);
END
GO

-- =============================================
-- Stored Procedures
-- =============================================

-- 取得今日最高軋空潛力標的
CREATE OR ALTER PROCEDURE sp_GetTopSqueezeCandidates
    @SignalDate DATE,
    @Limit INT = 10,
    @MinScore INT = 60
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Limit)
        ss.Ticker,
        ss.SqueezeScore,
        ss.BorrowScore,
        ss.GammaScore,
        ss.MarginScore,
        ss.MomentumScore,
        ss.Trend,
        ss.Comment,
        dsm.ClosePrice,
        dsm.Volume,
        dsm.MarginRatio,
        dsm.BorrowingBalanceChange
    FROM SqueezeSignals ss
    INNER JOIN DailyStockMetrics dsm
        ON ss.Ticker = dsm.Ticker AND ss.SignalDate = dsm.TradeDate
    WHERE ss.SignalDate = @SignalDate
        AND ss.SqueezeScore >= @MinScore
    ORDER BY ss.SqueezeScore DESC;
END
GO

-- 批量插入股票日指標
CREATE OR ALTER PROCEDURE sp_BulkUpsertDailyMetrics
    @Metrics dbo.DailyStockMetricsType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE INTO DailyStockMetrics AS target
    USING @Metrics AS source
    ON target.Ticker = source.Ticker AND target.TradeDate = source.TradeDate
    WHEN MATCHED THEN
        UPDATE SET
            ClosePrice = source.ClosePrice,
            BorrowingBalanceChange = source.BorrowingBalanceChange,
            MarginRatio = source.MarginRatio,
            HistoricalVolatility20D = source.HistoricalVolatility20D,
            Volume = source.Volume,
            UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (Ticker, TradeDate, ClosePrice, BorrowingBalanceChange,
                MarginRatio, HistoricalVolatility20D, Volume)
        VALUES (source.Ticker, source.TradeDate, source.ClosePrice,
                source.BorrowingBalanceChange, source.MarginRatio,
                source.HistoricalVolatility20D, source.Volume);
END
GO

PRINT 'Alpha Squeeze schema created successfully.';
GO
