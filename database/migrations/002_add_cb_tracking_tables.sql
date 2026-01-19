-- Migration: 002_add_cb_tracking_tables
-- Description: Add CB (Convertible Bond) tracking tables for warning light feature
-- Date: 2026-01-19

-- =============================================
-- CB 發行資訊表 (CBIssuance)
-- 存儲可轉換公司債的基本資訊
-- =============================================
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
        TotalIssueAmount DECIMAL(18, 4),             -- 發行總額 (億)
        OutstandingAmount DECIMAL(18, 4),            -- 流通餘額 (億)
        RedemptionTriggerPct DECIMAL(5, 2) DEFAULT 130.00,  -- 贖回觸發門檻 (%)
        RedemptionTriggerDays INT DEFAULT 30,        -- 連續觸發天數門檻
        IsActive BIT DEFAULT 1,                      -- 是否流通中
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UC_CBIssuance_Ticker UNIQUE (CBTicker)
    );

    CREATE NONCLUSTERED INDEX IX_CBIssuance_Underlying
    ON CBIssuance(UnderlyingTicker) WHERE IsActive = 1;

    PRINT 'Created CBIssuance table';
END
GO

-- =============================================
-- CB 每日追蹤表 (CBDailyTracking)
-- 每日記錄 CB 的觸發狀態與餘額變化
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CBDailyTracking')
BEGIN
    CREATE TABLE CBDailyTracking (
        ID BIGINT IDENTITY(1,1) NOT NULL,
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
        CONSTRAINT PK_CBDailyTracking PRIMARY KEY NONCLUSTERED (ID),
        CONSTRAINT UC_CBDailyTracking_Ticker_Date UNIQUE (CBTicker, TradeDate)
    );

    -- 時序查詢索引 (Clustered)
    CREATE CLUSTERED INDEX IX_CBDailyTracking_Date
    ON CBDailyTracking(TradeDate DESC, CBTicker);

    -- 預警查詢索引
    CREATE NONCLUSTERED INDEX IX_CBDailyTracking_Warning
    ON CBDailyTracking(TradeDate, WarningLevel)
    INCLUDE (CBTicker, ConsecutiveDaysAbove, OutstandingBalance);

    PRINT 'Created CBDailyTracking table';
END
GO

-- =============================================
-- CB 預警訊號表 (CBWarningSignals)
-- 儲存每日預警分析結果
-- =============================================
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

    PRINT 'Created CBWarningSignals table';
END
GO

-- =============================================
-- 插入測試用 CB 資料
-- =============================================
IF NOT EXISTS (SELECT 1 FROM CBIssuance WHERE CBTicker = '23301')
BEGIN
    INSERT INTO CBIssuance (
        CBTicker, UnderlyingTicker, CBName, IssueDate, MaturityDate,
        InitialConversionPrice, CurrentConversionPrice,
        TotalIssueAmount, OutstandingAmount
    ) VALUES
    ('23301', '2330', '台積電一', '2024-01-15', '2029-01-15', 850.00, 850.00, 50.00, 35.00),
    ('24541', '2454', '聯發科一', '2024-03-01', '2029-03-01', 1200.00, 1200.00, 30.00, 25.00),
    ('23171', '2317', '鴻海一', '2023-06-15', '2028-06-15', 120.00, 115.00, 80.00, 60.00),
    ('28811', '2881', '富邦金一', '2024-02-01', '2029-02-01', 65.00, 65.00, 40.00, 38.00);

    PRINT 'Inserted test CB issuance data';
END
GO

-- =============================================
-- 取得活躍 CB 清單的 Stored Procedure
-- =============================================
CREATE OR ALTER PROCEDURE sp_GetActiveCBs
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CBTicker,
        UnderlyingTicker,
        CBName,
        IssueDate,
        MaturityDate,
        CurrentConversionPrice,
        TotalIssueAmount,
        OutstandingAmount,
        RedemptionTriggerPct,
        RedemptionTriggerDays
    FROM CBIssuance
    WHERE IsActive = 1
      AND MaturityDate > GETDATE()
    ORDER BY UnderlyingTicker, CBTicker;
END
GO

-- =============================================
-- 取得 CB 每日追蹤資料的 Stored Procedure
-- =============================================
CREATE OR ALTER PROCEDURE sp_GetCBDailyTracking
    @TradeDate DATE = NULL,
    @MinWarningLevel NVARCHAR(20) = 'SAFE'
