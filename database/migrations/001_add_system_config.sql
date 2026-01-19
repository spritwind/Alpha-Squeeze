-- Migration: 001_add_system_config
-- Description: Add SystemConfig table for algorithm parameters and backfill tracking
-- Date: 2026-01-19

-- =============================================
-- 系統配置表 (SystemConfig)
-- 存儲演算法參數、系統設定等可調整值
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemConfig')
BEGIN
    CREATE TABLE SystemConfig (
        ConfigKey NVARCHAR(100) NOT NULL PRIMARY KEY,
        ConfigValue NVARCHAR(MAX) NOT NULL,
        ValueType NVARCHAR(20) NOT NULL,              -- STRING/INT/DECIMAL/BOOL/JSON
        Category NVARCHAR(50) NOT NULL,               -- SQUEEZE_WEIGHT/SQUEEZE_THRESHOLD/SYSTEM
        Description NVARCHAR(500),
        MinValue DECIMAL(18, 6),                      -- 數值型的最小值限制
        MaxValue DECIMAL(18, 6),                      -- 數值型的最大值限制
        IsReadOnly BIT DEFAULT 0,                     -- 是否為唯讀
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME DEFAULT GETDATE(),
        UpdatedBy NVARCHAR(100)
    );

    CREATE NONCLUSTERED INDEX IX_SystemConfig_Category
    ON SystemConfig(Category);

    PRINT 'Created SystemConfig table';
END
GO

-- =============================================
-- 資料回補任務表 (BackfillJobs)
-- 追蹤回補任務的執行狀態
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BackfillJobs')
BEGIN
    CREATE TABLE BackfillJobs (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        JobType NVARCHAR(50) NOT NULL,                -- STOCK_METRICS/WARRANT_DATA/FULL
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        Status NVARCHAR(20) NOT NULL,                 -- PENDING/RUNNING/COMPLETED/FAILED
        TotalTickers INT,
        ProcessedTickers INT DEFAULT 0,
        FailedTickers INT DEFAULT 0,
        ErrorMessage NVARCHAR(MAX),
        StartedAt DATETIME,
        CompletedAt DATETIME,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100)
    );

    CREATE NONCLUSTERED INDEX IX_BackfillJobs_Status
    ON BackfillJobs(Status, CreatedAt DESC);

    PRINT 'Created BackfillJobs table';
END
GO

-- =============================================
-- 追蹤股票清單表 (TrackedTickers)
-- 定義要追蹤的股票標的
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TrackedTickers')
BEGIN
    CREATE TABLE TrackedTickers (
        Ticker NVARCHAR(10) NOT NULL PRIMARY KEY,
        TickerName NVARCHAR(50),                      -- 股票名稱
        Category NVARCHAR(50),                        -- 類股 (半導體/金融/傳產等)
        IsActive BIT DEFAULT 1,                       -- 是否啟用追蹤
        Priority INT DEFAULT 100,                     -- 優先級 (數字越小越優先)
        AddedAt DATETIME DEFAULT GETDATE(),
        Notes NVARCHAR(500)
    );

    CREATE NONCLUSTERED INDEX IX_TrackedTickers_Active
    ON TrackedTickers(IsActive, Priority);

    PRINT 'Created TrackedTickers table';
END
GO

-- =============================================
-- 插入預設配置值
-- =============================================

-- Squeeze 權重配置
IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'SQUEEZE_WEIGHT_BORROW')
BEGIN
    INSERT INTO SystemConfig (ConfigKey, ConfigValue, ValueType, Category, Description, MinValue, MaxValue)
    VALUES
    ('SQUEEZE_WEIGHT_BORROW', '0.35', 'DECIMAL', 'SQUEEZE_WEIGHT', '法人空頭權重 (借券回補訊號)', 0, 1),
    ('SQUEEZE_WEIGHT_GAMMA', '0.25', 'DECIMAL', 'SQUEEZE_WEIGHT', 'Gamma效應權重 (IV-HV乖離)', 0, 1),
    ('SQUEEZE_WEIGHT_MARGIN', '0.20', 'DECIMAL', 'SQUEEZE_WEIGHT', '散戶燃料權重 (券資比)', 0, 1),
    ('SQUEEZE_WEIGHT_MOMENTUM', '0.20', 'DECIMAL', 'SQUEEZE_WEIGHT', '價量動能權重 (帶量突破)', 0, 1);

    PRINT 'Inserted squeeze weight defaults';
