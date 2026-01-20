-- Alpha Squeeze - Discovery Tables Migration
-- Version: 003
-- Description: Add DiscoveryPool and UserWatchList tables for radar scanning

-- =============================================
-- 潛在標的池 (DiscoveryPool)
-- 紀錄每日雷達掃描後的初步建議名單
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DiscoveryPool')
BEGIN
    CREATE TABLE DiscoveryPool (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        Ticker NVARCHAR(10) NOT NULL,              -- 股票代號
        TickerName NVARCHAR(50),                   -- 股票名稱
        Industry NVARCHAR(50),                     -- 產業類別
        ClosePrice DECIMAL(18, 2),                 -- 收盤價
        Volume BIGINT,                             -- 成交量
        AvgVolume5D BIGINT,                        -- 5日平均量
        VolMultiplier DECIMAL(18, 2),              -- 成交量倍數 (vs 5日均量)
        ShortSellingBalance BIGINT,                -- 借券賣出餘額
        SharesOutstanding BIGINT,                  -- 流通股數
        ShortRatio DECIMAL(18, 4),                 -- 空頭擁擠率 (借券/股本)
        MarginRatio DECIMAL(18, 4),                -- 券資比
        HasCB BIT DEFAULT 0,                       -- 是否有 CB
        CBTicker NVARCHAR(10),                     -- 關聯的 CB 代號
        CBPriceRatio DECIMAL(18, 4),               -- CB 股價比 (股價/轉換價)
        SqueezeScore INT,                          -- 初步權重分 (0-100)
        ScanDate DATE NOT NULL,                    -- 掃描日期
        CreatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_DiscoveryPool_Ticker_Date UNIQUE (Ticker, ScanDate)
    );

    -- Index for date-based queries
    CREATE NONCLUSTERED INDEX IX_DiscoveryPool_ScanDate
    ON DiscoveryPool(ScanDate DESC, SqueezeScore DESC);

    -- Index for ticker lookups
    CREATE NONCLUSTERED INDEX IX_DiscoveryPool_Ticker
    ON DiscoveryPool(Ticker, ScanDate DESC);

    PRINT 'Created DiscoveryPool table';
END
GO

-- =============================================
-- 用戶追蹤清單 (UserWatchList)
-- 紀錄用戶勾選後，系統必須執行「深度爬蟲」的標的
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserWatchList')
BEGIN
    CREATE TABLE UserWatchList (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        Ticker NVARCHAR(10) NOT NULL UNIQUE,       -- 股票代號
        TickerName NVARCHAR(50),                   -- 股票名稱
        AddedTime DATETIME DEFAULT GETDATE(),     -- 加入時間
        AddedBy NVARCHAR(50) DEFAULT 'WebUI',     -- 加入來源
        IsActive BIT DEFAULT 1,                    -- 是否持續追蹤
        Priority INT DEFAULT 100,                  -- 優先級
        LastDeepScrapedTime DATETIME,              -- 最近一次爬蟲更新時間
        LastSqueezeScore INT,                      -- 最近一次軋空分數
        Notes NVARCHAR(500),                       -- 備註
        UpdatedAt DATETIME DEFAULT GETDATE()
    );

    -- Index for active watchlist
    CREATE NONCLUSTERED INDEX IX_UserWatchList_Active
    ON UserWatchList(IsActive, Priority);

    PRINT 'Created UserWatchList table';
END
GO

-- =============================================
-- 掃描參數配置 (DiscoveryConfig)
-- 儲存雷達掃描的門檻參數
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DiscoveryConfig')
BEGIN
    CREATE TABLE DiscoveryConfig (
        ConfigKey NVARCHAR(50) PRIMARY KEY,
        ConfigValue NVARCHAR(100) NOT NULL,
        Description NVARCHAR(200),
        MinValue DECIMAL(18, 4),
        MaxValue DECIMAL(18, 4),
        UpdatedAt DATETIME DEFAULT GETDATE()
    );

    -- Insert default configuration
    INSERT INTO DiscoveryConfig (ConfigKey, ConfigValue, Description, MinValue, MaxValue) VALUES
    ('MinVolume', '1000', '最低成交量門檻 (張)', 100, 10000),
    ('MinPrice', '10', '最低股價門檻 (元)', 1, 100),
    ('MinShortRatio', '3', '最低借券比例門檻 (%)', 0.5, 20),
    ('MinVolMultiplier', '1.5', '最低量能爆發倍數', 1, 10),
    ('RequireCB', 'false', '是否必須有 CB', NULL, NULL),
    ('MaxResults', '100', '最大回傳筆數', 10, 500);

    PRINT 'Created DiscoveryConfig table with default values';
END
GO

PRINT 'Discovery tables migration completed successfully.';
GO