AS
BEGIN
    SET NOCOUNT ON;

    SET @TradeDate = ISNULL(@TradeDate, CAST(GETDATE() AS DATE));

    SELECT
        t.CBTicker,
        i.UnderlyingTicker,
        i.CBName,
        t.TradeDate,
        t.UnderlyingClosePrice,
        t.ConversionPrice,
        t.PriceToConversionRatio,
        t.IsAboveTrigger,
        t.ConsecutiveDaysAbove,
        t.OutstandingBalance,
        t.BalanceChangePercent,
        t.WarningLevel,
        i.TotalIssueAmount,
        i.MaturityDate
    FROM CBDailyTracking t
    INNER JOIN CBIssuance i ON t.CBTicker = i.CBTicker
    WHERE t.TradeDate = @TradeDate
      AND (
          @MinWarningLevel = 'SAFE' OR
          (@MinWarningLevel = 'CAUTION' AND t.WarningLevel IN ('CAUTION', 'WARNING', 'CRITICAL')) OR
          (@MinWarningLevel = 'WARNING' AND t.WarningLevel IN ('WARNING', 'CRITICAL')) OR
          (@MinWarningLevel = 'CRITICAL' AND t.WarningLevel = 'CRITICAL')
      )
    ORDER BY t.ConsecutiveDaysAbove DESC, t.OutstandingBalance DESC;
END
GO

-- =============================================
-- 更新 CB 每日追蹤資料的 Stored Procedure
-- =============================================
CREATE OR ALTER PROCEDURE sp_UpsertCBDailyTracking
    @CBTicker NVARCHAR(10),
    @TradeDate DATE,
    @UnderlyingClosePrice DECIMAL(18, 2),
    @ConversionPrice DECIMAL(18, 2),
    @OutstandingBalance DECIMAL(18, 4)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PrevConsecutiveDays INT = 0;
    DECLARE @PrevBalance DECIMAL(18, 4);
    DECLARE @TriggerPct DECIMAL(5, 2);
    DECLARE @TriggerDays INT;

    -- 取得前一日連續天數
    SELECT TOP 1 @PrevConsecutiveDays = ConsecutiveDaysAbove,
                 @PrevBalance = OutstandingBalance
    FROM CBDailyTracking
    WHERE CBTicker = @CBTicker AND TradeDate < @TradeDate
    ORDER BY TradeDate DESC;

    -- 取得觸發門檻設定
    SELECT @TriggerPct = RedemptionTriggerPct,
           @TriggerDays = RedemptionTriggerDays
    FROM CBIssuance
    WHERE CBTicker = @CBTicker;

    SET @TriggerPct = ISNULL(@TriggerPct, 130.00);
    SET @TriggerDays = ISNULL(@TriggerDays, 30);

    -- 計算指標
    DECLARE @PriceRatio DECIMAL(8, 4) =
        CASE WHEN @ConversionPrice > 0
             THEN (@UnderlyingClosePrice / @ConversionPrice) * 100
             ELSE 0 END;

    DECLARE @IsAboveTrigger BIT =
        CASE WHEN @PriceRatio >= @TriggerPct THEN 1 ELSE 0 END;

    DECLARE @ConsecutiveDays INT =
        CASE WHEN @IsAboveTrigger = 1
             THEN @PrevConsecutiveDays + 1
             ELSE 0 END;

    DECLARE @BalanceChange DECIMAL(8, 4) =
        CASE WHEN @PrevBalance > 0
             THEN ((@OutstandingBalance - @PrevBalance) / @PrevBalance) * 100
             ELSE 0 END;

    DECLARE @WarningLevel NVARCHAR(20) =
        CASE
            WHEN @ConsecutiveDays >= @TriggerDays THEN 'CRITICAL'
            WHEN @ConsecutiveDays >= @TriggerDays * 0.66 THEN 'WARNING'
            WHEN @ConsecutiveDays >= @TriggerDays * 0.33 THEN 'CAUTION'
            ELSE 'SAFE'
        END;

    -- Upsert
    MERGE CBDailyTracking AS target
    USING (SELECT @CBTicker AS CBTicker, @TradeDate AS TradeDate) AS source
    ON target.CBTicker = source.CBTicker AND target.TradeDate = source.TradeDate
    WHEN MATCHED THEN
        UPDATE SET
            UnderlyingClosePrice = @UnderlyingClosePrice,
            ConversionPrice = @ConversionPrice,
            PriceToConversionRatio = @PriceRatio,
            IsAboveTrigger = @IsAboveTrigger,
            ConsecutiveDaysAbove = @ConsecutiveDays,
            OutstandingBalance = @OutstandingBalance,
            BalanceChangePercent = @BalanceChange,
            WarningLevel = @WarningLevel
    WHEN NOT MATCHED THEN
        INSERT (CBTicker, TradeDate, UnderlyingClosePrice, ConversionPrice,
                PriceToConversionRatio, IsAboveTrigger, ConsecutiveDaysAbove,
                OutstandingBalance, BalanceChangePercent, WarningLevel)
        VALUES (@CBTicker, @TradeDate, @UnderlyingClosePrice, @ConversionPrice,
                @PriceRatio, @IsAboveTrigger, @ConsecutiveDays,
                @OutstandingBalance, @BalanceChange, @WarningLevel);

    SELECT @ConsecutiveDays AS ConsecutiveDays, @WarningLevel AS WarningLevel;
END
GO

PRINT 'Migration 002_add_cb_tracking_tables completed successfully.';
GO