END
GO

-- Squeeze 門檻配置
IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'SQUEEZE_THRESHOLD_BULLISH')
BEGIN
    INSERT INTO SystemConfig (ConfigKey, ConfigValue, ValueType, Category, Description, MinValue, MaxValue)
    VALUES
    ('SQUEEZE_THRESHOLD_BULLISH', '70', 'INT', 'SQUEEZE_THRESHOLD', '看多訊號門檻 (分數 >= 此值)', 0, 100),
    ('SQUEEZE_THRESHOLD_BEARISH', '40', 'INT', 'SQUEEZE_THRESHOLD', '看空訊號門檻 (分數 <= 此值)', 0, 100);

    PRINT 'Inserted squeeze threshold defaults';
END
GO

-- 券資比評分區間配置
IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'MARGIN_SCORE_TIER1_MAX')
BEGIN
    INSERT INTO SystemConfig (ConfigKey, ConfigValue, ValueType, Category, Description, MinValue, MaxValue)
    VALUES
    ('MARGIN_SCORE_TIER1_MAX', '5', 'DECIMAL', 'MARGIN_SCORING', '券資比第一層上限 (0-5%)', 0, 100),
    ('MARGIN_SCORE_TIER2_MAX', '10', 'DECIMAL', 'MARGIN_SCORING', '券資比第二層上限 (5-10%)', 0, 100),
    ('MARGIN_SCORE_TIER3_MAX', '20', 'DECIMAL', 'MARGIN_SCORING', '券資比第三層上限 (10-20%)', 0, 100);

    PRINT 'Inserted margin scoring tier defaults';
END
GO

-- 系統設定
IF NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'BACKFILL_DEFAULT_DAYS')
BEGIN
    INSERT INTO SystemConfig (ConfigKey, ConfigValue, ValueType, Category, Description, MinValue, MaxValue)
    VALUES
    ('BACKFILL_DEFAULT_DAYS', '30', 'INT', 'SYSTEM', '預設回補天數', 1, 365),
    ('FINMIND_RATE_LIMIT_DELAY', '0.5', 'DECIMAL', 'SYSTEM', 'FinMind API 呼叫間隔 (秒)', 0.1, 10),
    ('MAX_CONCURRENT_FETCHES', '5', 'INT', 'SYSTEM', '最大並行抓取數', 1, 20);

    PRINT 'Inserted system config defaults';
END
GO

-- =============================================
-- 插入預設追蹤股票
-- =============================================
IF NOT EXISTS (SELECT 1 FROM TrackedTickers WHERE Ticker = '2330')
BEGIN
    INSERT INTO TrackedTickers (Ticker, TickerName, Category, Priority)
    VALUES
    -- 半導體
    ('2330', '台積電', '半導體', 1),
    ('2454', '聯發科', '半導體', 2),
    ('2303', '聯電', '半導體', 3),
    ('3711', '日月光投控', '半導體', 4),
    ('2379', '瑞昱', '半導體', 5),

    -- 電子權值
    ('2317', '鴻海', '電子', 10),
    ('2308', '台達電', '電子', 11),
    ('2382', '廣達', '電子', 12),
    ('2357', '華碩', '電子', 13),
    ('2412', '中華電', '電信', 14),

    -- 金融
    ('2881', '富邦金', '金融', 20),
    ('2882', '國泰金', '金融', 21),
    ('2883', '開發金', '金融', 22),
    ('2884', '玉山金', '金融', 23),
    ('2885', '元大金', '金融', 24),
    ('2886', '兆豐金', '金融', 25),
    ('2887', '台新金', '金融', 26),
    ('2891', '中信金', '金融', 27),
    ('2892', '第一金', '金融', 28),

    -- 傳產權值
    ('1301', '台塑', '塑化', 30),
    ('1303', '南亞', '塑化', 31),
    ('1326', '台化', '塑化', 32),
    ('2002', '中鋼', '鋼鐵', 33),
    ('2207', '和泰車', '汽車', 34),
    ('2912', '統一超', '零售', 35);

    PRINT 'Inserted default tracked tickers';
END
GO

-- =============================================
-- 取得配置的 Stored Procedure
-- =============================================
CREATE OR ALTER PROCEDURE sp_GetConfigByCategory
    @Category NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ConfigKey,
        ConfigValue,
        ValueType,
        Description,
        MinValue,
        MaxValue,
        IsReadOnly,
        UpdatedAt
    FROM SystemConfig
    WHERE Category = @Category
    ORDER BY ConfigKey;
END
GO

-- =============================================
-- 更新配置的 Stored Procedure
-- =============================================
CREATE OR ALTER PROCEDURE sp_UpdateConfig
    @ConfigKey NVARCHAR(100),
    @ConfigValue NVARCHAR(MAX),
    @UpdatedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IsReadOnly BIT;
    DECLARE @ValueType NVARCHAR(20);
    DECLARE @MinValue DECIMAL(18, 6);
    DECLARE @MaxValue DECIMAL(18, 6);

    -- 檢查配置是否存在
    SELECT
        @IsReadOnly = IsReadOnly,
        @ValueType = ValueType,
        @MinValue = MinValue,
        @MaxValue = MaxValue
    FROM SystemConfig
    WHERE ConfigKey = @ConfigKey;

    IF @IsReadOnly IS NULL
    BEGIN
        RAISERROR('Config key not found: %s', 16, 1, @ConfigKey);
        RETURN;
    END

    IF @IsReadOnly = 1
    BEGIN
        RAISERROR('Config key is read-only: %s', 16, 1, @ConfigKey);
        RETURN;
    END

    -- 驗證數值範圍
    IF @ValueType IN ('INT', 'DECIMAL')
    BEGIN
        DECLARE @NumValue DECIMAL(18, 6);
        SET @NumValue = TRY_CAST(@ConfigValue AS DECIMAL(18, 6));

        IF @NumValue IS NULL
        BEGIN
            RAISERROR('Invalid numeric value for config: %s', 16, 1, @ConfigKey);
            RETURN;
        END

        IF @MinValue IS NOT NULL AND @NumValue < @MinValue
        BEGIN
            RAISERROR('Value below minimum for config: %s (min: %s)', 16, 1, @ConfigKey, @MinValue);
            RETURN;
        END

        IF @MaxValue IS NOT NULL AND @NumValue > @MaxValue
        BEGIN
            RAISERROR('Value above maximum for config: %s (max: %s)', 16, 1, @ConfigKey, @MaxValue);
            RETURN;
        END
    END

    -- 更新配置
    UPDATE SystemConfig
    SET
        ConfigValue = @ConfigValue,
        UpdatedAt = GETDATE(),
        UpdatedBy = @UpdatedBy
    WHERE ConfigKey = @ConfigKey;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- =============================================
-- 驗證權重總和的 Function
-- =============================================
CREATE OR ALTER FUNCTION fn_ValidateSqueezeWeights()
RETURNS BIT
AS
BEGIN
    DECLARE @Total DECIMAL(18, 6);

    SELECT @Total = SUM(CAST(ConfigValue AS DECIMAL(18, 6)))
    FROM SystemConfig
    WHERE Category = 'SQUEEZE_WEIGHT';

    -- 允許 0.001 的誤差
    IF ABS(@Total - 1.0) < 0.001
        RETURN 1;

    RETURN 0;
END
GO

PRINT 'Migration 001_add_system_config completed successfully.';
GO
